namespace Tutor.Api.Models.Subscriptions
{
    public class Subscription : BaseEntity<Guid>, IBaseEntity<Guid>
    {
        public SubscriptionGroup Group { get; set; } = SubscriptionGroup.Basic;
        public List<Cycle> Cycles { get; set; } = [];
    }

    public enum SubscriptionGroup
    {
        Basic
    }
}
