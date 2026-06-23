using Microsoft.AspNetCore.Identity;
using Tutor.Api.Models.Account;

namespace Tutor.Api.Tests.Utility;

public sealed class TestUserManager(User? existingUser = null)
    : UserManager<User>(
        new TestUserStore(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        new PasswordHasher<User>(),
        [],
        [],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        new EmptyServiceProvider(),
        new TestLogger<UserManager<User>>())
{
    public IdentityResult CreateResult { get; init; } = IdentityResult.Success;
    public IdentityResult UpdateResult { get; init; } = IdentityResult.Success;
    public bool ReturnCreatedUserFromFind { get; init; } = true;
    public int FindByIdCallCount { get; private set; }
    public int CreateCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public User? CreatedUser { get; private set; }
    public User? UpdatedUser { get; private set; }
    public string? CreatedPassword { get; private set; }

    public override Task<User?> FindByIdAsync(string userId)
    {
        FindByIdCallCount++;

        if (existingUser?.Id == userId)
            return Task.FromResult<User?>(existingUser);

        if (ReturnCreatedUserFromFind && CreatedUser?.Id == userId)
            return Task.FromResult<User?>(CreatedUser);

        return Task.FromResult<User?>(null);
    }

    public override Task<IdentityResult> CreateAsync(User user, string password)
    {
        CreateCallCount++;
        CreatedUser = user;
        CreatedPassword = password;
        return Task.FromResult(CreateResult);
    }

    public override Task<IdentityResult> UpdateAsync(User user)
    {
        UpdateCallCount++;
        UpdatedUser = user;
        return Task.FromResult(UpdateResult);
    }
}
