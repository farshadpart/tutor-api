using Microsoft.Extensions.Logging;

namespace Tutor.Api.Tests.Utility;

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
