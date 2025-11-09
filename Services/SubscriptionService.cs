
using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models.Exceptions;
using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;

namespace Tutor.Api.Services
{
    public class SubscriptionService
    {
        private readonly TutorContext _tutorContext;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(TutorContext tutorContext, ILogger<SubscriptionService> logger)
        {
            _tutorContext = tutorContext;
            _logger = logger;
        }

        public async Task Create(CreateSubscriptionRequest createRequest)
        {
            var user = _tutorContext.Users.Include(x => x.Subscriptions).FirstOrDefault(x => x.Id.Equals(createRequest.UserId));

            if (user is null)
            {
                _logger.LogError("No users exist with id: {userId}", createRequest.UserId);
                throw new TutorException(Errors.USER_NOT_EXIST);
            }

            if(user.Subscriptions.Any(x => x.Group.Equals(createRequest.SubscriptionGroup)))
            {
                _logger.LogError($"The user with id {createRequest.UserId} already has a subscription with typeId: {createRequest.SubscriptionGroup}");
                throw new TutorException(Errors.USER_ALREADY_HAS_SUBSCRIPTION);
            }

            var subscription = new Subscription
            {
                CreatedAt = DateTime.UtcNow,
                Group = createRequest.SubscriptionGroup
            };

            subscription.Cycles.Add(new Cycle
            {
                CreatedAt = DateTime.UtcNow,
                Status = CycleStatus.Active,
                StartedAt = DateTime.UtcNow
            });

            user.Subscriptions.Add(subscription);

            await _tutorContext.SaveChangesAsync();
        }

        public async Task Assert(string userId)
        {
            var useableCycles = _tutorContext.Users
                .Include(x => x.Subscriptions)
                .ThenInclude(x => x.Cycles)
                .Where(x => x.Id.Equals(userId))
                .SelectMany(x => x.Subscriptions)
                .SelectMany(x => x.Cycles)
                .OrderBy(x => x.CreatedAt)
                .Where(x => x.Status.Equals(CycleStatus.Active) || x.Status.Equals(CycleStatus.Queued));

            if (useableCycles is null || useableCycles.Count().Equals(0))
            {
                _logger.LogError("The user with id: {userId} does not have any active or queued cycle!", userId);
                throw new TutorException(Errors.NO_ACTIVE_CYCLE);
            }

            var activeCycle = useableCycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));
            var queuedCycle = useableCycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Queued));
            ValidateCycle(activeCycle);
            if((activeCycle is null || !activeCycle.Status.Equals(CycleStatus.Active)) && queuedCycle is null)
            {
                _logger.LogError("The user with id: {userId} does not have any active or queued cycle!", userId);
                throw new TutorException(Errors.NO_ACTIVE_CYCLE);
            }

            if(activeCycle is not null && activeCycle.Status.Equals(CycleStatus.Active))
            {
                activeCycle.CurrentRequestConut++;
                await _tutorContext.SaveChangesAsync();
                return;
            }
            
            if(queuedCycle is not null)
            {
                queuedCycle.StartedAt = DateTime.UtcNow;
                queuedCycle.Status = CycleStatus.Active;
                queuedCycle.CurrentRequestConut = 1;
                await _tutorContext.SaveChangesAsync();
                return;
            }

            _logger.LogError("Something went wrong when asserting the subscription for user with id: {userId}!", userId);
            throw new Exception(Errors.SOMETHING_WENT_WRONG);
        }

        private void ValidateCycle(Cycle? cycle)
        {
            if (cycle is not null)
            {
                if (cycle.CurrentRequestConut > cycle.ValidRequestCount)
                {
                    cycle.ExpiredAt = DateTime.UtcNow;
                    cycle.Status = CycleStatus.Expired;
                    _logger.LogInformation("All possible requests in the cycle with id: {cycleId} have been used!", cycle.Id);
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
                    _logger.LogInformation("The time window of the cycle with id: {cycleId} has finished!", cycle.Id);
                }
            }
        }

        public async Task RegisterRequest(string userId)
        {
            var activeCycle = _tutorContext.Users.Include(x => x.Subscriptions)
                .ThenInclude(x => x.Cycles)
                .FirstOrDefault(x => x.Id.Equals(userId))?.Subscriptions.SelectMany(x => x.Cycles)
                .FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));

            if (activeCycle is null)
            {
                _logger.LogError("The user with id: {userId} does not have any active cycle!", userId);
                throw new TutorException(Errors.NO_ACTIVE_CYCLE);
            }

            activeCycle.CurrentRequestConut++;

            await _tutorContext.SaveChangesAsync();
        }

        public List<string> GetSubscriptionGroups()
        {
            return [.. Enum.GetValues<SubscriptionGroup>().Select(e => e.ToString())];
        }

        public SubscriptionGroup? GetUserUseableSubscriptionGroup(string userId)
        {
            var useableSubscription = _tutorContext.Users
                .Include(x => x.Subscriptions)
                .ThenInclude(x => x.Cycles)
                .FirstOrDefault(x => x.Id.Equals(userId))?.Subscriptions
                .FirstOrDefault(x => x.Cycles.OrderBy(x => x.CreatedAt).Any(c => c.Status.Equals(CycleStatus.Active) || c.Status.Equals(CycleStatus.Queued)));

            return useableSubscription?.Group;
        }
    }
}
