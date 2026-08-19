using Serilog;

namespace Tutor.Api.Utilities;

public static class EnvironmentUtility
{
    public static string GetJwtSecretKey()
    {
        var jwtSecretKey = Environment.GetEnvironmentVariable("JwtSecretKey");
        if (string.IsNullOrWhiteSpace(jwtSecretKey))
        {
            throw new InvalidOperationException("Environment variable 'JwtSecretKey' is missing or empty.");
        }

        return jwtSecretKey;
    }

    public static List<(string Name, string Value)> GetConnectionStrings(IWebHostEnvironment environment, IConfiguration configuration)
    {
        string? tutorConnectionString;
        string? redisConnectionString;
        if (environment.IsDevelopment())
        {
            tutorConnectionString = configuration.GetConnectionString("TutorContext")
                                    ?? throw new InvalidOperationException("Connection string: 'TutorContext' not found.");
            redisConnectionString = configuration.GetConnectionString("Redis")
                                    ?? throw new InvalidOperationException("Connection string: 'Redis' not found.");
                
            return [("TutorContext", tutorConnectionString), ("Redis", redisConnectionString)];
        }

        tutorConnectionString = Environment.GetEnvironmentVariable("TutorConnectionString");
        if (string.IsNullOrEmpty(tutorConnectionString))
        {
            Log.Logger.Fatal("Connection string: 'TutorConnectionString' not found.");   
            throw new InvalidOperationException("Connection string: 'TutorContext' not found.");
        }
            
        redisConnectionString = Environment.GetEnvironmentVariable("RedisConnectionString");
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            Log.Logger.Fatal("Connection string: 'RedisConnectionString' not found.");
            throw new InvalidOperationException("Connection string: 'Redis' not found.");
        }
            
        return [("TutorContext", tutorConnectionString), ("Redis", redisConnectionString)];
    }
}
