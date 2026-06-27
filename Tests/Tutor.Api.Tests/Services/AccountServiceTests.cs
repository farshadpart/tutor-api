using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using System.Text;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Services;

public class AccountServiceTests
{
    [Fact]
    public async Task Register_WhenEmailIsInvalid_ReturnsBadRequestAndDoesNotCreateUser()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestCreateUser("not-an-email", "Password1!");

        // Act
        var result = await sut.Register(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("The entered email address is not valid!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
        await userManager.DidNotReceive().GenerateEmailConfirmationTokenAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Register_WhenCreateUserFails_ReturnsBadRequestAndDoesNotGenerateConfirmationToken()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestCreateUser("student@example.com", "Password1!");

        userManager
            .CreateAsync(Arg.Any<User>(), request.Password)
            .Returns(Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail" })));

        // Act
        var result = await sut.Register(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Failed to create the user!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.Received(1).CreateAsync(
            Arg.Is<User>(user =>
                user.Email == request.Email &&
                user.UserName == request.Email &&
                user.NormalizedEmail == request.Email.ToUpper() &&
                user.NormalizedUserName == request.Email.ToUpper()),
            request.Password);
        await userManager.DidNotReceive().GenerateEmailConfirmationTokenAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Register_WhenCreateUserSucceeds_ReturnsUserAndEmailConfirmationToken()
    {
        // Arrange
        const string token = "confirmation-token";
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestCreateUser("student@example.com", "Password1!");
        User? createdUser = null;

        userManager
            .CreateAsync(Arg.Do<User>(user => createdUser = user), request.Password)
            .Returns(Task.FromResult(IdentityResult.Success));
        userManager
            .GenerateEmailConfirmationTokenAsync(Arg.Any<User>())
            .Returns(Task.FromResult(token));

        // Act
        var result = await sut.Register(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Token.ShouldBe(token);
        result.Value.User.ShouldBeSameAs(createdUser);
        result.Value.User.Email.ShouldBe(request.Email);
        result.Value.User.UserName.ShouldBe(request.Email);
        result.Value.User.NormalizedEmail.ShouldBe(request.Email.ToUpper());
        result.Value.User.NormalizedUserName.ShouldBe(request.Email.ToUpper());
        createdUser.ShouldNotBeNull();
        await userManager.Received(1).CreateAsync(createdUser, request.Password);
        await userManager.Received(1).GenerateEmailConfirmationTokenAsync(createdUser!);
    }

    [Fact]
    public async Task Register_WhenConfirmationTokenIsEmpty_Throws()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestCreateUser("student@example.com", "Password1!");

        userManager
            .CreateAsync(Arg.Any<User>(), request.Password)
            .Returns(Task.FromResult(IdentityResult.Success));
        userManager
            .GenerateEmailConfirmationTokenAsync(Arg.Any<User>())
            .Returns(Task.FromResult(string.Empty));

        // Act
        var exception = await Assert.ThrowsAsync<Exception>(() => sut.Register(request));

        // Assert
        exception.Message.ShouldBe("Method GenerateEmailConfirmationTokenAsync failed to generate the confirmation token!");
    }

    [Fact]
    public async Task GeneratePasswordResetToken_WhenEmailIsInvalid_ReturnsBadRequestAndDoesNotLookupUser()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestForgotPassword("not-an-email");

        // Act
        var result = await sut.GeneratePasswordResetToken(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("The entered email address is not valid!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task GeneratePasswordResetToken_WhenUserDoesNotExist_ReturnsNotFoundAndDoesNotGenerateToken()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestForgotPassword("student@example.com");

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(null));

        // Act
        var result = await sut.GeneratePasswordResetToken(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Password reset target not found.");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("NotFound");
        await userManager.Received(1).FindByEmailAsync(request.Email);
        await userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task GeneratePasswordResetToken_WhenUserEmailIsNull_ReturnsNotFoundAndDoesNotGenerateToken()
    {
        // Arrange
        var user = new User { Id = "user-1", Email = null };
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestForgotPassword("student@example.com");

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(user));

        // Act
        var result = await sut.GeneratePasswordResetToken(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Password reset target not found.");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("NotFound");
        await userManager.Received(1).FindByEmailAsync(request.Email);
        await userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task GeneratePasswordResetToken_WhenGeneratedTokenIsEmpty_ReturnsInternalServerError()
    {
        // Arrange
        var user = new User { Id = "user-1", Email = "student@example.com" };
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestForgotPassword(user.Email);

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(user));
        userManager
            .GeneratePasswordResetTokenAsync(user)
            .Returns(Task.FromResult(string.Empty));

        // Act
        var result = await sut.GeneratePasswordResetToken(request);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Something went wrong!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("InternalServerError");
        await userManager.Received(1).GeneratePasswordResetTokenAsync(user);
    }

    [Fact]
    public async Task GeneratePasswordResetToken_WhenHappyPath_ReturnsUserAndEncodedToken()
    {
        // Arrange
        const string token = "reset-token/with+special=characters";
        var user = new User { Id = "user-1", Email = "student@example.com" };
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestForgotPassword(user.Email);

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(user));
        userManager
            .GeneratePasswordResetTokenAsync(user)
            .Returns(Task.FromResult(token));

        // Act
        var result = await sut.GeneratePasswordResetToken(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.User.ShouldBeSameAs(user);
        result.Value.Token.ShouldBe(EncodeResetToken(token));
        Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(result.Value.Token)).ShouldBe(token);
        await userManager.Received(1).FindByEmailAsync(request.Email);
        await userManager.Received(1).GeneratePasswordResetTokenAsync(user);
    }

    [Fact]
    public async Task ResetPassword_WhenEmailIsInvalid_ReturnsBadRequestAndDoesNotLookupUser()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestResetPassword("not-an-email", "token", "NewPassword1!");

        // Act
        var result = await sut.ResetPassword(request, "127.0.0.1");

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("The entered email address is not valid!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await userManager.DidNotReceive().ResetPasswordAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPassword_WhenUserDoesNotExist_ReturnsBadRequestAndDoesNotResetPassword()
    {
        // Arrange
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestResetPassword("student@example.com", EncodeResetToken("reset-token"), "NewPassword1!");

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(null));

        // Act
        var result = await sut.ResetPassword(request, "127.0.0.1");

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Invalid password!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.Received(1).FindByEmailAsync(request.Email);
        await userManager.DidNotReceive().ResetPasswordAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPassword_WhenTokenIsNotBase64Url_ReturnsBadRequestAndDoesNotResetPassword()
    {
        // Arrange
        var user = new User { Id = "user-1", Email = "student@example.com" };
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestResetPassword(user.Email, "not a valid base64url token", "NewPassword1!");

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(user));

        // Act
        var result = await sut.ResetPassword(request, "127.0.0.1");

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Invalid password reset request!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.Received(1).FindByEmailAsync(request.Email);
        await userManager.DidNotReceive().ResetPasswordAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPassword_WhenIdentityResetFails_ReturnsBadRequest()
    {
        // Arrange
        var user = new User { Id = "user-1", Email = "student@example.com" };
        var resetToken = "reset-token";
        var userManager = CreateUserManager();
        var sut = CreateService(userManager);
        var request = new RequestResetPassword(user.Email, EncodeResetToken(resetToken), "NewPassword1!");

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(user));
        userManager
            .ResetPasswordAsync(user, resetToken, request.NewPassword)
            .Returns(Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" })));

        // Act
        var result = await sut.ResetPassword(request, "127.0.0.1");

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Invalid password reset request!");
        result.Errors.Single().Metadata["MethodName"].ShouldBe("BadRequest");
        await userManager.Received(1).ResetPasswordAsync(user, resetToken, request.NewPassword);
    }

    [Fact]
    public async Task ResetPassword_WhenIdentityResetSucceeds_RevokesRefreshTokensAndReturnsSuccess()
    {
        // Arrange
        const string ip = "127.0.0.1";
        var user = new User { Id = "user-1", Email = "student@example.com" };
        var resetToken = "reset-token";
        var userManager = CreateUserManager();
        var refreshTokenService = CreateRefreshTokenService();
        var sut = CreateService(userManager, refreshTokenService);
        var request = new RequestResetPassword(user.Email, EncodeResetToken(resetToken), "NewPassword1!");

        userManager
            .FindByEmailAsync(request.Email)
            .Returns(Task.FromResult<User?>(user));
        userManager
            .ResetPasswordAsync(user, resetToken, request.NewPassword)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await sut.ResetPassword(request, ip);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await userManager.Received(1).ResetPasswordAsync(user, resetToken, request.NewPassword);
        await refreshTokenService.Received(1).RevokeAllUserRefreshTokensByUserId(user.Id, ip);
    }

    private static AccountService CreateService(UserManager<User> userManager, IRefreshTokenService? refreshTokenService = null)
    {
        return new AccountService(
            new AppSettings(),
            userManager,
            null!,
            refreshTokenService!,
            new TestLogger<AccountService>());
    }

    private static string EncodeResetToken(string token)
    {
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    private static UserManager<User> CreateUserManager()
    {
        return Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }

    private static IRefreshTokenService CreateRefreshTokenService()
    {
        var refreshTokenService = Substitute.For<IRefreshTokenService>();
        refreshTokenService
            .RevokeAllUserRefreshTokensByUserId(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        return refreshTokenService;
    }
}
