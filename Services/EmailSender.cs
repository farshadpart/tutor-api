using Microsoft.AspNetCore.Identity;
using System.Net.Mail;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;

namespace Tutor.Api.Services
{
    public class EmailSender : IEmailSender<User>
    {
        private readonly SmtpClient _smtpClient;
        private readonly Smtp _smtp;

        public EmailSender(AppSettings appSettings)
        {
            _smtp = appSettings.Smtp;
            _smtpClient = new SmtpClient(_smtp.Host, _smtp.Port);
        }

        public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
        {

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail),
                Subject = "Email Confirmation",
                Body = $"<p>Please confirm your email: {confirmationLink}</p>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
        }

        public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail),
                Subject = "Password Reset Code",
                Body = $"<p>Your password reset code is: <strong>{resetCode}</strong></p>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
        }

        public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail),
                Subject = "Password Reset Link",
                Body = $"<p>You can reset your password by clicking this link: <a href='{resetLink}'>Reset Password</a></p>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
        }
    }
}
