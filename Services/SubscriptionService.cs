
using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models.Exceptions;
using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;

namespace Tutor.Api.Services
{
    public class SubscriptionService
    {
        private readonly SubscriptionAssertionService subscriptionAssertionService;
        private readonly TutorContext _tutorContext;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(SubscriptionAssertionService subscriptionAssertionService, TutorContext tutorContext, ILogger<SubscriptionService> logger)
        {
            this.subscriptionAssertionService = subscriptionAssertionService;
            _tutorContext = tutorContext;
            _logger = logger;
        }

        public async Task Create(CreateSubscriptionRequest createRequest)
        {
            var user = _tutorContext.Users.Include(x => x.Subscriptions).FirstOrDefault(x => x.Id.Equals(createRequest.UserId));

            if (user is null)
            {
                _logger.LogError("No users exist with id: {userId}", createRequest.UserId);
                throw new TutorException(Errors.USER_NOT_FOUND);
            }

            if (user.Subscriptions.Any(x => x.Group.Equals(createRequest.SubscriptionGroup)))
            {
                _logger.LogError("The user with id {createRequest.UserId} already has a subscription with typeId: {createRequest.SubscriptionGroup}",
                    createRequest.UserId, createRequest.SubscriptionGroup);
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

        public async Task Assert(string userId)
        {
            await subscriptionAssertionService.AssertUserSubscriptionAsync(userId);
        }
    }
}