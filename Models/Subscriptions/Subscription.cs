using Tutor.Api.Models.Account;

namespace Tutor.Api.Models.Subscriptions
{
    public class Subscription
    {
        public Guid Id { get; set; }
        public User User { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public Guid SubscriptionTypeId { get; set; }
        public SubscriptionType SubscriptionType { get; set; } = new();
        public List<Cycle> Cycles { get; set; } = [];
    }
}
