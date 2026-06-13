namespace Tutor.Api.Services
{
    using Baksteen.Extensions.DeepCopy;
    using Medallion.Threading;
    using Medallion.Threading.Redis;
    using Microsoft.EntityFrameworkCore;
    using StackExchange.Redis;
    using System.Text.Json;
    using Tutor.Api.Data;
    using Tutor.Api.Models.Account;
    using Tutor.Api.Models.Exceptions;
    using Tutor.Api.Models.Subscriptions;
    using Tutor.Api.Utilities;

    public class SubscriptionAssertionService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionAssertionService> _logger;
        private readonly IDistributedLockProvider _lockProvider;
        private readonly IDatabase _cacheDb;

        public SubscriptionAssertionService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionAssertionService> logger, IDistributedLockProvider lockProvider, IDatabase cacheDb)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _lockProvider = lockProvider;
            _cacheDb = cacheDb;
        }

        public async Task AssertUserSubscriptionAsync(string userId)
        {
            _logger.LogDebug("Subscription assertion requested for user {UserId}.", userId);

            using (_lockProvider.AcquireLock($"UserAccount:{userId}"))
            {
                _logger.LogDebug("Subscription assertion lock acquired for user {UserId}.", userId);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var tutorContext = scope.ServiceProvider.GetRequiredService<TutorContext>();
                var user = await GetUserAsync(userId, tutorContext);
                var useableCycles = user
                    .Subscriptions
                    .SelectMany(x => x.Cycles)
                    .OrderBy(x => x.CreatedAt)
                    .Where(x => x.Status.Equals(CycleStatus.Active) || x.Status.Equals(CycleStatus.Queued));

                var activeCycle = useableCycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));
                var queuedCycle = useableCycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Queued));

                if (await ValidateAndUpdateActiveCycle(user, activeCycle, tutorContext))
                {
                    _logger.LogDebug("Subscription assertion passed using active cycle for user {UserId}.", userId);
                    return;
                }

                if (!await ValidateAndUpdateQueuedCycle(user, queuedCycle, tutorContext))
                {
                    _logger.LogError("Subscription assertion failed for user {UserId}: no valid active or queued cycle.", userId);
                    throw new Exception(Errors.FAILED_VALIDATE_QUEUED_CYCLE);
                }

                _logger.LogDebug("Subscription assertion passed using queued cycle for user {UserId}.", userId);
            }
        }

        private async Task<bool> ValidateAndUpdateActiveCycle(User user, Cycle? activeCycle, TutorContext tutorContext)
        {
            if (activeCycle is null)
            {
                _logger.LogError("The user with id: {userId} does not have any active cycle!", user.Id);
                return false;
            }

            _logger.LogDebug(
                "Validating active cycle {CycleId} for user {UserId}; usage is {CurrentRequestCount}/{ValidRequestCount}.",
                activeCycle.Id,
                user.Id,
                activeCycle.CurrentRequestConut,
                activeCycle.ValidRequestCount);

            bool isValid = false;
            if (ValidateCycle(activeCycle))
            {
                activeCycle.CurrentRequestConut++;
                isValid = true;
                _logger.LogInformation(
                    "Active cycle {CycleId} accepted for user {UserId}; usage is now {CurrentRequestCount}/{ValidRequestCount}.",
                    activeCycle.Id,
                    user.Id,
                    activeCycle.CurrentRequestConut,
                    activeCycle.ValidRequestCount);
            }

            await UpdateDb(user, activeCycle, tutorContext);
            return isValid;
        }

        private async Task<bool> ValidateAndUpdateQueuedCycle(User user, Cycle? queuedCycle, TutorContext tutorContext)
        {
            if (queuedCycle is null)
            {
                _logger.LogError("The user with id: {userId} does not have any queued cycle!", user.Id);
                return false;
            }

            _logger.LogInformation(
                "Activating queued cycle {CycleId} for user {UserId}.",
                queuedCycle.Id,
                user.Id);

            queuedCycle.StartedAt = DateTime.UtcNow;
            queuedCycle.Status = CycleStatus.Active;

            bool isValid = false;
            if (ValidateCycle(queuedCycle))
            {
                queuedCycle.CurrentRequestConut++;
                isValid = true;
                _logger.LogInformation(
                    "Queued cycle {CycleId} accepted for user {UserId}; usage is now {CurrentRequestCount}/{ValidRequestCount}.",
                    queuedCycle.Id,
                    user.Id,
                    queuedCycle.CurrentRequestConut,
                    queuedCycle.ValidRequestCount);
            }

            await UpdateDb(user, queuedCycle, tutorContext);
            return isValid;
        }

        private bool ValidateCycle(Cycle cycle)
        {
            if (cycle.CurrentRequestConut >= cycle.ValidRequestCount)
            {
                cycle.ExpiredAt = DateTime.UtcNow;
                cycle.Status = CycleStatus.Expired;
                _logger.LogInformation(
                    "Cycle {CycleId} expired because all requests were used: {CurrentRequestCount}/{ValidRequestCount}.",
                    cycle.Id,
                    cycle.CurrentRequestConut,
                    cycle.ValidRequestCount);
                return false;
            }

            if (cycle.StartedAt is null)
            {
                _logger.LogError("An active cycle should have a not null 'StartAt' property!, Cycle: {@activeCycle}", cycle);
                throw new Exception(Errors.SOMETHING_WENT_WRONG);
            }

            if (cycle.StartedAt.Value.Add(cycle.Duration) < DateTime.UtcNow)
            {
                cycle.ExpiredAt = DateTime.UtcNow;
                cycle.Status = CycleStatus.Expired;
                _logger.LogInformation(
                    "Cycle {CycleId} expired because its duration ended. StartedAt: {StartedAt}; Duration: {Duration}.",
                    cycle.Id,
                    cycle.StartedAt,
                    cycle.Duration);
                return false;
            }

            _logger.LogDebug("Cycle {CycleId} is valid.", cycle.Id);
            return true;
        }

        private async Task<User> GetUserAsync(string userId, TutorContext tutorContext)
        {
            var redisUserValue = await _cacheDb.StringGetAsync(GetKey(userId));

            if (redisUserValue.HasValue)
            {
                _logger.LogDebug("Subscription user {UserId} loaded from cache.", userId);
                return redisUserValue.ToString().Deserialize<User, SubscriptionAssertionService>(_logger) ?? throw new Exception(Errors.SOMETHING_WENT_WRONG);
            }

            _logger.LogDebug("Subscription user {UserId} cache miss; loading from database.", userId);

            var user = await tutorContext.Users
                                .AsNoTracking()
                                .AsSplitQuery()
                                .Include(x => x.Subscriptions)
                                .ThenInclude(x => x.Cycles)
                                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user is null)
            {
                _logger.LogError("User with id: {userId} was not found in the database!", userId);
                throw new TutorException(Errors.USER_NOT_FOUND);
            }

            _logger.LogDebug(
                "Subscription user {UserId} loaded from database with {SubscriptionCount} subscriptions.",
                userId,
                user.Subscriptions.Count);

            return user.DeepCopy() ?? throw new Exception(Errors.DEEP_CLONE_FAILED);
        }

        private async Task UpdateDb(User user, Cycle cycle, TutorContext tutorContext)
        {
            try
            {
                tutorContext.Entry(cycle).State = EntityState.Modified;
                tutorContext.Update(cycle);
                await tutorContext.SaveChangesAsync();
                var userSerialized = JsonSerializer.Serialize(user);
                await _cacheDb.StringSetAsync(GetKey(user.Id), userSerialized);
                _logger.LogDebug(
                    "Persisted cycle {CycleId} for user {UserId} and refreshed subscription cache.",
                    cycle.Id,
                    user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the cycle with id: {cycleId} in the database!", cycle.Id);
                throw;
            }
        }

        private static string GetKey(string userId)
        {
            return $"tutor:user:{userId}";
        }
    }

}
