using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Tutor.Api.Tests.Utility;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Services;

namespace Tutor.Api.Tests.Services;

public class PrerequisitesServiceTests
{
    private const string GoogleUserId = "f1620f14-07df-4f6f-bf05-57587d3fefc7";
    private const string GoogleUserPassword = "F7!qR9@ZxM#2KpW$E8vH";

    [Fact]
    public async Task InsertInitialData_WhenGoogleUserAlreadyExists_DoesNotCreateOrUpdateUser()
    {
        var existingUser = new User { Id = GoogleUserId, UserName = "googleStoreUser" };
        var userManager = new TestUserManager(existingUser);
        var logger = new TestLogger<PrerequisitesService>();
        var service = new PrerequisitesService(userManager, logger);

        await service.InsertInitialData();

        Assert.Equal(1, userManager.FindByIdCallCount);
        Assert.Equal(0, userManager.CreateCallCount);
        Assert.Equal(0, userManager.UpdateCallCount);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task InsertInitialData_WhenGoogleUserIsMissing_CreatesPlayTestUserAndConfirmsEmail()
    {
        var userManager = new TestUserManager();
        var logger = new TestLogger<PrerequisitesService>();
        var service = new PrerequisitesService(userManager, logger);

        await service.InsertInitialData();

        Assert.Equal(2, userManager.FindByIdCallCount);
        Assert.Equal(1, userManager.CreateCallCount);
        Assert.Equal(1, userManager.UpdateCallCount);
        Assert.Equal(GoogleUserPassword, userManager.CreatedPassword);
        Assert.Empty(logger.Entries);

        var createdUser = Assert.IsType<User>(userManager.CreatedUser);
        Assert.Equal(GoogleUserId, createdUser.Id);
        Assert.Equal("googleStoreUser", createdUser.UserName);
        Assert.True(createdUser.EmailConfirmed);

        var subscription = Assert.Single(createdUser.Subscriptions);
        Assert.Equal(SubscriptionGroup.Basic, subscription.Group);
        Assert.NotEqual(default, subscription.CreatedAt);

        var cycle = Assert.Single(subscription.Cycles);
        Assert.Equal(CycleSizeHelper.GetDuration(CycleSize.PlayTest), cycle.Duration);
        Assert.Equal(CycleSizeHelper.GetValidRequestCount(CycleSize.PlayTest), cycle.ValidRequestCount);
        Assert.Equal(CycleStatus.Active, cycle.Status);
        Assert.NotEqual(default, cycle.CreatedAt);
        Assert.NotNull(cycle.StartedAt);
    }

    [Fact]
    public async Task InsertInitialData_WhenCreateFails_LogsErrorAndDoesNotConfirmEmail()
    {
        var userManager = new TestUserManager
        {
            CreateResult = IdentityResult.Failed(new IdentityError { Code = "CreateFailed" })
        };
        var logger = new TestLogger<PrerequisitesService>();
        var service = new PrerequisitesService(userManager, logger);

        await service.InsertInitialData();

        Assert.Equal(1, userManager.FindByIdCallCount);
        Assert.Equal(1, userManager.CreateCallCount);
        Assert.Equal(0, userManager.UpdateCallCount);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("Failed to create the googleStoreUser.", entry.Message);
    }

    [Fact]
    public async Task InsertInitialData_WhenCreatedUserCannotBeFound_LogsErrorAndDoesNotUpdate()
    {
        var userManager = new TestUserManager { ReturnCreatedUserFromFind = false };
        var logger = new TestLogger<PrerequisitesService>();
        var service = new PrerequisitesService(userManager, logger);

        await service.InsertInitialData();

        Assert.Equal(2, userManager.FindByIdCallCount);
        Assert.Equal(1, userManager.CreateCallCount);
        Assert.Equal(0, userManager.UpdateCallCount);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("Failed to find the googleStoreUser.", entry.Message);
    }

    [Fact]
    public async Task InsertInitialData_WhenEmailConfirmationUpdateFails_LogsError()
    {
        var userManager = new TestUserManager
        {
            UpdateResult = IdentityResult.Failed(new IdentityError { Code = "UpdateFailed" })
        };
        var logger = new TestLogger<PrerequisitesService>();
        var service = new PrerequisitesService(userManager, logger);

        await service.InsertInitialData();

        Assert.Equal(2, userManager.FindByIdCallCount);
        Assert.Equal(1, userManager.CreateCallCount);
        Assert.Equal(1, userManager.UpdateCallCount);
        Assert.True(userManager.UpdatedUser?.EmailConfirmed);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("Failed to enable EmailConfirmed in the googleStoreUser.", entry.Message);
    }
}
