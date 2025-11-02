namespace Tutor.Api.Models.Subscriptions
{
    public class Cycle
    { 
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpiredAt { get; set; }
        /// <summary>
        /// If no other terminating conditions are met, this cycle will remain active until this date.
        /// </summary>
        public DateTimeOffset MaxValidUntil { get; set; }
        public int CurrentRequestConut {  get; set; }
        public CycleStatus Status { get; set; }
    }

    public enum CycleStatus
    {
        Active,
        Completed,
        Expired
    }
}
