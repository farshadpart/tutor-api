using Serilog;

namespace Tutor.Api.Utilities;

public static class LoggingUtility
{
    public static void Configure(WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Services.AddSerilog();
    }

    public static void Run(WebApplication app)
    {
        Log.Logger.Information("Starting Tutor.Api!");
        try
        {
            app.Run();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
