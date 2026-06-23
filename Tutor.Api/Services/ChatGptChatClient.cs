using OpenAI.Chat;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Services;

public sealed class ChatGptChatClient : IChatGptChatClient
{
    private readonly ChatClient _client = new("gpt-4.1", Environment.GetEnvironmentVariable("TutorKey"));

    public async Task<string> CompleteChatAsync(ChatMessage[] chats, ChatCompletionOptions options)
    {
        var completion = await _client.CompleteChatAsync(chats, options);
        return completion.Value.Content[0].Text;
    }
}
