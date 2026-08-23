using OpenAI.Chat;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;

namespace Tutor.Api.Services;

public class TutorChatService(
    ChatGptChatService chatGptChatService,
    ChatGptAudioService chatGptAudioService)
{
    public Task<string> Transcribe(IFormFile voice, string userId)
    {
        return chatGptAudioService.Transcribe(voice, userId);
    }

    public async Task<TutorChatReply> ReplyAsync(
        Message[] messages,
        string userId,
        CancellationToken cancellationToken)
    {
        var openAiMessages = messages.Select(ToOpenAiMessage).ToList();
        openAiMessages.Add(new SystemChatMessage(Prompts.SYSTEM_PROMPT));

        var response = await chatGptChatService.ChatAsync([.. openAiMessages], userId);
        var audioResponse = await chatGptAudioService.Speech(response, userId, cancellationToken);

        return new TutorChatReply(response, audioResponse);
    }

    private static ChatMessage ToOpenAiMessage(Message message) => message.Role switch
    {
        "user" => new UserChatMessage(message.Content),
        "assistant" => new AssistantChatMessage(message.Content),
        _ => throw new ArgumentException($"Unsupported message role: {message.Role}", nameof(message))
    };
}
