
using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models.Exceptions;
using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;

namespace Tutor.Api.Services
{
    public class SubscriptionService(
        SubscriptionAssertionService subscriptionAssertionService,
        TutorContext tutorContext,
        ILogger<SubscriptionService> logger)
    {
        public async Task Create(CreateSubscriptionRequest createRequest)
        {
            logger.LogDebug(
                "Loading user {UserId} for subscription creation with group {SubscriptionGroup}.",
                createRequest.UserId,
                createRequest.SubscriptionGroup);

            var user = tutorContext.Users.Include(x => x.Subscriptions).FirstOrDefault(x => x.Id.Equals(createRequest.UserId));

            if (user is null)
            {
                logger.LogError("No users exist with id: {userId}", createRequest.UserId);
                throw new TutorException(Errors.USER_NOT_FOUND);
            }

            if (user.Subscriptions.Any(x => x.Group.Equals(createRequest.SubscriptionGroup)))
            {
                logger.LogError("The user with id {createRequest.UserId} already has a subscription with typeId: {createRequest.SubscriptionGroup}",
                    createRequest.UserId, createRequest.SubscriptionGroup);
                throw new TutorException(Errors.USER_ALREADY_HAS_SUBSCRIPTION);
            }

            logger.LogDebug(
                "Creating subscription entity for user {UserId} with group {SubscriptionGroup}.",
                createRequest.UserId,
                createRequest.SubscriptionGroup);

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

            await tutorContext.SaveChangesAsync();

            logger.LogInformation(
                "Created subscription {SubscriptionId} for user {UserId} with group {SubscriptionGroup}; active cycle {CycleId} started.",
                subscription.Id,
                createRequest.UserId,
                subscription.Group,
                subscription.Cycles.First().Id);
        }

        public async Task RegisterRequest(string userId)
        {
            logger.LogDebug("Registering subscription request usage for user {UserId}.", userId);

            var activeCycle = tutorContext.Users
                .AsSplitQuery()
                .Include(x => x.Subscriptions)
                .ThenInclude(x => x.Cycles)
                .AsSingleQuery()
                .FirstOrDefault(x => x.Id.Equals(userId))?.Subscriptions.SelectMany(x => x.Cycles)
                .FirstOrDefault(x => x.Status.Equals(CycleStatus.Active));

            if (activeCycle is null)
            {
                logger.LogError("The user with id: {userId} does not have any active cycle!", userId);
                throw new TutorException(Errors.NO_ACTIVE_CYCLE);
            }

            activeCycle.CurrentRequestConut++;

            await tutorContext.SaveChangesAsync();

            logger.LogInformation(
                "Registered subscription request for user {UserId}; cycle {CycleId} usage is now {CurrentRequestCount}/{ValidRequestCount}.",
                userId,
                activeCycle.Id,
                activeCycle.CurrentRequestConut,
                activeCycle.ValidRequestCount);
        }

        public List<string> GetSubscriptionGroups()
        {
            var subscriptionGroups = Enum.GetValues<SubscriptionGroup>().Select(e => e.ToString()).ToList();
            logger.LogDebug("Resolved {SubscriptionGroupCount} subscription groups.", subscriptionGroups.Count);
            return subscriptionGroups;
        }

        public SubscriptionGroup? GetUserUseableSubscriptionGroup(string userId)
        {
            logger.LogDebug("Resolving usable subscription group for user {UserId}.", userId);

            var useableSubscription = tutorContext.Users
                .AsSplitQuery()
                .Include(x => x.Subscriptions)
                .ThenInclude(x => x.Cycles)
                .AsSingleQuery()
                .FirstOrDefault(x => x.Id.Equals(userId))?.Subscriptions
                .FirstOrDefault(x => x.Cycles.OrderBy(x => x.CreatedAt).Any(c => c.Status.Equals(CycleStatus.Active) || c.Status.Equals(CycleStatus.Queued)));

            if (useableSubscription is null)
            {
                logger.LogInformation("No usable subscription group found for user {UserId}.", userId);
            }
            else
            {
                logger.LogDebug(
                    "Resolved usable subscription group {SubscriptionGroup} for user {UserId}.",
                    useableSubscription.Group,
                    userId);
            }

            return useableSubscription?.Group;
        }

        public async Task Assert(string userId)
        {
            logger.LogDebug("Asserting subscription for user {UserId}.", userId);
            await subscriptionAssertionService.AssertUserSubscriptionAsync(userId);
            logger.LogDebug("Subscription assertion completed for user {UserId}.", userId);
        }
    }
}
