namespace Tutor.Api.Services
{
    using Baksteen.Extensions.DeepCopy;
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Concurrent;
    using Tutor.Api.Data;
    using Tutor.Api.Models.Account;
    using Tutor.Api.Models.Exceptions;
    using Tutor.Api.Models.Subscriptions;

    public class UserStateService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UserStateService> _logger;
        private readonly ConcurrentDictionary<string, User> _cache = [];
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public UserStateService(IServiceScopeFactory scopeFactory, ILogger<UserStateService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task AssertUserSubscriptionAsync(string userId)
        {
            var safetyTimeout = TimeSpan.FromSeconds(10);
            var semaphore = _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
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
            finally
            {
                semaphore.Release();
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(userId, out _);
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
            if (_cache.TryGetValue(userId, out var userCached))
            {
                return userCached.DeepCopy() ?? throw new Exception(Errors.DEEP_CLONE_FAILED); ;
            }

            var user = await tutorContext.Users
                                .AsNoTracking()
                                .Include(x => x.Subscriptions)
                                .ThenInclude(x => x.Cycles)
                                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user is null)
            {
                _logger.LogError("User with id: {userId} was not found in the database!", userId);
                throw new TutorException(Errors.USER_NOT_FOUND);
            }

            var userCloned = user.DeepCopy() ?? throw new Exception(Errors.DEEP_CLONE_FAILED);
            _cache[userId] = userCloned;

            return userCloned;
        }

        private async Task UpdateDb(User user, Cycle cycle, TutorContext tutorContext)
        {
            try
            {
                tutorContext.Entry(cycle).State = EntityState.Modified;
                tutorContext.Update(cycle);
                await tutorContext.SaveChangesAsync();
                _cache[user.Id] = user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the cycle with id: {cycleId} in the database!", cycle.Id);
                throw;
            }
        }
    }

}
