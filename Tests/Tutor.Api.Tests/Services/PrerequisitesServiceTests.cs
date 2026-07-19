using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Services;

namespace Tutor.Api.Tests.Services;

public class PrerequisitesServiceTests : IDisposable
{
    private const string GoogleUserId = "f1620f14-07df-4f6f-bf05-57587d3fefc7";
    private const string GoogleUserPassword = "GoogleUserPasswordSetInSecrets";

    [Fact]
    public async Task InsertInitialData_WhenGoogleUserAlreadyExists_DoesNotCreateOrUpdateUser()
    {
        // Arrange
        SetGoogleUserPassword(GoogleUserPassword);
        var existingUser = new User { Id = GoogleUserId, UserName = "googleStoreUser" };
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger);

        userManager.FindByIdAsync(GoogleUserId).Returns(existingUser);

        // Act
        await sut.InsertInitialData();

        // Assert
        await userManager.Received(1).FindByIdAsync(GoogleUserId);
        await userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
        AssertNoLogs(logger);
    }

    [Fact]
    public async Task InsertInitialData_WhenGoogleUserIsMissing_CreatesPlayTestUserAndConfirmsEmail()
    {
        // Arrange
        SetGoogleUserPassword(GoogleUserPassword);
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger);
        User? createdUser = null;
        string? createdPassword = null;
        User? updatedUser = null;
        var findByIdCallCount = 0;

        userManager
            .FindByIdAsync(GoogleUserId)
            .Returns(_ => Task.FromResult(++findByIdCallCount == 1 ? null : createdUser));
        userManager
            .CreateAsync(Arg.Do<User>(user => createdUser = user), Arg.Do<string>(password => createdPassword = password))
            .Returns(Task.FromResult(IdentityResult.Success));
        userManager
            .UpdateAsync(Arg.Do<User>(user => updatedUser = user))
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        await sut.InsertInitialData();

        // Assert
        await userManager.Received(2).FindByIdAsync(GoogleUserId);
        await userManager.Received(1).CreateAsync(Arg.Any<User>(), GoogleUserPassword);
        await userManager.Received(1).UpdateAsync(Arg.Any<User>());
        createdPassword.ShouldBe(GoogleUserPassword);
        updatedUser.ShouldBeSameAs(createdUser);
        AssertNoLogs(logger);

        createdUser.ShouldBeOfType<User>();
        createdUser.Id.ShouldBe(GoogleUserId);
        createdUser.UserName.ShouldBe("googleStoreUser");
        createdUser.EmailConfirmed.ShouldBeTrue();

        var subscription = createdUser.Subscriptions.ShouldHaveSingleItem();
        subscription.Group.ShouldBe(SubscriptionGroup.Basic);
        subscription.CreatedAt.ShouldNotBe(default);

        var cycle = subscription.Cycles.ShouldHaveSingleItem();
        cycle.Duration.ShouldBe(CycleSizeHelper.GetDuration(CycleSize.PlayTest));
        cycle.ValidRequestCount.ShouldBe(CycleSizeHelper.GetValidRequestCount(CycleSize.PlayTest));
        cycle.Status.ShouldBe(CycleStatus.Active);
        cycle.CreatedAt.ShouldNotBe(default);
        cycle.StartedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task InsertInitialData_WhenCreateFails_LogsErrorAndDoesNotConfirmEmail()
    {
        // Arrange
        SetGoogleUserPassword(GoogleUserPassword);
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger);
        var createResult = IdentityResult.Failed(new IdentityError { Code = "CreateFailed" });

        userManager.FindByIdAsync(GoogleUserId).Returns(Task.FromResult<User?>(null));
        userManager.CreateAsync(Arg.Any<User>(), GoogleUserPassword).Returns(Task.FromResult(createResult));

        // Act
        await sut.InsertInitialData();

        // Assert
        await userManager.Received(1).FindByIdAsync(GoogleUserId);
        await userManager.Received(1).CreateAsync(Arg.Any<User>(), GoogleUserPassword);
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
        AssertSingleLog(logger, LogLevel.Error, "Failed to create the googleStoreUser.");
    }

    [Fact]
    public async Task InsertInitialData_WhenCreatedUserCannotBeFound_LogsErrorAndDoesNotUpdate()
    {
        // Arrange
        SetGoogleUserPassword(GoogleUserPassword);
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger);

        userManager.FindByIdAsync(GoogleUserId).Returns(Task.FromResult<User?>(null));
        userManager.CreateAsync(Arg.Any<User>(), GoogleUserPassword).Returns(Task.FromResult(IdentityResult.Success));

        // Act
        await sut.InsertInitialData();

        // Assert
        await userManager.Received(2).FindByIdAsync(GoogleUserId);
        await userManager.Received(1).CreateAsync(Arg.Any<User>(), GoogleUserPassword);
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
        AssertSingleLog(logger, LogLevel.Error, "Failed to find the googleStoreUser.");
    }

    [Fact]
    public async Task InsertInitialData_WhenEmailConfirmationUpdateFails_LogsError()
    {
        // Arrange
        SetGoogleUserPassword(GoogleUserPassword);
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger);
        User? createdUser = null;
        User? updatedUser = null;
        var updateResult = IdentityResult.Failed(new IdentityError { Code = "UpdateFailed" });
        var findByIdCallCount = 0;

        userManager
            .FindByIdAsync(GoogleUserId)
            .Returns(_ => Task.FromResult(++findByIdCallCount == 1 ? null : createdUser));
        userManager
            .CreateAsync(Arg.Do<User>(user => createdUser = user), GoogleUserPassword)
            .Returns(Task.FromResult(IdentityResult.Success));
        userManager
            .UpdateAsync(Arg.Do<User>(user => updatedUser = user))
            .Returns(Task.FromResult(updateResult));

        // Act
        await sut.InsertInitialData();

        // Assert
        await userManager.Received(2).FindByIdAsync(GoogleUserId);
        await userManager.Received(1).CreateAsync(Arg.Any<User>(), GoogleUserPassword);
        await userManager.Received(1).UpdateAsync(Arg.Any<User>());
        updatedUser?.EmailConfirmed.ShouldBeTrue();
        AssertSingleLog(logger, LogLevel.Error, "Failed to enable EmailConfirmed in the googleStoreUser.");
    }

    [Fact]
    public async Task InsertInitialData_WhenEnvironmentIsDevelopment_DoesNotCreateGoogleUser()
    {
        // Arrange
        SetGoogleUserPassword(null);
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger, Environments.Development);

        // Act
        await sut.InsertInitialData();

        // Assert
        await userManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
        await userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
        AssertNoLogs(logger);
    }

    [Fact]
    public async Task InsertInitialData_WhenGoogleUserPasswordEnvironmentVariableIsMissing_LogsCriticalAndThrows()
    {
        // Arrange
        SetGoogleUserPassword(null);
        var userManager = CreateUserManager();
        var logger = Substitute.For<ILogger<PrerequisitesService>>();
        var sut = CreateService(userManager, logger);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(sut.InsertInitialData);

        // Assert
        exception.Message.ShouldBe("Failed to find the GOOGLE_USER_PASSWORD environment variable.");
        await userManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
        await userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
        AssertSingleLog(logger, LogLevel.Critical, "Failed to find the GOOGLE_USER_PASSWORD environment variable.");
    }

    private static PrerequisitesService CreateService(
        UserManager<User> userManager,
        ILogger<PrerequisitesService> logger,
        string environmentName = "Production")
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new PrerequisitesService(userManager, logger, environment);
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

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GOOGLE_USER_PASSWORD", string.Empty);
    }

    private static void SetGoogleUserPassword(string? password)
    {
        Environment.SetEnvironmentVariable("GOOGLE_USER_PASSWORD", password);
    }

    private static void AssertNoLogs(ILogger<PrerequisitesService> logger)
    {
        logger.ReceivedCalls().ShouldNotContain(call => call.GetMethodInfo().Name == nameof(ILogger.Log));
    }

    private static void AssertSingleLog(ILogger<PrerequisitesService> logger, LogLevel level, string message)
    {
        var call = logger
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .ShouldHaveSingleItem();
        var arguments = call.GetArguments();
        var logMessage = arguments[2]?.ToString();

        arguments[0].ShouldBe(level);
        logMessage.ShouldNotBeNull();
        logMessage.ShouldContain(message);
    }
}
