using Medallion.Threading;
using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace Tutor.Api.Utilities;

public static class RedisUtility
{
    public static void AddRedis(IServiceCollection services, IReadOnlyCollection<(string Name, string Value)> connectionStrings)
    {
        var connectionString = connectionStrings.First(x => x.Name == "Redis").Value;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
        services.AddSingleton<IDistributedLockProvider>(serviceProvider =>
            new RedisDistributedSynchronizationProvider(serviceProvider.GetRequiredService<IDatabase>()));
    }
}
