using Microsoft.AspNetCore.Http;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;
using Tutor.Api.Validators;

namespace Tutor.Api.Tests.Validators;

public class TutorChatValidatorTests
{
    [Fact]
    public void Validate_WithValidMessages_Succeeds()
    {
        Message[] messages =
        [
            new("user", "Question"),
            new("assistant", "Answer")
        ];

        var result = TutorChatValidator.Validate(messages);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithNoMessages_Fails()
    {
        var result = TutorChatValidator.Validate([]);

        Assert.True(result.IsFailed);
        Assert.Equal("Chat messages cannot be null or empty.", result.Errors[0].Message);
    }

    [Fact]
    public void Validate_WithBlankContent_Fails()
    {
        var result = TutorChatValidator.Validate([new Message("user", " ")]);

        Assert.True(result.IsFailed);
        Assert.Equal("Message content cannot be empty.", result.Errors[0].Message);
    }

    [Fact]
    public void Validate_WithLongContent_Fails()
    {
        var result = TutorChatValidator.Validate([new Message("user", new string('a', Limit.MAX_MESSAGE_LENGTH))]);

        Assert.Equal(
            $"Chat messages should be less than {Limit.MAX_MESSAGE_LENGTH} characters.",
            result.Errors[0].Message);
    }

    [Fact]
    public void Validate_WithUnsupportedRole_Fails()
    {
        var result = TutorChatValidator.Validate([new Message("system", "Instructions")]);

        Assert.True(result.IsFailed);
        Assert.Equal("The message role 'system' is not supported.", result.Errors[0].Message);
    }

    [Fact]
    public void Validate_WithVoiceBelowMaximumSize_Succeeds()
    {
        var voice = CreateVoiceFile(Limit.MAX_VOICE_SIZE - 1);

        var result = TutorChatValidator.Validate(voice);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithVoiceAtMaximumSize_Fails()
    {
        var voice = CreateVoiceFile(Limit.MAX_VOICE_SIZE);

        var result = TutorChatValidator.Validate(voice);

        Assert.True(result.IsFailed);
        Assert.Equal("A voice message size should be less than 3MB.", result.Errors[0].Message);
    }

    [Fact]
    public void Validate_WithMissingVoice_Fails()
    {
        var result = TutorChatValidator.Validate((IFormFile?)null);

        Assert.True(result.IsFailed);
        Assert.Equal("A voice message is required.", result.Errors[0].Message);
    }

    private static IFormFile CreateVoiceFile(long length)
    {
        var stream = new MemoryStream();
        stream.SetLength(length);
        return new FormFile(stream, 0, length, "voice", "voice.webm");
    }
}
