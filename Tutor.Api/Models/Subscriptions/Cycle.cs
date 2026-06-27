namespace Tutor.Api.Models.Subscriptions
{
    public class Cycle: BaseEntity<Guid>, IBaseEntity<Guid>
    {
        public TimeSpan Duration { get; set; } = CycleSizeHelper.GetDuration(CycleSize.Standard);
        public int ValidRequestCount { get; set; } = CycleSizeHelper.GetValidRequestCount(CycleSize.Standard);
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? ExpiredAt { get; set; }
        public DateTimeOffset? CancelledAt { get; set; }
        public int CurrentRequestConut {  get; set; }
        public CycleStatus Status { get; set; }
    }

    public enum CycleStatus
    {
        Active,
        Queued,
        Expired,
        Canceled
    }
}
