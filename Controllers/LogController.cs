using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using System.Text.Json;
using Tutor.Api.Models.Tutor.Api.Contracts.Log;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LogController(ILogger<LogController> logger) : ControllerBase
    {
        [HttpPost("log")]
        public Task Log([FromBody] LogRequest logRequest)
        {
            if (!logger.IsEnabled(logRequest.LogLevel) && logRequest is { Arguments.Length: > 15 })
            {
                return Task.CompletedTask;
            }

            using (LogContext.PushProperty("LogStream", "Mobile"))
            {
                var exception = logRequest.Exception is not null ? new Exception(logRequest.Exception) : null;

#pragma warning disable CA2254
                logger.Log(logLevel: logRequest.LogLevel, exception: exception, message: logRequest.Message, args: NormalizeArguments(logRequest.Arguments));
#pragma warning restore CA2254
            }
            
            return Task.CompletedTask;
        }

        private static object?[] NormalizeArguments(object?[]? args)
        {
            if (args is null || args.Length == 0)
                return [];

            return [.. args.Select(ConvertArgument)];
        }

        private static object? ConvertArgument(object? arg)
        {
            if (arg is JsonElement jsonElement)
                return jsonElement.GetRawText();

            return arg;
        }
    }
}
