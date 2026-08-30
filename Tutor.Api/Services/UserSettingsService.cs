using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;
using Tutor.Api.Utilities;

namespace Tutor.Api.Services;

public class UserSettingsService(
    TutorContext context,
    AppSettings appSettings,
    ILogger<UserSettingsService> logger)
{
    public async Task<UserSettings> Get(string id)
    {
        return await context.UserSettings.FirstOrDefaultAsync(x => x.UserId == id) ?? new();
    }

    public async Task Update(string userId, RequestUpdateUserSettings requestUpdateUserSettings)
    {
        UserSettings? userSettings = await context.UserSettings
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (userSettings is null)
        {
            userSettings = new UserSettings { UserId = userId };
            context.UserSettings.Add(userSettings);
        }

        userSettings.AutoPlayVoice = requestUpdateUserSettings.AutoPlayVoice;
        await context.SaveChangesAsync();
    }

    public async Task<UserAvatarFile?> GetUserAvatarFile(string userId)
    {
        logger.LogDebug("Getting avatar file path for user {UserId}.", userId);
        var storedImage = await context.UserSettings
            .Where(x => x.UserId == userId)
            .Select(x => x.UserProfileImage)
            .FirstOrDefaultAsync();
        if (storedImage is null)
        {
            logger.LogDebug("User {UserId} does not have a user avatar.", userId);
            return null;
        }

        var avatarFilePath = Path.Combine(
            appSettings.StorageRootDirectory,
            "user-avatars",
            storedImage.FileName);
        logger.LogInformation("Avatar file path retrieved for user {UserId}.", userId);

        return new UserAvatarFile(avatarFilePath, ImageUtility.GetContentType(storedImage.Format));
    }

    public async Task UpdateUserAvatar(string userId, IFormFile uploadedImage)
    {
        var avatarDirectory = Path.Combine(appSettings.StorageRootDirectory, "user-avatars");
        var savedImage = await ImageUtility.ProcessAndSaveAvatar(uploadedImage, avatarDirectory, logger);
        var storedImage = new StoredImage
        {
            FileName = savedImage.FileName,
            Format = savedImage.Format
        };

        StoredImage? previousImage = null;
        try
        {
            var userSettings = await context.UserSettings
                .Include(x => x.UserProfileImage)
                .FirstAsync(x => x.UserId == userId);

            previousImage = userSettings.UserProfileImage;
            context.Images.Add(storedImage);
            userSettings.UserProfileImage = storedImage;
            if (previousImage is not null)
            {
                context.Images.Remove(previousImage);
            }

            await context.SaveChangesAsync();
        }
        catch
        {
            ImageUtility.DeleteImageFile(savedImage.FilePath, logger);
            throw;
        }

        if (previousImage is not null)
        {
            var previousImagePath = Path.Combine(appSettings.StorageRootDirectory, "user-avatars", previousImage.FileName);
            ImageUtility.DeleteImageFile(previousImagePath, logger);
        }
    }
}
