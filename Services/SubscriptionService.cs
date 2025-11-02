
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
        private readonly ILogger _logger;

        public SubscriptionService(TutorContext tutorContext, ILogger logger)
        {
            _tutorContext = tutorContext;
            _logger = logger;
        }

        public async Task Create(CreateSubscriptionRequest createRequest)
        {
            if (createRequest.StartAt < DateTime.UtcNow)
            {
                _logger.LogError("The subscription start date is invalid! Start Date: {startDate}", createRequest.StartAt.ToString());
                throw new TutorException(Errors.SUBSCRIPTION_START_DATE_INVALID);
            }

            var subscriptionType = _tutorContext.Set<SubscriptionType>().FirstOrDefault(x => x.Id.Equals(createRequest.SubscriptionTypeId));
            if(subscriptionType is null)
            {
                _logger.LogError("The subscriptio type with id: {subscriptionTypeId} does not exist!", createRequest.SubscriptionTypeId);
                throw new TutorException(Errors.SUBSCRIPTION_TYPE_NOT_EXIST);
            }

            var user = _tutorContext.Users.FirstOrDefault(x => x.Id.Equals(createRequest.UserId));
            if (user is null)
            {
                _logger.LogError("No users exist with id: {userId}", createRequest.UserId);
                throw new TutorException(Errors.USER_NOT_EXIST);
            }

            if(user.Subscriptions.Any(x => x.SubscriptionType.Id.Equals(createRequest.SubscriptionTypeId)))
            {
                _logger.LogError($"The user with id {createRequest.UserId} already has a subscription with typeId: {createRequest.SubscriptionTypeId}");
                throw new TutorException(Errors.USER_ALREADY_HAS_SUBSCRIPTION);
            }

            var subscription = new Subscription
            {
                Id = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                SubscriptionTypeId = createRequest.SubscriptionTypeId,
            };

            subscription.Cycles.Add(new Cycle
            {
                Id = Guid.CreateVersion7(),
                CreatedAt = DateTime.UtcNow,
                Status = CycleStatus.Active
            });

            user.Subscriptions.Add(subscription);

            await _tutorContext.SaveChangesAsync();
        }

        public async Task Active(ActiveSubscriptionRequest activeRequest)
        {
            var user = _tutorContext.Users.FirstOrDefault(x => x.Id.Equals(activeRequest.UserId));
            if (user is null)
            {
                _logger.LogError("No users exist with id: {userId}", activeRequest.UserId);
                throw new TutorException(Errors.USER_NOT_EXIST);
            }

            var userSubscription = user.Subscriptions.FirstOrDefault(x => x.Id.Equals(activeRequest.SubscriptionId));
            if (userSubscription is null) 
            {
                _logger.LogError("The user does not have any subscription with id: {subscriptionID}", activeRequest.SubscriptionId);
                throw new TutorException(Errors.USER_NOT_HAVE_SUBSCRIPTION);
            }

            user.ActiveSubscriptionId = activeRequest.SubscriptionId;
            await _tutorContext.SaveChangesAsync();
        }

        public async Task Assert(string userEmail)
        {
            var user = _tutorContext.Users.FirstOrDefault(x => x.Id.Equals(userEmail));
            if (user is null)
            {
                _logger.LogError("No users exist with email: {email}", userEmail);
                throw new TutorException(Errors.USER_NOT_EXIST);
            }

            var activeSubscription = user.Subscriptions.FirstOrDefault(x =>x.Id.Equals(userEmail));
            if (activeSubscription is null)
            {
                _logger.LogError("The user does not have any active subscription");
                throw new TutorException(Errors.USER_NOT_HAVE_SUBSCRIPTION);
            }

            var activeCycle = activeSubscription.Cycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));
            if (activeCycle is null)
            {
                _logger.LogError("The subscription with id: {subscriptionId} does not have any active cycle!", activeSubscription.Id);
                throw new TutorException(Errors.NO_ACTIVE_CYCLE);
            }

            if(activeCycle.CurrentRequestConut >= activeSubscription.SubscriptionType.MaxRequestCount)
            {
                activeCycle.ExpiredAt = DateTime.UtcNow;
                activeCycle.Status = CycleStatus.Expired;
                _logger.LogInformation("All possible requests in the cycle with id: {cycleId} have been used!", activeCycle.Id);
            }

            if(activeCycle.StartedAt is null)
            {
                _logger.LogError("An active cycle should have a not null 'StartAt' property!, Cycle: {@activeCycle}", activeCycle);
                throw new Exception(Errors.SOMETHING_WENT_WRONG);
            }

            if (activeCycle.StartedAt.Value.Add(activeSubscription.SubscriptionType.Duration) <= DateTime.UtcNow)
            {
                activeCycle.ExpiredAt = DateTime.UtcNow;
                activeCycle.Status = CycleStatus.Expired;
                _logger.LogInformation("The time window of the cycle with id: {cycleId} has finished!", activeCycle.Id);
            }

            var queuedCycle = activeSubscription.Cycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Queued));
            if (queuedCycle is null)
            {
                user.ActiveSubscriptionId = null;
                await _tutorContext.SaveChangesAsync();
                throw new TutorException(Errors.USER_NOT_HAVE_SUBSCRIPTION);
            }

            queuedCycle.StartedAt = DateTime.UtcNow;
            queuedCycle.Status = CycleStatus.Active;
            await _tutorContext.SaveChangesAsync();
        }

        public async Task RegisterRequest(string userId)
        {
            var subscription = Get(x => x.User.Id.Equals(userId));
            if (subscription is null)
            {
                _logger.LogError("The user does not have any active subscription");
                throw new TutorException(Errors.USER_NOT_HAVE_SUBSCRIPTION);
            }

            var activeCycle = subscription.Cycles.FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));
            if (activeCycle is null)
            {
                _logger.LogError("The subscription with id: {subscriptionId} does not have any active cycle!", subscription.Id);
                throw new TutorException(Errors.NO_ACTIVE_CYCLE);
            }

            activeCycle.CurrentRequestConut++;

            await _tutorContext.SaveChangesAsync();
        }

        public async Task<List<SubscriptionType>> GetSubscriptionTypes()
        {
            return await _tutorContext.Set<SubscriptionType>().ToListAsync();
        }

        private Subscription? Get(Func<Subscription, bool> func) 
        {
            return _tutorContext.Set<Subscription>().FirstOrDefault(func);
        }
    }
}
