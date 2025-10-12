
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Services;

namespace Tutor.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddDbContext<TutorContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("TutorContext") 
                        ?? throw new InvalidOperationException("Connection string 'TutorContext' not found.")
                )
            );
            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddSingleton<ChatGptAudioService>();
            builder.Services.AddSingleton<ChatGptChatService>();
            builder.Services.Configure<AppSettings>(builder.Configuration);
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
