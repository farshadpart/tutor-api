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
            using (_lockProvider.AcquireLock($"UserAccount:{userId}"))
            {
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
                    return;
                }

                if (!await ValidateAndUpdateQueuedCycle(user, queuedCycle, tutorContext))
                {
                    throw new Exception(Errors.FAILED_VALIDATE_QUEUED_CYCLE);
                }
            }
        }

        private async Task<bool> ValidateAndUpdateActiveCycle(User user, Cycle? activeCycle, TutorContext tutorContext)
        {
            if (activeCycle is null)
            {
                _logger.LogError("The user with id: {userId} does not have any active cycle!", user.Id);
                return false;
            }

            bool isValid = false;
            if (ValidateCycle(activeCycle))
            {
                activeCycle.CurrentRequestConut++;
                isValid = true;
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

            queuedCycle.StartedAt = DateTime.UtcNow;
            queuedCycle.Status = CycleStatus.Active;

            bool isValid = false;
            if (ValidateCycle(queuedCycle))
            {
                queuedCycle.CurrentRequestConut++;
                isValid = true;
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
                _logger.LogInformation("All possible requests in the cycle with id: {cycleId} have been used!", cycle.Id);
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
                return false;
            }

            return true;
        }

        private async Task<User> GetUserAsync(string userId, TutorContext tutorContext)
        {
            var redisUserValue = await _cacheDb.StringGetAsync(GetKey(userId));

            if (redisUserValue.HasValue)
            {
                return redisUserValue.ToString().Deserialize<User, SubscriptionAssertionService>(_logger) ?? throw new Exception(Errors.SOMETHING_WENT_WRONG);
            }

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
