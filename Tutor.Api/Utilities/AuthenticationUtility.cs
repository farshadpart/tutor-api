using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Tutor.Api.Models.Constants;

namespace Tutor.Api.Utilities;

public static class AuthenticationUtility
{
    public static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters.ValidIssuer = configuration["Jwt:Issuer"];
                options.TokenValidationParameters.ValidAudience = configuration["Jwt:Audience"];
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(EnvironmentUtility.GetJwtSecretKey()));
                options.Events = CreateJwtEvents();
            });

        services.AddAuthorization();
    }

    private static JwtBearerEvents CreateJwtEvents()
    {
        return new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var logger = GetLogger(context.HttpContext);
                if (!string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                {
                    logger.LogDebug(
                        "Bearer authentication header received for {Method} {Path} from IP {Ip}.",
                        context.Request.Method, context.Request.Path, GetRemoteIp(context.HttpContext));
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                GetLogger(context.HttpContext).LogInformation(
                    "Bearer token validated for user {UserId} on {Method} {Path} from IP {Ip}.",
                    context.Principal?.FindFirst(TutorClaimTypes.Id)?.Value ?? "unknown",
                    context.Request.Method, context.Request.Path, GetRemoteIp(context.HttpContext));
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                GetLogger(context.HttpContext).LogWarning(
                    context.Exception,
                    "Bearer authentication failed on {Method} {Path} from IP {Ip}.",
                    context.Request.Method, context.Request.Path, GetRemoteIp(context.HttpContext));
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                GetLogger(context.HttpContext).LogWarning(
                    "Bearer authentication challenge on {Method} {Path} from IP {Ip}. Error: {Error}; Description: {ErrorDescription}.",
                    context.Request.Method, context.Request.Path, GetRemoteIp(context.HttpContext),
                    context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                GetLogger(context.HttpContext).LogWarning(
                    "Bearer authorization forbidden for user {UserId} on {Method} {Path} from IP {Ip}.",
                    context.Principal?.FindFirst(TutorClaimTypes.Id)?.Value ?? "unknown",
                    context.Request.Method, context.Request.Path, GetRemoteIp(context.HttpContext));
                return Task.CompletedTask;
            }
        };

        static ILogger<Program> GetLogger(HttpContext context) => context.RequestServices.GetRequiredService<ILogger<Program>>();
    }

    private static string GetRemoteIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
