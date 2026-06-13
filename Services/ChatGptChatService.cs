using OpenAI.Chat;
using SerilogTimings;

namespace Tutor.Api.Services
{
    public class ChatGptChatService(SubscriptionService subscriptionService, ILogger<ChatGptChatService> logger)
    {
        private readonly ChatClient _client = new("gpt-4.1", Environment.GetEnvironmentVariable("TutorKey"));

        public async Task<string> ChatAsync(ChatMessage[] chats, string userId)
        {
            logger.LogInformation(
                "Chat completion started for user {UserId}; OpenAI message count {MessageCount}.",
                userId,
                chats.Length);

            logger.LogDebug("Asserting subscription before chat completion for user {UserId}.", userId);
            await subscriptionService.Assert(userId);
            logger.LogDebug("Subscription assertion passed before chat completion for user {UserId}.", userId);

            ChatCompletionOptions options = new()
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 1024
            };

            ChatCompletion completion;
            using (var operation = Operation.Begin("Complete chat with OpenAI for user {UserId}", userId))
            {
                try
                {
                    logger.LogDebug(
                        "Calling OpenAI chat completion for user {UserId}; temperature {Temperature}, max output tokens {MaxOutputTokenCount}.",
                        userId,
                        options.Temperature,
                        options.MaxOutputTokenCount);
                    completion = await _client.CompleteChatAsync(chats, options);
                    operation.Complete();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "OpenAI chat completion failed for user {UserId}.", userId);
                    operation.SetException(ex);
                    operation.Abandon();
                    throw;
                }
            }

            var response = completion.Content[0].Text;
            logger.LogInformation(
                "OpenAI chat completion succeeded for user {UserId}; response length {ResponseLength}.",
                userId,
                response.Length);

            logger.LogDebug("Registering subscription usage after chat completion for user {UserId}.", userId);
            await subscriptionService.RegisterRequest(userId);
            logger.LogDebug("Subscription usage registered after chat completion for user {UserId}.", userId);

            return response;
        }
    }
}
