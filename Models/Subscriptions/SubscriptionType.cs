namespace Tutor.Api.Models.Subscriptions
{
    public class SubscriptionType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MaxRequestCount { get; } = 5000;
        public TimeSpan Duration { get; } = TimeSpan.FromDays(90);
    }
}
