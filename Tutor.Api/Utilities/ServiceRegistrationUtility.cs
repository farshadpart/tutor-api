using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tutor.Api.Filters;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Services;
using Tutor.Api.Services.Interfaces;
using Tutor.Api.Validators;

namespace Tutor.Api.Utilities;

public static class ServiceRegistrationUtility
{
    public static void AddApplicationServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllersWithViews();
        services.AddOpenApi();
        services.AddScoped<ControllerExecutionTimingFilter>();
        services.AddScoped<AccountService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<UserSettingsService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<PrerequisitesService>();
        services.AddScoped<ChatGptAudioService>();
        services.AddScoped<IChatGptAudioClient, ChatGptAudioClient>();
        services.AddScoped<IChatGptChatClient, ChatGptChatClient>();
        services.AddScoped<ChatGptChatService>();
        services.AddScoped<TutorChatService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddSingleton<SubscriptionAssertionService>();

        AddAppSettings(services, configuration);
        AddEmailSender(services);
    }

    private static void AddAppSettings(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration);
        services.AddSingleton(serviceProvider =>
        {
            var appSettings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
#if !DEBUG
            appSettings.MailConfiguration.MailJet.MailCredentials = appSettings.GetMailJetCredentials();
#endif
            return appSettings;
        });
    }

    private static void AddEmailSender(IServiceCollection services)
    {
#if DEBUG
        services.AddScoped<IEmailSender<User>, SmtpEmailSender>();
#else
        services.AddHttpClient<IEmailSender<User>, EmailSender>();
#endif
    }
}
