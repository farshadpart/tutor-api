namespace Tutor.Api.Models.Tutor.Api.Contracts.Subscription
{
    public record CreateSubscriptionRequest(string UserId, Guid SubscriptionTypeId, DateTimeOffset StartAt);
}
