using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Net.Mail;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;

namespace Tutor.Api.Services
{
    public class SmtpEmailSender(AppSettings appSettings, ILogger<SmtpEmailSender> logger) : IEmailSender<User>
    {
        private readonly MailConfiguration _mailConfiguration = appSettings.MailConfiguration;
        private readonly SmtpConfiguration _smtpConfiguration = appSettings.MailConfiguration.SmtpConfiguration;

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
            using var message = new MailMessage();
            message.From = new MailAddress(_mailConfiguration.FromEmail, _mailConfiguration.FromName);
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;
            message.To.Add(toEmail);

            using var smtpClient = new SmtpClient(_smtpConfiguration.Host, _smtpConfiguration.Port);
            smtpClient.EnableSsl = _smtpConfiguration.EnableSsl;
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.UseDefaultCredentials = false;

            if (!string.IsNullOrWhiteSpace(_smtpConfiguration.UserName) && !string.IsNullOrWhiteSpace(_smtpConfiguration.Password))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpConfiguration.UserName, _smtpConfiguration.Password);
            }

            try
            {
                await smtpClient.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMTP failed to send email to {Email}.", toEmail);
                throw new InvalidOperationException("SMTP failed to send the email.", ex);
            }
        }
    }
}
