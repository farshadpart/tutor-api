using FluentResults;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;

namespace Tutor.Api.Validators;

public static class TutorChatValidator
{
    public static Result Validate(IFormFile? voice)
    {
        if (voice is null)
        {
            return Result.Fail("A voice message is required.");
        }

        if (voice.Length >= Limit.MAX_VOICE_SIZE)
        {
            var maxSizeMb = Limit.MAX_VOICE_SIZE / (1024 * 1024);
            return Result.Fail($"A voice message size should be less than {maxSizeMb}MB.");
        }

        return Result.Ok();
    }

    public static Result Validate(IReadOnlyCollection<Message>? messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return Result.Fail("Chat messages cannot be null or empty.");
        }

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return Result.Fail("Message content cannot be empty.");
            }

            if (message.Content.Length >= Limit.MAX_MESSAGE_LENGTH)
            {
                return Result.Fail(
                    $"Chat messages should be less than {Limit.MAX_MESSAGE_LENGTH} characters.");
            }

            if (message.Role is not ("user" or "assistant"))
            {
                return Result.Fail($"The message role '{message.Role}' is not supported.");
            }
        }

        return Result.Ok();
    }
}
