using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;

namespace Tutor.Api.Utilities;

public static class DatabaseUtility
{
    public static void AddDatabase(IServiceCollection services, IReadOnlyCollection<(string Name, string Value)> connectionStrings)
    {
        var connectionString = connectionStrings.First(x => x.Name == "TutorContext").Value;
        services.AddDbContext<TutorContext>(options => options.UseNpgsql(connectionString));
    }
}
