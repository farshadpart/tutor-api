namespace Tutor.Api.Models.Account;

public class UserSettings
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public bool AutoPlayVoice { get; set; } = true;
}
