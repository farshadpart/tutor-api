using NSubstitute;
using OpenAI.Chat;
using Tutor.Api.Services;
using Tutor.Api.Services.Interfaces;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Services;

public class ChatGptChatServiceTests
{
    private const string UserId = "user-123";

    [Fact]
    public async Task ChatAsync_AssertsUserIdBeforeCallingChatGpt()
    {
        // Arrange
        var callOrder = new List<string>();
        var subscriptionService = CreateSubscriptionService();
        var chatGptChatClient = Substitute.For<IChatGptChatClient>();
        var service = CreateService(subscriptionService, chatGptChatClient);
        var chats = CreateChats();

        subscriptionService.Assert(UserId).Returns(_ =>
        {
            callOrder.Add("assert");
            return Task.CompletedTask;
        });
        chatGptChatClient.CompleteChatAsync(chats, Arg.Any<ChatCompletionOptions>()).Returns(_ =>
        {
            callOrder.Add("chatgpt");
            return Task.FromResult("chat response");
        });
        subscriptionService.RegisterRequest(UserId).Returns(_ =>
        {
            callOrder.Add("register");
            return Task.CompletedTask;
        });

        // Act
        await service.ChatAsync(chats, UserId);

        // Assert
        Assert.Equal(["assert", "chatgpt", "register"], callOrder);
    }

    [Fact]
    public async Task ChatAsync_WhenChatGptSucceeds_ReturnsResponseAndRegistersRequest()
    {
        // Arrange
        const string response = "hello from chat";
        ChatCompletionOptions? capturedOptions = null;
        var subscriptionService = CreateSubscriptionService();
        var chatGptChatClient = Substitute.For<IChatGptChatClient>();
        var service = CreateService(subscriptionService, chatGptChatClient);
        var chats = CreateChats();

        chatGptChatClient
            .CompleteChatAsync(chats, Arg.Do<ChatCompletionOptions>(options => capturedOptions = options))
            .Returns(Task.FromResult(response));

        // Act
        var result = await service.ChatAsync(chats, UserId);

        // Assert
        Assert.Equal(response, result);
        Assert.NotNull(capturedOptions);
        Assert.Equal(0.7f, capturedOptions.Temperature);
        Assert.Equal(1024, capturedOptions.MaxOutputTokenCount);
        await subscriptionService.Received(1).Assert(UserId);
        await subscriptionService.Received(1).RegisterRequest(UserId);
        await chatGptChatClient.Received(1).CompleteChatAsync(chats, Arg.Any<ChatCompletionOptions>());
    }

    [Fact]
    public async Task ChatAsync_WhenChatGptFails_RethrowsAndDoesNotRegisterRequest()
    {
        // Arrange
        var exception = new InvalidOperationException("OpenAI failed");
        var subscriptionService = CreateSubscriptionService();
        var chatGptChatClient = Substitute.For<IChatGptChatClient>();
        var service = CreateService(subscriptionService, chatGptChatClient);
        var chats = CreateChats();

        chatGptChatClient
            .CompleteChatAsync(chats, Arg.Any<ChatCompletionOptions>())
            .Returns(Task.FromException<string>(exception));

        // Act
        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChatAsync(chats, UserId));

        // Assert
        Assert.Same(exception, result);
        await subscriptionService.Received(1).Assert(UserId);
        await subscriptionService.DidNotReceive().RegisterRequest(Arg.Any<string>());
    }

    private static ChatGptChatService CreateService(
        SubscriptionService subscriptionService,
        IChatGptChatClient chatGptChatClient)
    {
        return new ChatGptChatService(
            subscriptionService,
            chatGptChatClient,
            new TestLogger<ChatGptChatService>());
    }

    private static SubscriptionService CreateSubscriptionService()
    {
        var subscriptionService = Substitute.For<SubscriptionService>(
            null!,
            null!,
            new TestLogger<SubscriptionService>());

        subscriptionService.Assert(Arg.Any<string>()).Returns(Task.CompletedTask);
        subscriptionService.RegisterRequest(Arg.Any<string>()).Returns(Task.CompletedTask);

        return subscriptionService;
    }

    private static ChatMessage[] CreateChats()
    {
        return
        [
            ChatMessage.CreateSystemMessage("You are a tutor."),
            ChatMessage.CreateUserMessage("Hello")
        ];
    }
}
