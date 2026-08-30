using Tutor.Api.Middlewares;

namespace Tutor.Api.Utilities;

public static class MiddlewareUtility
{
    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseExceptionHandlingMiddleware();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
        {
            app.UseHttpsRedirection();
        }
        
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
    }
}
