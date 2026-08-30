using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Shouldly;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Services;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Services;

public class UserSettingsServiceTests
{
    private const int MaximumAvatarUploadSize = 1024 * 1024;

    [Fact]
    public async Task Get_WhenSettingsExist_ReturnsSettingsForRequestedUser()
    {
        // Arrange
        await using var context = CreateContext();
        context.UserSettings.AddRange(
            new UserSettings { UserId = "requested-user", AutoPlayVoice = false },
            new UserSettings { UserId = "other-user", AutoPlayVoice = true });
        await context.SaveChangesAsync();
        var sut = CreateService(context);

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
        var sut = CreateService(context);

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
        var sut = CreateService(context);

        // Act
        await sut.Update("requested-user", new RequestUpdateUserSettings { AutoPlayVoice = false });

        // Assert
        var settings = (await context.UserSettings.ToListAsync()).ShouldHaveSingleItem();
        settings.UserId.ShouldBe("requested-user");
        settings.AutoPlayVoice.ShouldBeFalse();
    }

    [Fact]
    public async Task GetUserAvatarFile_WhenImageExists_ReturnsPathAndContentType()
    {
        // Arrange
        await using var context = CreateContext();
        context.UserSettings.Add(new UserSettings
        {
            UserId = "requested-user",
            UserProfileImage = new StoredImage { FileName = "avatar.png", Format = "PNG" }
        });
        await context.SaveChangesAsync();
        var sut = CreateService(context, storageRootDirectory: "/storage");

        // Act
        var result = await sut.GetUserAvatarFile("requested-user");

        // Assert
        result.ShouldNotBeNull();
        result.FilePath.ShouldBe(Path.Combine("/storage", "user-avatars", "avatar.png"));
        result.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task GetUserAvatarFile_WhenImageDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetUserAvatarFile("missing-user");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateUserAvatar_WhenImageIsValid_ResizesAndStoresImageMetadata()
    {
        // Arrange
        await using var context = CreateContext();
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            context.UserSettings.Add(new UserSettings { UserId = "requested-user" });
            await context.SaveChangesAsync();
            var sut = CreateService(context, storageRootDirectory: storageRoot);
            using var source = new Image<Rgba32>(800, 400);
            await using var stream = new MemoryStream();
            await source.SaveAsPngAsync(stream);
            stream.Position = 0;
            var upload = new FormFile(stream, 0, stream.Length, "image", "avatar.png");

            // Act
            await sut.UpdateUserAvatar("requested-user", upload);

            // Assert
            var settings = await context.UserSettings.Include(x => x.UserProfileImage).SingleAsync();
            settings.UserId.ShouldBe("requested-user");
            settings.UserProfileImage.ShouldNotBeNull();
            settings.UserProfileImage.Format.ShouldBe("PNG");
            var savedPath = Path.Combine(storageRoot, "user-avatars", settings.UserProfileImage.FileName);
            File.Exists(savedPath).ShouldBeTrue();
            using var savedImage = await Image.LoadAsync(savedPath);
            savedImage.Width.ShouldBe(512);
            savedImage.Height.ShouldBe(256);
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, true);
            }
        }
    }

    [Fact]
    public async Task UpdateUserAvatar_WhenImageIsOneMegabyte_RejectsUpload()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateService(context);
        await using var stream = new MemoryStream(new byte[MaximumAvatarUploadSize]);
        var upload = new FormFile(stream, 0, stream.Length, "image", "avatar.png");

        // Act
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => sut.UpdateUserAvatar("requested-user", upload));

        // Assert
        exception.Message.ShouldContain("less than 1 MB");
        context.Images.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateUserAvatar_WhenUserSettingsDoNotExist_ThrowsAndRemovesSavedFile()
    {
        // Arrange
        await using var context = CreateContext();
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var sut = CreateService(context, storageRootDirectory: storageRoot);
            using var source = new Image<Rgba32>(10, 10);
            await using var stream = new MemoryStream();
            await source.SaveAsPngAsync(stream);
            stream.Position = 0;
            var upload = new FormFile(stream, 0, stream.Length, "image", "avatar.png");

            // Act
            await Should.ThrowAsync<InvalidOperationException>(
                () => sut.UpdateUserAvatar("requested-user", upload));

            // Assert
            context.Images.ShouldBeEmpty();
            Directory.GetFiles(Path.Combine(storageRoot, "user-avatars")).ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, true);
            }
        }
    }

    private static UserSettingsService CreateService(
        TutorContext context,
        ILogger<UserSettingsService>? logger = null,
        string storageRootDirectory = "")
    {
        return new UserSettingsService(
            context,
            new AppSettings { StorageRootDirectory = storageRootDirectory },
            logger ?? new TestLogger<UserSettingsService>());
    }

    private static TutorContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TutorContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TutorContext(options);
    }
}
