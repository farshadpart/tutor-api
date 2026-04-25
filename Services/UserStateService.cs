namespace Tutor.Api.Services
{
    using FluentResults;
    using Microsoft.EntityFrameworkCore;
    using Polly;
    using RedLockNet;
    using System.Collections.Concurrent;
    using Tutor.Api.Data;
    using Tutor.Api.Models.Account;
    using Tutor.Api.Models.Exceptions;
    using Tutor.Api.Models.Subscriptions;
    using Tutor.Api.Utilities;

    public class UserStateService
    {
        private readonly TutorContext _tutorContext;
        private readonly ILogger<UserStateService> _logger;
        private readonly IDistributedLockFactory _lockFactory;
        private readonly ConcurrentDictionary<string, User> _cache = [];
        private readonly ResiliencePipeline _retryPipeline = ResiliencePipelineUtility.CreateAssertRequestPipeline();

        public UserStateService(TutorContext db, ILogger<UserStateService> logger, IDistributedLockFactory distributedLockFactory)
        {
            _tutorContext = db;
            _logger = logger;
            _lockFactory = distributedLockFactory;
        }

        public async Task AssertAsync(string userId)
        {
            await _retryPipeline.ExecuteAsync(async cancellationToken =>
            {
                var resource = $"lock:user:{userId}";
                var safetyTimeout = TimeSpan.FromSeconds(10);
                await using var redLock = await _lockFactory.CreateLockAsync(resource, safetyTimeout);
                if (!redLock.IsAcquired)
                {
                    _logger.LogWarning("Could not acquire lock for user update. UserId: {UserId}", userId);
                    throw new Exception(Errors.COULD_NOT_ACQUIRE_LOCK_FOR_USER_UPDATE);
                }

                var user = await GetUserAsync(userId);
                var useableCycles = user
                    .Subscriptions
                    .SelectMany(x => x.Cycles)
                    .OrderBy(x => x.CreatedAt)
                    .Where(x => x.Status.Equals(CycleStatus.Active) || x.Status.Equals(CycleStatus.Queued));

                if (useableCycles is null || !useableCycles.Any())
                {
                    _logger.LogError("The user with id: {userId} does not have any active or queued cycle!", userId);
                    throw new TutorException(Errors.NO_ACTIVE_CYCLE);
                }

                var activeCycle = useableCycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));
                var queuedCycle = useableCycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Queued));
                await ValidateCycle(activeCycle);
                if ((activeCycle is null || !activeCycle.Status.Equals(CycleStatus.Active)) && queuedCycle is null)
                {
                    _logger.LogError("The user with id: {userId} does not have any active or queued cycle!", userId);
                    throw new TutorException(Errors.NO_ACTIVE_CYCLE);
                }

                if (activeCycle is not null && activeCycle.Status.Equals(CycleStatus.Active))
                {
                    activeCycle.CurrentRequestConut++;
                    await UpdateDb(activeCycle);
                    return;
                }

                if (queuedCycle is not null)
                {
                    queuedCycle.StartedAt = DateTime.UtcNow;
                    queuedCycle.Status = CycleStatus.Active;
                    queuedCycle.CurrentRequestConut = 1;
                    await UpdateDb(queuedCycle);
                    return;
                }

                _logger.LogError("Something went wrong when asserting the subscription for user with id: {userId}!", userId);
                throw new Exception(Errors.SOMETHING_WENT_WRONG);
            });
        }

        private async Task ValidateCycle(Cycle? cycle)
        {
            if (cycle is null)
            {
                throw new Exception(Errors.NO_CYCLE_AVAILABLE);
            }
            
            if (cycle.CurrentRequestConut > cycle.ValidRequestCount)
            {
                cycle.ExpiredAt = DateTime.UtcNow;
                cycle.Status = CycleStatus.Expired;
                _logger.LogInformation("All possible requests in the cycle with id: {cycleId} have been used!", cycle.Id);
                await UpdateDb(cycle);
                throw new Exception(Errors.ALL_REQUESTS_USED);
            }
            else if (cycle.StartedAt is null)
            {
                _logger.LogError("An active cycle should have a not null 'StartAt' property!, Cycle: {@activeCycle}", cycle);
                throw new Exception(Errors.SOMETHING_WENT_WRONG);
            }
            else if (cycle.StartedAt.Value.Add(cycle.Duration) < DateTime.UtcNow)
            {
                cycle.ExpiredAt = DateTime.UtcNow;
                cycle.Status = CycleStatus.Expired;
                await UpdateDb(cycle);
                throw new Exception(Errors.TIME_WINDOW_FINISHED);
            }
        }

        private async Task<User> GetUserAsync(string userId)
        {
            if(_cache.TryGetValue(userId, out var userCached))
            {
                return userCached;
            }

            var user = await _tutorContext.Users
                                .AsNoTracking()
                                .Include(x => x.Subscriptions)
                                .ThenInclude(x => x.Cycles)
                                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user is null)
            {
                _logger.LogError("User with id: {userId} was not found in the database!", userId);
                throw new TutorException(Errors.USER_NOT_FOUND);
            }

            return user;
        }

        private async Task UpdateDb(Cycle cycle)
        {
            _tutorContext.Entry(cycle).State = EntityState.Modified;
            _tutorContext.Update(cycle);
            await _tutorContext.SaveChangesAsync();
        }
    }

}
