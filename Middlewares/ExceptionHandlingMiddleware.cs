namespace Tutor.Api.Middlewares
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Tutor.Api.Models.Exceptions;

    public class ExceptionHandlingMiddleware(RequestDelegate Next, ILogger<ExceptionHandlingMiddleware> Logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await Next(context);
            }
            catch (TutorException ex)
            {
                Logger.LogWarning(ex, "TutorException: {exceptionMessage}", ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await Next(context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unhandled exception");

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    Message = Errors.SOMETHING_WENT_WRONG
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
