using Microsoft.AspNetCore.Http;
using OpenAI.Audio;
using Tutor.Api.Models.Exceptions;
using Tutor.Api.Services;
using Tutor.Api.Services.Interfaces;
using Tutor.Api.Tests.Utility;

namespace Tutor.Api.Tests.Services;

public class ChatGptAudioServiceTests
{
    private const string UserId = "user-123";

    [Fact]
    public async Task Transcribe_AssertsUserIdBeforeCallingChatGpt()
    {
        //Arrange
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient
        {
            OnTranscribe = () => subscriptionService.CallOrder.Add("chatgpt")
        };
        var service = CreateService(subscriptionService, chatGptAudioClient);
        var audioFile = CreateAudioFile();

        //Act
        await service.Transcribe(audioFile, UserId);

        //Assert
        Assert.Equal(UserId, subscriptionService.AssertedUserId);
        Assert.Equal(["assert", "chatgpt", "register"], subscriptionService.CallOrder);
    }

    [Fact]
    public async Task Transcribe_WhenChatGptSucceeds_ReturnsTranscriptionAndRegistersRequest()
    {
        //Arrange
        const string transcription = "hello from audio";
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient { Transcription = transcription };
        var service = CreateService(subscriptionService, chatGptAudioClient);
        var audioFile = CreateAudioFile();

        //Act
        var result = await service.Transcribe(audioFile, UserId);

        //Assert
        Assert.Equal(transcription, result);
        Assert.Equal(1, chatGptAudioClient.TranscribeCallCount);
        Assert.Equal("audio.webm", chatGptAudioClient.FileName);
        Assert.Equal("en", chatGptAudioClient.Options?.Language);
        Assert.Equal(UserId, subscriptionService.RegisteredUserId);
        Assert.Equal(1, subscriptionService.RegisterRequestCallCount);
    }

    [Fact]
    public async Task Transcribe_WhenChatGptFails_ThrowsTutorExceptionAndDoesNotRegisterRequest()
    {
        //Arrange
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient
        {
            Exception = new InvalidOperationException("OpenAI failed")
        };
        var service = CreateService(subscriptionService, chatGptAudioClient);
        var audioFile = CreateAudioFile();

        //Act
        var exception = await Assert.ThrowsAsync<TutorException>(() => service.Transcribe(audioFile, UserId));

        //Assert
        Assert.Equal(Errors.CHATGPT_AUDIO_TRANSCRIPTION_FAILED, exception.Message);
        Assert.Equal(1, subscriptionService.AssertCallCount);
        Assert.Equal(0, subscriptionService.RegisterRequestCallCount);
    }

    private static ChatGptAudioService CreateService(
        TestSubscriptionService subscriptionService,
        TestChatGptAudioClient chatGptAudioClient)
    {
        return new ChatGptAudioService(
            subscriptionService,
            chatGptAudioClient,
            new TestLogger<ChatGptAudioService>());
    }

    private static IFormFile CreateAudioFile()
    {
        var content = new MemoryStream([1, 2, 3]);
        return new FormFile(content, 0, content.Length, "audio", "audio.webm")
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/webm"
        };
    }

    private sealed class TestSubscriptionService()
        : SubscriptionService(null!, null!, new TestLogger<SubscriptionService>())
    {
        public int AssertCallCount { get; private set; }
        public int RegisterRequestCallCount { get; private set; }
        public string? AssertedUserId { get; private set; }
        public string? RegisteredUserId { get; private set; }
        public List<string> CallOrder { get; } = [];

        public override Task Assert(string userId)
        {
            AssertCallCount++;
            AssertedUserId = userId;
            CallOrder.Add("assert");
            return Task.CompletedTask;
        }

        public override Task RegisterRequest(string userId)
        {
            RegisterRequestCallCount++;
            RegisteredUserId = userId;
            CallOrder.Add("register");
            return Task.CompletedTask;
        }
    }

    private sealed class TestChatGptAudioClient : IChatGptAudioClient
    {
        public string Transcription { get; init; } = "transcribed text";
        public Exception? Exception { get; init; }
        public int TranscribeCallCount { get; private set; }
        public string? FileName { get; private set; }
        public AudioTranscriptionOptions? Options { get; private set; }
        public Action? OnTranscribe { get; init; }

        public string Transcribe(Stream audioStream, string fileName, AudioTranscriptionOptions options)
        {
            TranscribeCallCount++;
            FileName = fileName;
            Options = options;
            OnTranscribe?.Invoke();

            if (Exception is not null)
            {
                throw Exception;
            }

            return Transcription;
        }
    }
}
