using Tutor.Api.Services;

namespace Tutor.Api.Utilities;

public static class ApplicationUtility
{
    public static void InsertInitialData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PrerequisitesService>();
        service.InsertInitialData().GetAwaiter().GetResult();
    }
}
