using Microsoft.AspNetCore.Identity;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;

namespace Tutor.Api.Services
{
    public class EmailSender(HttpClient httpClient, AppSettings appSettings, ILogger<EmailSender> logger) : IEmailSender<User>
    {
        private readonly MailConfiguration _mailConfiguration = appSettings.MailConfiguration;
        private readonly MailJet _mailJetConfig = appSettings.MailConfiguration.MailJet;

        public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
        {
            await SendEmailAsync(
                email,
                "Confirm your Tutor account",
                EmailViews.BuildConfirmationLinkBody(confirmationLink));
        }

        public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            await SendEmailAsync(
                email,
                "Your Tutor password reset code",
                EmailViews.BuildPasswordResetCodeBody(resetCode));
        }

        public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            await SendEmailAsync(
                email,
                "Reset your Tutor password",
                EmailViews.BuildPasswordResetLinkBody(resetLink));
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _mailJetConfig.MailJetSendEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_mailJetConfig.MailCredentials.ApiKey}:{_mailJetConfig.MailCredentials.ApiSecret}")));
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    Messages = new[]
                    {
                        new
                        {
                            From = new
                            {
                                Email = _mailConfiguration.FromEmail,
                                Name = _mailConfiguration.FromName
                            },
                            To = new[]
                            {
                                new
                                {
                                    Email = toEmail
                                }
                            },
                            Subject = subject,
                            HTMLPart = htmlBody
                        }
                    }
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "MailJet failed to send email to {Email}. StatusCode: {StatusCode}. Response: {ResponseBody}",
                toEmail,
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException("MailJet failed to send the email.");
        }
    }
}
