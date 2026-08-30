namespace Tutor.Api.Models.Account;

public class StoredImage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string FileName { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
}
