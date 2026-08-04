namespace Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;

public record FileContainer(BinaryData Content, string ContentType, string FileName);