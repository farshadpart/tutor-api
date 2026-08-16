using Microsoft.EntityFrameworkCore;
using Shouldly;
using Tutor.Api.Data;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services;

namespace Tutor.Api.Tests.Services;

public class UserSettingsServiceTests
{
    [Fact]
    public async Task Get_WhenSettingsExist_ReturnsSettingsForRequestedUser()
    {
        // Arrange
        await using var context = CreateContext();
        context.UserSettings.AddRange(
            new UserSettings { UserId = "requested-user", AutoPlayVoice = false },
            new UserSettings { UserId = "other-user", AutoPlayVoice = true });
        await context.SaveChangesAsync();
        var sut = new UserSettingsService(context);

        // Act
        var result = await sut.Get("requested-user");

        // Assert
        result.ShouldNotBeNull();
        result.UserId.ShouldBe("requested-user");
        result.AutoPlayVoice.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_WhenSettingsDoNotExist_ReturnsDefaultSettings()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = new UserSettingsService(context);

        // Act
        var result = await sut.Get("missing-user");

        // Assert
        result.ShouldNotBeNull();
        result.UserId.ShouldBeEmpty();
        result.AutoPlayVoice.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WhenSettingsDoNotExist_CreatesSettingsForRequestedUser()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = new UserSettingsService(context);

        // Act
        await sut.Update("requested-user", new RequestUpdateUserSettings { AutoPlayVoice = false });

        // Assert
        var settings = (await context.UserSettings.ToListAsync()).ShouldHaveSingleItem();
        settings.UserId.ShouldBe("requested-user");
        settings.AutoPlayVoice.ShouldBeFalse();
    }

    private static TutorContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TutorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TutorContext(options);
    }
}
