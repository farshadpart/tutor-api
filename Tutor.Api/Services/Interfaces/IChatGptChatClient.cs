using OpenAI.Chat;

namespace Tutor.Api.Services.Interfaces;

public interface IChatGptChatClient
{
    Task<string> CompleteChatAsync(ChatMessage[] chats, ChatCompletionOptions options);
}
