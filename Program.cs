
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
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Tutor.Api.Data;
using Tutor.Api.Middlewares;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Services;

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
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.FindFirstValue(ClaimTypes.Email) ??
                                      throw new Exception("User is not authenticated."),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 20,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));
    
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
            });

            builder.Services.AddAuthorization();

            builder.Services.AddIdentityCore<User>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<TutorContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddControllers();
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
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);
            builder.Services.AddScoped<IEmailSender<User>, EmailSender>();
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
