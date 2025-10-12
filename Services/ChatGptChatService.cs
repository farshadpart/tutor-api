using OpenAI.Chat;

namespace Tutor.Api.Services
{
    public class ChatGptChatService
    {
        private readonly ChatClient _client;

        public ChatGptChatService()
        {
            _client = new ChatClient("gpt-4.1", Environment.GetEnvironmentVariable("TutorKey"));    
        }

        public async Task<string> ChatAsync(ChatMessage[] chats)
        {
            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 1024
            };
            ChatCompletion completion = await _client.CompleteChatAsync(chats, options);
            return completion.Content[0].Text;
        }
    }
}
