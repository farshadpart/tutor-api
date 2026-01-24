
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
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
                        ?? throw new InvalidOperationException("Connection string 'TutorContext' not found.")
                )
            );

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
            builder.Services.AddScoped<PrerequisitesService>();
            builder.Services.AddScoped<ChatGptAudioService>();
            builder.Services.AddScoped<ChatGptChatService>();
            builder.Services.AddScoped<SubscriptionService>();
            builder.Services.Configure<AppSettings>(builder.Configuration);
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);
            builder.Services.AddScoped<IEmailSender<User>, EmailSender>();
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
            app.UseAuthorization();

            app.MapControllers();

            Log.Logger.Information("Starting Tutor.Api!");
            app.Run();
        }
    }
}
