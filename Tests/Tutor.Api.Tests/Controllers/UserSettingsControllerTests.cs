using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;
using Tutor.Api.Controllers;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Constants;
using Tutor.Api.Services;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Controllers;

public class UserSettingsControllerTests
{
    private const int MaximumAvatarUploadSize = 1024 * 1024;

    [Fact]
    public async Task GetUserAvatar_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        await using var context = CreateContext();
        var logger = new TestLogger<UserSettingsController>();
        var sut = CreateController(context, logger);

        // Act
        var result = await sut.GetUserAvatar();

        // Assert
        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUserAvatar_WhenAvatarDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        await using var context = CreateContext();
        var logger = new TestLogger<UserSettingsController>();
        var sut = CreateController(context, logger, "requested-user");

        // Act
        var result = await sut.GetUserAvatar();

        // Assert
        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUserAvatar_WhenAvatarExists_ReturnsPhysicalFileWithStoredFormat()
    {
        // Arrange
        await using var context = CreateContext();
        context.UserSettings.Add(new UserSettings
        {
            UserId = "requested-user",
            UserProfileImage = new StoredImage { FileName = "avatar.png", Format = "PNG" }
        });
        await context.SaveChangesAsync();
        var logger = new TestLogger<UserSettingsController>();
        var sut = CreateController(context, logger, "requested-user", "/storage");

        // Act
        var result = await sut.GetUserAvatar();

        // Assert
        var fileResult = result.ShouldBeOfType<PhysicalFileResult>();
        fileResult.FileName.ShouldBe(Path.Combine("/storage", "user-avatars", "avatar.png"));
        fileResult.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task UpdateUserAvatar_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateController(context, new TestLogger<UserSettingsController>());
        await using var stream = new MemoryStream([1]);
        var upload = new FormFile(stream, 0, stream.Length, "image", "avatar.png");

        // Act
        var result = await sut.UpdateUserAvatar(upload);

        // Assert
        result.ShouldBeOfType<UnauthorizedResult>();
        context.Images.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateUserAvatar_WhenImageIsTooLarge_ReturnsBadRequest()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateController(context, new TestLogger<UserSettingsController>(), "requested-user");
        await using var stream = new MemoryStream(new byte[MaximumAvatarUploadSize]);
        var upload = new FormFile(stream, 0, stream.Length, "image", "avatar.png");

        // Act
        var result = await sut.UpdateUserAvatar(upload);

        // Assert
        result.ShouldBeOfType<BadRequestResult>();
        context.Images.ShouldBeEmpty();
    }

    private static UserSettingsController CreateController(
        TutorContext context,
        ILogger<UserSettingsController> logger,
        string? userId = null,
        string storageRootDirectory = "")
    {
        var service = new UserSettingsService(
            context,
            new AppSettings { StorageRootDirectory = storageRootDirectory },
            new TestLogger<UserSettingsService>());
        var controller = new UserSettingsController(service, logger);
        var claims = userId is null
            ? []
            : new[] { new Claim(TutorClaimTypes.Id, userId) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };

        return controller;
    }

    private static TutorContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TutorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TutorContext(options);
    }
}
