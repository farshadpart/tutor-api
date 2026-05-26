using Microsoft.AspNetCore.Identity;
using System.Net;
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
            var encodedConfirmationLink = WebUtility.HtmlEncode(confirmationLink);
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail),
                Subject = "Confirm your Tutor account",
                Body = BuildEmailBody(
                    "Confirm your email address",
                    "Thank you for creating a Tutor account. Please confirm your email address to complete your registration and start using your account.",
                    "Confirm email address",
                    encodedConfirmationLink,
                    "If you did not create a Tutor account, you can safely ignore this email."),
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
        }

        public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            var encodedResetCode = WebUtility.HtmlEncode(resetCode);
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail),
                Subject = "Your Tutor password reset code",
                Body = BuildEmailBody(
                    "Password reset code",
                    "We received a request to reset the password for your Tutor account. Use the verification code below to continue.",
                    encodedResetCode,
                    "This code should be kept private. If you did not request a password reset, you can safely ignore this email."),
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
        }

        public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            var encodedResetLink = WebUtility.HtmlEncode(resetLink);
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail),
                Subject = "Reset your Tutor password",
                Body = BuildEmailBody(
                    "Reset your password",
                    "We received a request to reset the password for your Tutor account. Use the secure link below to choose a new password.",
                    "Reset password",
                    encodedResetLink,
                    "If you did not request a password reset, you can safely ignore this email."),
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
        }

        private static string BuildEmailBody(string title, string message, string actionText, string actionUrl, string footer)
        {
            return $"""
                <!doctype html>
                <html lang="en">
                <body style="margin:0;padding:0;background-color:#f6f7f9;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f6f7f9;padding:24px 0;">
                        <tr>
                            <td align="center">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:8px;">
                                    <tr>
                                        <td style="padding:32px;">
                                            <h1 style="margin:0 0 16px;font-size:24px;line-height:32px;color:#111827;">{title}</h1>
                                            <p style="margin:0 0 24px;font-size:16px;line-height:24px;color:#374151;">{message}</p>
                                            <p style="margin:0 0 24px;">
                                                <a href="{actionUrl}" style="display:inline-block;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;padding:12px 18px;font-size:16px;font-weight:700;">{actionText}</a>
                                            </p>
                                            <p style="margin:0 0 16px;font-size:14px;line-height:22px;color:#4b5563;">If the button does not work, copy and paste this link into your browser:</p>
                                            <p style="margin:0 0 24px;font-size:14px;line-height:22px;word-break:break-all;color:#2563eb;">{actionUrl}</p>
                                            <p style="margin:0;font-size:14px;line-height:22px;color:#6b7280;">{footer}</p>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """;
        }

        private static string BuildEmailBody(string title, string message, string code, string footer)
        {
            return $"""
                <!doctype html>
                <html lang="en">
                <body style="margin:0;padding:0;background-color:#f6f7f9;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f6f7f9;padding:24px 0;">
                        <tr>
                            <td align="center">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:8px;">
                                    <tr>
                                        <td style="padding:32px;">
                                            <h1 style="margin:0 0 16px;font-size:24px;line-height:32px;color:#111827;">{title}</h1>
                                            <p style="margin:0 0 24px;font-size:16px;line-height:24px;color:#374151;">{message}</p>
                                            <p style="margin:0 0 24px;padding:16px 20px;background-color:#f3f4f6;border-radius:6px;text-align:center;font-size:28px;letter-spacing:4px;font-weight:700;color:#111827;">{code}</p>
                                            <p style="margin:0;font-size:14px;line-height:22px;color:#6b7280;">{footer}</p>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """;
        }
    }
}
