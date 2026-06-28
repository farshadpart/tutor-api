using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;

namespace Tutor.Api.Services.Interfaces;

public interface ISubscriptionService
{
    Task Create(CreateSubscriptionRequest createRequest);
    Task RegisterRequest(string userId);
    List<string> GetSubscriptionGroups();
    SubscriptionGroup? GetUserUseableSubscriptionGroup(string userId);
    Task Assert(string userId);
}
