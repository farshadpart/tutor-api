using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Tutor.Api.Filters;

public class ControllerExecutionTimingFilter(
    ILogger<ControllerExecutionTimingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();
            var action = (ControllerActionDescriptor)context.ActionDescriptor;

            logger.LogInformation(
                "Controller method {Controller}.{Action} executed in {ElapsedMilliseconds:F2} ms.",
                action.ControllerName,
                action.ActionName,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
