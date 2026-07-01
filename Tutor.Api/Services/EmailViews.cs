using System.Net;

namespace Tutor.Api.Services
{
    public static class EmailViews
    {
        public static string BuildConfirmationLinkBody(string confirmationLink)
        {
            return BuildActionEmailBody(
                "Email confirmation",
                "Confirm your email address",
                "Thank you for creating a Tutor account. Please confirm your email address to complete your registration and start using your account.",
                "Confirm email address",
                WebUtility.HtmlEncode(confirmationLink),
                "If you did not create a Tutor account, you can safely ignore this email.");
        }

        public static string BuildPasswordResetCodeBody(string resetCode)
        {
            return BuildResetCodeEmailBody(
                "Reset your password",
                "We received a request to reset the password for your Tutor account. Paste the secure token below into the reset password screen to continue.",
                WebUtility.HtmlEncode(resetCode),
                "This token should be kept private. If you did not request a password reset, you can safely ignore this email.");
        }

        public static string BuildPasswordResetLinkBody(string resetLink)
        {
            return BuildActionEmailBody(
                "Password reset",
                "Reset your password",
                "We received a request to reset the password for your Tutor account. Use the secure link below to choose a new password.",
                "Reset password",
                WebUtility.HtmlEncode(resetLink),
                "If you did not request a password reset, you can safely ignore this email.");
        }

        private static string BuildActionEmailBody(string eyebrow, string title, string message, string actionText, string actionUrl, string footer)
        {
            return $"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width,initial-scale=1">
                    <meta name="color-scheme" content="light">
                    <title>{title}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#eef3f0;font-family:Verdana,Geneva,sans-serif;color:#17221c;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef3f0;background-image:linear-gradient(135deg,#eef3f0 0%,#f8f6eb 100%);padding:28px 12px;">
                        <tr>
                            <td align="center">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;">
                                    <tr>
                                        <td style="padding:0 0 14px 0;">
                                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                                <tr>
                                                    <td align="left" style="font-size:18px;line-height:24px;font-weight:800;letter-spacing:-0.3px;color:#17221c;">
                                                        <span style="display:inline-block;width:34px;height:34px;border-radius:12px;background-color:#1f6f52;color:#ffffff;text-align:center;line-height:34px;margin-right:8px;">T</span>
                                                        Tutor
                                                    </td>
                                                    <td align="right" style="font-size:12px;line-height:18px;color:#6b766f;">Account security</td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="background-color:#ffffff;border:1px solid #dce6df;border-radius:22px;box-shadow:0 18px 40px rgba(31,76,58,0.10);overflow:hidden;">
                                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                                <tr>
                                                    <td style="height:8px;background-color:#1f6f52;background-image:linear-gradient(90deg,#1f6f52,#e2b84f);font-size:0;line-height:0;">&nbsp;</td>
                                                </tr>
                                                <tr>
                                                    <td style="padding:34px 30px 30px;">
                                                        <p style="margin:0 0 12px;font-size:12px;line-height:18px;font-weight:800;letter-spacing:1.6px;text-transform:uppercase;color:#1f6f52;">{eyebrow}</p>
                                                        <h1 style="margin:0 0 14px;font-size:30px;line-height:38px;letter-spacing:-0.8px;color:#17221c;">{title}</h1>
                                                        <p style="margin:0 0 26px;font-size:16px;line-height:26px;color:#4e5b53;">{message}</p>
                                                        <p style="margin:0 0 26px;">
                                                            <a href="{actionUrl}" style="display:inline-block;background-color:#1f6f52;color:#ffffff;text-decoration:none;border-radius:999px;padding:15px 24px;font-size:16px;line-height:20px;font-weight:800;box-shadow:0 10px 22px rgba(31,111,82,0.25);">{actionText}</a>
                                                        </p>
                                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f7faf7;border:1px solid #e1e9e3;border-radius:14px;">
                                                            <tr>
                                                                <td style="padding:16px 18px;">
                                                                    <p style="margin:0 0 8px;font-size:13px;line-height:20px;font-weight:700;color:#17221c;">Button not working?</p>
                                                                    <p style="margin:0;font-size:13px;line-height:20px;color:#5d6a61;">Copy and paste this secure link into your browser:</p>
                                                                    <p style="margin:10px 0 0;font-size:13px;line-height:20px;overflow-wrap:anywhere;word-break:break-all;color:#1f6f52;">{actionUrl}</p>
                                                                </td>
                                                            </tr>
                                                        </table>
                                                        <p style="margin:22px 0 0;font-size:13px;line-height:21px;color:#6b766f;">{footer}</p>
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align="center" style="padding:18px 24px 0;font-size:12px;line-height:18px;color:#839087;">
                                            This message was sent by Tutor. Please do not reply to this email.
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

        private static string BuildResetCodeEmailBody(string title, string message, string code, string footer)
        {
            return $"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width,initial-scale=1">
                    <meta name="color-scheme" content="light">
                    <title>{title}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#eef3f0;font-family:Verdana,Geneva,sans-serif;color:#17221c;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef3f0;background-image:linear-gradient(135deg,#eef3f0 0%,#f8f6eb 100%);padding:28px 12px;">
                        <tr>
                            <td align="center">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;">
                                    <tr>
                                        <td style="padding:0 0 14px 0;">
                                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                                <tr>
                                                    <td align="left" style="font-size:18px;line-height:24px;font-weight:800;letter-spacing:-0.3px;color:#17221c;">
                                                        <span style="display:inline-block;width:34px;height:34px;border-radius:12px;background-color:#1f6f52;color:#ffffff;text-align:center;line-height:34px;margin-right:8px;">T</span>
                                                        Tutor
                                                    </td>
                                                    <td align="right" style="font-size:12px;line-height:18px;color:#6b766f;">Account security</td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="background-color:#ffffff;border:1px solid #dce6df;border-radius:22px;box-shadow:0 18px 40px rgba(31,76,58,0.10);overflow:hidden;">
                                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                                <tr>
                                                    <td style="height:8px;background-color:#1f6f52;background-image:linear-gradient(90deg,#1f6f52,#e2b84f);font-size:0;line-height:0;">&nbsp;</td>
                                                </tr>
                                                <tr>
                                                    <td style="padding:34px 30px 30px;">
                                                        <p style="margin:0 0 12px;font-size:12px;line-height:18px;font-weight:800;letter-spacing:1.6px;text-transform:uppercase;color:#1f6f52;">Password reset</p>
                                                        <h1 style="margin:0 0 14px;font-size:30px;line-height:38px;letter-spacing:-0.8px;color:#17221c;">{title}</h1>
                                                        <p style="margin:0 0 24px;font-size:16px;line-height:26px;color:#4e5b53;">{message}</p>
                                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f7faf7;border:1px solid #dbe8df;border-radius:16px;">
                                                            <tr>
                                                                <td style="padding:18px;">
                                                                    <p style="margin:0 0 10px;font-size:13px;line-height:20px;font-weight:800;letter-spacing:0.8px;text-transform:uppercase;color:#6b766f;">Reset token</p>
                                                                    <p style="margin:0;padding:16px;background-color:#ffffff;border:1px dashed #b9cbbf;border-radius:12px;text-align:left;font-family:'Courier New',Courier,monospace;font-size:15px;line-height:23px;letter-spacing:0.6px;font-weight:700;overflow-wrap:anywhere;word-break:break-all;color:#17221c;">{code}</p>
                                                                    <p style="margin:12px 0 0;font-size:13px;line-height:20px;color:#5d6a61;">Paste this token into the reset password screen to continue.</p>
                                                                </td>
                                                            </tr>
                                                        </table>
                                                        <p style="margin:22px 0 0;font-size:13px;line-height:21px;color:#6b766f;">{footer}</p>
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align="center" style="padding:18px 24px 0;font-size:12px;line-height:18px;color:#839087;">
                                            This message was sent by Tutor. Please do not reply to this email.
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
