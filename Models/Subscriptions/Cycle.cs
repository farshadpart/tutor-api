namespace Tutor.Api.Models.Subscriptions
{
    public class Cycle
    { 
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? ExpiredAt { get; set; }
        public int CurrentRequestConut {  get; set; }
        public CycleStatus Status { get; set; }
    }

    public enum CycleStatus
    {
        Active,
        Queued,
        Expired
    }
}
