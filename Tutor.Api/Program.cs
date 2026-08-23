using Tutor.Api.Utilities;

namespace Tutor.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        LoggingUtility.Configure(builder);

        var connectionStrings = 
            EnvironmentUtility.GetConnectionStrings(builder.Environment, builder.Configuration);

        DatabaseUtility.AddDatabase(builder.Services, connectionStrings);
        RateLimiterUtility.AddRateLimiter(builder.Services);
        AuthenticationUtility.AddAuthentication(builder.Services, builder.Configuration);
        IdentityUtility.AddIdentity(builder.Services);
        ServiceRegistrationUtility.AddApplicationServices(builder.Services, builder.Configuration);
        RedisUtility.AddRedis(builder.Services, connectionStrings);

        var app = builder.Build();

        ApplicationUtility.InsertInitialData(app.Services);
        MiddlewareUtility.ConfigurePipeline(app);
        LoggingUtility.Run(app);
    }
}
