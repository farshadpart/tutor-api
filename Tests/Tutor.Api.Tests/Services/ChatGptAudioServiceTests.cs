using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using OpenAI.Audio;
using Shouldly;
using Tutor.Api.Models.Exceptions;
using Tutor.Api.Models.Subscriptions;
using Tutor.Api.Models.Tutor.Api.Contracts.Subscription;
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
        subscriptionService.AssertedUserId.ShouldBe(UserId);
        subscriptionService.CallOrder.ShouldBe(["assert", "chatgpt", "register"]);
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
        result.ShouldBe(transcription);
        chatGptAudioClient.TranscribeCallCount.ShouldBe(1);
        chatGptAudioClient.FileName.ShouldBe("audio.webm");
        chatGptAudioClient.Options?.Language.ShouldBe("en");
        subscriptionService.RegisteredUserId.ShouldBe(UserId);
        subscriptionService.RegisterRequestCallCount.ShouldBe(1);
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
        var exception = await Should.ThrowAsync<TutorException>(() => service.Transcribe(audioFile, UserId));

        //Assert
        exception.Message.ShouldBe(Errors.CHATGPT_AUDIO_TRANSCRIPTION_FAILED);
        subscriptionService.AssertCallCount.ShouldBe(1);
        subscriptionService.RegisterRequestCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Speech_AssertsUserIdBeforeCallingChatGpt()
    {
        //Arrange
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient
        {
            OnSpeech = () => subscriptionService.CallOrder.Add("chatgpt")
        };
        var service = CreateService(subscriptionService, chatGptAudioClient);

        //Act
        await service.Speech("Hello", UserId, CancellationToken.None);

        //Assert
        subscriptionService.AssertedUserId.ShouldBe(UserId);
        subscriptionService.CallOrder.ShouldBe(["assert", "chatgpt", "register"]);
    }

    [Fact]
    public async Task Speech_WhenChatGptSucceeds_ReturnsMp3AndRegistersRequest()
    {
        //Arrange
        const string text = "Hello from text";
        byte[] audio = [1, 2, 3, 4];
        using var cancellationTokenSource = new CancellationTokenSource();
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient { SpeechAudio = BinaryData.FromBytes(audio) };
        var service = CreateService(subscriptionService, chatGptAudioClient);

        //Act
        var result = await service.Speech(text, UserId, cancellationTokenSource.Token);

        //Assert
        result.Content.ToArray().ShouldBe(audio);
        result.ContentType.ShouldBe("audio/mpeg");
        result.FileName.ShouldEndWith(".mp3");
        Guid.TryParse(Path.GetFileNameWithoutExtension(result.FileName), out _).ShouldBeTrue();
        chatGptAudioClient.SpeechCallCount.ShouldBe(1);
        chatGptAudioClient.SpeechText.ShouldBe(text);
        chatGptAudioClient.SpeechToken.ShouldBe(cancellationTokenSource.Token);
        subscriptionService.RegisteredUserId.ShouldBe(UserId);
        subscriptionService.RegisterRequestCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Speech_WhenChatGptFails_RethrowsAndDoesNotRegisterRequest()
    {
        //Arrange
        var expectedException = new InvalidOperationException("OpenAI failed");
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient { SpeechException = expectedException };
        var service = CreateService(subscriptionService, chatGptAudioClient);

        //Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => service.Speech("Hello", UserId, CancellationToken.None));

        //Assert
        exception.ShouldBeSameAs(expectedException);
        subscriptionService.AssertCallCount.ShouldBe(1);
        subscriptionService.RegisterRequestCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Speech_WhenCancelled_RethrowsCancellationAndDoesNotRegisterRequest()
    {
        //Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var subscriptionService = new TestSubscriptionService();
        var chatGptAudioClient = new TestChatGptAudioClient
        {
            SpeechException = new OperationCanceledException(cancellationTokenSource.Token)
        };
        var service = CreateService(subscriptionService, chatGptAudioClient);

        //Act
        await Should.ThrowAsync<OperationCanceledException>(
            () => service.Speech("Hello", UserId, cancellationTokenSource.Token));

        //Assert
        chatGptAudioClient.SpeechCallCount.ShouldBe(1);
        subscriptionService.RegisterRequestCallCount.ShouldBe(0);
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

    private sealed class TestSubscriptionService : ISubscriptionService
    {
        public int AssertCallCount { get; private set; }
        public int RegisterRequestCallCount { get; private set; }
        public string? AssertedUserId { get; private set; }
        public string? RegisteredUserId { get; private set; }
        public List<string> CallOrder { get; } = [];

        public Task Assert(string userId)
        {
            AssertCallCount++;
            AssertedUserId = userId;
            CallOrder.Add("assert");
            return Task.CompletedTask;
        }

        public Task RegisterRequest(string userId)
        {
            RegisterRequestCallCount++;
            RegisteredUserId = userId;
            CallOrder.Add("register");
            return Task.CompletedTask;
        }

        public Task Create(CreateSubscriptionRequest createRequest)
        {
            return Task.CompletedTask;
        }

        public List<string> GetSubscriptionGroups()
        {
            return [];
        }

        public SubscriptionGroup? GetUserUseableSubscriptionGroup(string userId)
        {
            return null;
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
        public BinaryData SpeechAudio { get; init; } = BinaryData.FromBytes([1, 2, 3]);
        public Exception? SpeechException { get; init; }
        public int SpeechCallCount { get; private set; }
        public string? SpeechText { get; private set; }
        public CancellationToken SpeechToken { get; private set; }
        public Action? OnSpeech { get; init; }

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

        public Task<ClientResult<BinaryData>> Speech(string text, CancellationToken token)
        {
            SpeechCallCount++;
            SpeechText = text;
            SpeechToken = token;
            OnSpeech?.Invoke();

            if (SpeechException is not null)
            {
                return Task.FromException<ClientResult<BinaryData>>(SpeechException);
            }

            return Task.FromResult(ClientResult.FromValue(SpeechAudio, Substitute.For<PipelineResponse>()));
        }
    }
}
