using Microsoft.EntityFrameworkCore;
using Tutor.Api.Data;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Tutor.Api.Contracts.Account;

namespace Tutor.Api.Services;

public class UserSettingsService(TutorContext context)
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
}
