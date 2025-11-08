namespace Tutor.Api.Models.Subscriptions
{
    public class Subscription
    {
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public SubscriptionGroup Group { get; set; } = SubscriptionGroup.Basic;
        public List<Cycle> Cycles { get; set; } = [];
    }

    public enum SubscriptionGroup
    {
        Basic
    }
}
