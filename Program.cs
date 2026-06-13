
using System.Globalization;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;
using Tutor.Api.Data;
using Tutor.Api.Middlewares;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Constants;
using Tutor.Api.Services;
using Tutor.Api.Utilities;

namespace Tutor.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            builder.Services.AddDbContext<TutorContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("TutorContext")
                        ?? throw new InvalidOperationException("Connection string: 'TutorContext' not found.")
                )
            );
            
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("Chat", httpContext =>
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetUserIdentifier(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 20,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                });
    
                options.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                    }

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
                };
            });

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters.ValidIssuer = builder.Configuration["Jwt:Issuer"];
                options.TokenValidationParameters.ValidAudience = builder.Configuration["Jwt:Audience"];
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!));
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        if (!string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                        {
                            logger.LogDebug(
                                "Bearer authentication header received for {Method} {Path} from IP {Ip}.",
                                context.Request.Method,
                                context.Request.Path,
                                context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        var userId = context.Principal?.FindFirst(TutorClaimTypes.Id)?.Value ?? "unknown";
                        logger.LogInformation(
                            "Bearer token validated for user {UserId} on {Method} {Path} from IP {Ip}.",
                            userId,
                            context.Request.Method,
                            context.Request.Path,
                            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning(
                            context.Exception,
                            "Bearer authentication failed on {Method} {Path} from IP {Ip}.",
                            context.Request.Method,
                            context.Request.Path,
                            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning(
                            "Bearer authentication challenge on {Method} {Path} from IP {Ip}. Error: {Error}; Description: {ErrorDescription}.",
                            context.Request.Method,
                            context.Request.Path,
                            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            context.Error,
                            context.ErrorDescription);

                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        var userId = context.Principal?.FindFirst(TutorClaimTypes.Id)?.Value ?? "unknown";
                        logger.LogWarning(
                            "Bearer authorization forbidden for user {UserId} on {Method} {Path} from IP {Ip}.",
                            userId,
                            context.Request.Method,
                            context.Request.Path,
                            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            builder.Services.AddIdentityCore<User>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<TutorContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddControllersWithViews();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddScoped<AccountService>();
            builder.Services.AddScoped<RefreshTokenService>();
            builder.Services.AddScoped<PrerequisitesService>();
            builder.Services.AddScoped<ChatGptAudioService>();
            builder.Services.AddScoped<ChatGptChatService>();
            builder.Services.AddScoped<SubscriptionService>();
            builder.Services.AddSingleton<SubscriptionAssertionService>();
            builder.Services.Configure<AppSettings>(builder.Configuration);
            builder.Services.AddSingleton(sp =>
            {
                var appSettings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
                appSettings.MailJet.MailCredentials = appSettings.GetMailJetCredentials();
                return appSettings;
            });
            builder.Services.AddHttpClient<IEmailSender<User>, EmailSender>();
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")
                        ?? throw new InvalidOperationException("Connection string: 'Redis' not found.")));
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
            builder.Services.AddSingleton<IDistributedLockProvider>(sp => new RedisDistributedSynchronizationProvider(sp.GetRequiredService<IDatabase>()));
            builder.Services.AddSerilog();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var prerequisitesService = scope.ServiceProvider.GetRequiredService<PrerequisitesService>();
                prerequisitesService.InsertInitialData().GetAwaiter().GetResult();
            }

            app.UseExceptionHandlingMiddleware();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapControllers();

            Log.Logger.Information("Starting Tutor.Api!");
            app.Run();
        }
    }
}
