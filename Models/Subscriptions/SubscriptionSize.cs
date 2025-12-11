namespace Tutor.Api.Models.Subscriptions
{
    public static class CycleSizeHelper
    {
        public static TimeSpan GetDuration(CycleSize size)
        {
            return size switch
            {
                CycleSize.Standard => TimeSpan.FromDays(30),
                CycleSize.PlayTest => TimeSpan.FromDays(30 * 365),
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };
        }

        public static int GetValidRequestCount(CycleSize size)
        {
            return size switch
            {
                CycleSize.Standard => 5000,
                CycleSize.PlayTest => 5000,
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };
        }
    }

    public enum CycleSize
    {
        Standard,
        PlayTest
    }
}
