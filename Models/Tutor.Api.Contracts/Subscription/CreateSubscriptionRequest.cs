using Tutor.Api.Models.Subscriptions;

namespace Tutor.Api.Models.Tutor.Api.Contracts.Subscription
{
    public record CreateSubscriptionRequest(string UserId, SubscriptionGroup SubscriptionGroup);
}
