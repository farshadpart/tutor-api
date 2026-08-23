using System.Globalization;
using System.Threading.RateLimiting;

namespace Tutor.Api.Utilities;

public static class RateLimiterUtility
{
    public static void AddRateLimiter(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("Chat", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.GetUserIdentifier(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 20,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1)
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Please try again later.", cancellationToken);
            };
        });
    }
}
