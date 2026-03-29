using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using System.Text.Json;
using Tutor.Api.Models.Tutor.Api.Contracts.Log;

namespace Tutor.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LogController(ILogger<AccountController> Logger) : ControllerBase
    {
        [HttpPost("log")]
        public async Task Log([FromBody] LogRequest logRequest)
        {
            if (!Logger.IsEnabled(logRequest.LogLevel) && logRequest is { Arguments.Length: > 15 })
            {
                return;
            }

            using (LogContext.PushProperty("LogStream", "Mobile"))
            {
                Exception? exception = logRequest.Exception is not null ? new Exception(logRequest.Exception) : null;

#pragma warning disable CA2254
                Logger.Log(logLevel: logRequest.LogLevel, exception: exception, message: logRequest.Message, args: NormalizeArguments(logRequest.Arguments));
#pragma warning restore CA2254
            }
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
