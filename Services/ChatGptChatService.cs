using OpenAI.Chat;

namespace Tutor.Api.Services
{
    public class ChatGptChatService
    {
        private readonly ChatClient _client;
        private readonly SubscriptionService _subscriptionService;

        public ChatGptChatService(SubscriptionService subscriptionService)
        {
            _client = new ChatClient("gpt-4.1", Environment.GetEnvironmentVariable("TutorKey"));    
            _subscriptionService = subscriptionService;
        }

        public async Task<string> ChatAsync(ChatMessage[] chats, string userId)
        {
            await _subscriptionService.Assert(userId);

            ChatCompletionOptions options = new()
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 1024
            };
            ChatCompletion completion = await _client.CompleteChatAsync(chats, options);

            await _subscriptionService.RegisterRequest(userId);
            return completion.Content[0].Text;
        }
    }
}
