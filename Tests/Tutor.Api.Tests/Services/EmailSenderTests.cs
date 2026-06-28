using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Services;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Services;

public class EmailSenderTests
{
    [Fact]
    public async Task SendConfirmationLinkAsync_SendsMailJetRequestWithExpectedPayload()
    {
        // Arrange
        const string confirmationLink = "https://tutor.test/confirm?user=user-1&token=<token>";
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var appSettings = CreateAppSettings();
        var sut = new EmailSender(httpClient, appSettings, new TestLogger<EmailSender>());
        var user = new User { Id = "user-1", Email = "student@example.com", UserName = "student" };

        // Act
        await sut.SendConfirmationLinkAsync(user, user.Email, confirmationLink);

        // Assert
        handler.Request.ShouldNotBeNull();
        handler.Request.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri.ShouldBe(new Uri(appSettings.MailJet.MailJetSendEndpoint));
        handler.Request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Basic", Convert.ToBase64String("api-key:api-secret"u8.ToArray())));

        var payload = JsonDocument.Parse(handler.Content);
        var message = payload.RootElement
            .GetProperty("Messages")
            .EnumerateArray()
            .ShouldHaveSingleItem();

        message.GetProperty("From").GetProperty("Email").GetString().ShouldBe("noreply@tutor.test");
        message.GetProperty("From").GetProperty("Name").GetString().ShouldBe("Tutor Tests");
        message.GetProperty("To").EnumerateArray().ShouldHaveSingleItem().GetProperty("Email").GetString().ShouldBe(user.Email);
        message.GetProperty("Subject").GetString().ShouldBe("Confirm your Tutor account");

        var htmlBody = message.GetProperty("HTMLPart").GetString();
        htmlBody.ShouldNotBeNull();
        htmlBody.ShouldContain("Email confirmation");
        htmlBody.ShouldContain("Confirm email address");
        htmlBody.ShouldContain(WebUtility.HtmlEncode(confirmationLink));
        htmlBody.ShouldNotContain(confirmationLink);
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_SendsMailJetRequestWithExpectedPayload()
    {
        // Arrange
        const string resetCode = "reset<token>&value";
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var appSettings = CreateAppSettings();
        var sut = new EmailSender(httpClient, appSettings, new TestLogger<EmailSender>());
        var user = new User { Id = "user-1", Email = "student@example.com", UserName = "student" };

        // Act
        await sut.SendPasswordResetCodeAsync(user, user.Email, resetCode);

        // Assert
        handler.Request.ShouldNotBeNull();
        handler.Request.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri.ShouldBe(new Uri(appSettings.MailJet.MailJetSendEndpoint));
        handler.Request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Basic", Convert.ToBase64String("api-key:api-secret"u8.ToArray())));

        var payload = JsonDocument.Parse(handler.Content);
        var message = payload.RootElement
            .GetProperty("Messages")
            .EnumerateArray()
            .ShouldHaveSingleItem();

        message.GetProperty("From").GetProperty("Email").GetString().ShouldBe("noreply@tutor.test");
        message.GetProperty("From").GetProperty("Name").GetString().ShouldBe("Tutor Tests");
        message.GetProperty("To").EnumerateArray().ShouldHaveSingleItem().GetProperty("Email").GetString().ShouldBe(user.Email);
        message.GetProperty("Subject").GetString().ShouldBe("Your Tutor password reset code");

        var htmlBody = message.GetProperty("HTMLPart").GetString();
        htmlBody.ShouldNotBeNull();
        htmlBody.ShouldContain("Password reset");
        htmlBody.ShouldContain("Reset token");
        htmlBody.ShouldContain(WebUtility.HtmlEncode(resetCode));
        htmlBody.ShouldNotContain(resetCode);
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_WhenMailJetFails_LogsAndThrows()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"ErrorMessage":"invalid recipient"}""")
        });
        var httpClient = new HttpClient(handler);
        var appSettings = CreateAppSettings();
        var logger = new TestLogger<EmailSender>();
        var sut = new EmailSender(httpClient, appSettings, logger);
        var user = new User { Id = "user-1", Email = "student@example.com", UserName = "student" };

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.SendPasswordResetCodeAsync(user, user.Email, "reset-token"));

        // Assert
        exception.Message.ShouldBe("MailJet failed to send the email.");
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("MailJet failed to send email to student@example.com");
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_SendsMailJetRequestWithExpectedPayload()
    {
        // Arrange
        const string resetLink = "https://tutor.test/reset-password?user=user-1&token=<reset-token>";
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var appSettings = CreateAppSettings();
        var sut = new EmailSender(httpClient, appSettings, new TestLogger<EmailSender>());
        var user = new User { Id = "user-1", Email = "student@example.com", UserName = "student" };

        // Act
        await sut.SendPasswordResetLinkAsync(user, user.Email, resetLink);

        // Assert
        handler.Request.ShouldNotBeNull();
        handler.Request.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri.ShouldBe(new Uri(appSettings.MailJet.MailJetSendEndpoint));
        handler.Request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Basic", Convert.ToBase64String("api-key:api-secret"u8.ToArray())));

        var payload = JsonDocument.Parse(handler.Content);
        var message = payload.RootElement
            .GetProperty("Messages")
            .EnumerateArray()
            .ShouldHaveSingleItem();

        message.GetProperty("From").GetProperty("Email").GetString().ShouldBe("noreply@tutor.test");
        message.GetProperty("From").GetProperty("Name").GetString().ShouldBe("Tutor Tests");
        message.GetProperty("To").EnumerateArray().ShouldHaveSingleItem().GetProperty("Email").GetString().ShouldBe(user.Email);
        message.GetProperty("Subject").GetString().ShouldBe("Reset your Tutor password");

        var htmlBody = message.GetProperty("HTMLPart").GetString();
        htmlBody.ShouldNotBeNull();
        htmlBody.ShouldContain("Password reset");
        htmlBody.ShouldContain("Reset your password");
        htmlBody.ShouldContain("Reset password");
        htmlBody.ShouldContain(WebUtility.HtmlEncode(resetLink));
        htmlBody.ShouldNotContain(resetLink);
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_WhenMailJetFails_LogsAndThrows()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"ErrorMessage":"invalid recipient"}""")
        });
        var httpClient = new HttpClient(handler);
        var appSettings = CreateAppSettings();
        var logger = new TestLogger<EmailSender>();
        var sut = new EmailSender(httpClient, appSettings, logger);
        var user = new User { Id = "user-1", Email = "student@example.com", UserName = "student" };

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.SendPasswordResetLinkAsync(user, user.Email, "https://tutor.test/reset-password"));

        // Assert
        exception.Message.ShouldBe("MailJet failed to send the email.");
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("MailJet failed to send email to student@example.com");
    }

    private static AppSettings CreateAppSettings()
    {
        return new AppSettings
        {
            MailJet = new MailJet
            {
                MailJetSendEndpoint = "https://api.mailjet.test/v3.1/send",
                MailCredentials = new MailJetCredentials
                {
                    ApiKey = "api-key",
                    ApiSecret = "api-secret"
                },
                MailConfiguration = new MailConfiguration
                {
                    FromEmail = "noreply@tutor.test",
                    FromName = "Tutor Tests"
                }
            }
        };
    }

    private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Content { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }
}
