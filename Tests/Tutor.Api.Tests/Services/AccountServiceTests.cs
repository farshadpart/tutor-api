using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
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

    private static AccountService CreateService(UserManager<User> userManager)
    {
        return new AccountService(
            new AppSettings(),
            userManager,
            null!,
            null!,
            new TestLogger<AccountService>());
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
}
