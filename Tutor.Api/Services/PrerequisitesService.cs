using Microsoft.AspNetCore.Identity;
using Tutor.Api.Models.Account;
using Tutor.Api.Models.Subscriptions;

namespace Tutor.Api.Services
{
    public class PrerequisitesService(UserManager<User> userManager, ILogger<PrerequisitesService> logger, IWebHostEnvironment environment)
    {
        public async Task InsertInitialData()
        {
            await InsertGoogleUser();
        }

        private async Task InsertGoogleUser()
        {
            if (environment.IsDevelopment())
            {
                return;
            }

            var googleUserPassword = Environment.GetEnvironmentVariable("GOOGLE_USER_PASSWORD");
            if (string.IsNullOrEmpty(googleUserPassword))
            {
                logger.LogCritical("Failed to find the GOOGLE_USER_PASSWORD environment variable.");
                throw new InvalidOperationException("Failed to find the GOOGLE_USER_PASSWORD environment variable.");
            }
            
            const string googleUserId = "f1620f14-07df-4f6f-bf05-57587d3fefc7";
            var userIdentityResult = await userManager.FindByIdAsync(googleUserId);
            if (userIdentityResult is not null)
                return;

            var subscription = new Subscription
            {
                CreatedAt = DateTime.UtcNow,
                Group = SubscriptionGroup.Basic
            };

            subscription.Cycles.Add(new Cycle
            {
                Duration = CycleSizeHelper.GetDuration(CycleSize.PlayTest),
                ValidRequestCount = CycleSizeHelper.GetValidRequestCount(CycleSize.PlayTest),
                CreatedAt = DateTime.UtcNow,
                Status = CycleStatus.Active,
                StartedAt = DateTime.UtcNow
            });

            var creationResult = await userManager.CreateAsync(new User
            {
                Id = googleUserId,
                UserName = "googleStoreUser",
                Subscriptions = [ subscription ]
            }, googleUserPassword);

            if (creationResult.Errors.Any())
            {
                logger.LogError("Failed to create the googleStoreUser.\nErrors: {@googleUserCreationErrors}", creationResult.Errors);
                return;
            }

            var identityGoogleUser = await userManager.FindByIdAsync(googleUserId);
            if(identityGoogleUser is null)
            {
                logger.LogError("Failed to find the googleStoreUser.");
                return;
            }

            identityGoogleUser.EmailConfirmed = true;
            var updateResult  = await userManager.UpdateAsync(identityGoogleUser);
            if (updateResult.Errors.Any())
            {
                logger.LogError("Failed to enable EmailConfirmed in the googleStoreUser.\nErrors: {@googleUserCreationErrors}", updateResult.Errors);
                return;
            }
        }
    }
}
