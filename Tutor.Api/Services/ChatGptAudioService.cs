using OpenAI.Audio;
using Tutor.Api.Models.Exceptions;
using SerilogTimings;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Services
{
    public class ChatGptAudioService(
        ISubscriptionService subscriptionService,
        IChatGptAudioClient chatGptAudioClient,
        ILogger<ChatGptAudioService> logger)
    {
        public async Task<string> Transcribe(IFormFile audioFile, string userId)
        {
            logger.LogInformation(
                "Audio transcription started for user {UserId}; file name {FileName}, content type {ContentType}, size {SizeBytes} bytes.",
                userId,
                audioFile.FileName,
                audioFile.ContentType,
                audioFile.Length);

            logger.LogDebug("Asserting subscription before audio transcription for user {UserId}.", userId);
            await subscriptionService.Assert(userId);
            logger.LogDebug("Subscription assertion passed before audio transcription for user {UserId}.", userId);

            AudioTranscriptionOptions options = new()
            {
                ResponseFormat = AudioTranscriptionFormat.Verbose,
                TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
                Language = "en"
            };

            string transcription;
            using (var operation = Operation.Begin("Transcribe audio with OpenAI for user {UserId}", userId))
            {
                try
                {
                    logger.LogDebug(
                        "Calling OpenAI audio transcription for user {UserId}; response format {ResponseFormat}, language {Language}.",
                        userId,
                        options.ResponseFormat,
                        options.Language);
                    transcription = chatGptAudioClient.Transcribe(audioFile.OpenReadStream(), audioFile.FileName, options);
                    operation.Complete();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "OpenAI audio transcription failed for user {UserId}.", userId);
                    operation.SetException(ex);
                    operation.Abandon();
                    throw new TutorException(Errors.CHATGPT_AUDIO_TRANSCRIPTION_FAILED);
                }
            }

            logger.LogInformation(
                "OpenAI audio transcription succeeded for user {UserId}; transcription length {TranscriptionLength}.",
                userId,
                transcription.Length);

            logger.LogDebug("Registering subscription usage after audio transcription for user {UserId}.", userId);
            await subscriptionService.RegisterRequest(userId);
            logger.LogDebug("Subscription usage registered after audio transcription for user {UserId}.", userId);

            return transcription;
        }
        
        public async Task<FileContainer> Speech(string text, string userId, CancellationToken token)
        {
            logger.LogInformation(
                "Speech generation started for user {UserId}; input length {InputLength}.",
                userId,
                text.Length);

            logger.LogDebug("Asserting subscription before speech generation for user {UserId}.", userId);
            await subscriptionService.Assert(userId);
            logger.LogDebug("Subscription assertion passed before speech generation for user {UserId}.", userId);

            BinaryData audioData;
            using (var operation = Operation.Begin("Generate speech with OpenAI for user {UserId}", userId))
            {
                try
                {
                    logger.LogDebug("Calling OpenAI speech generation for user {UserId}.", userId);
                    var response = await chatGptAudioClient.Speech(text, token);
                    audioData = response.Value;
                    operation.Complete();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    logger.LogInformation("OpenAI speech generation was cancelled for user {UserId}.", userId);
                    operation.Abandon();
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "OpenAI speech generation failed for user {UserId}.", userId);
                    operation.SetException(ex);
                    operation.Abandon();
                    throw;
                }
            }

            var fileName = $"{Guid.NewGuid()}.mp3";
            logger.LogInformation(
                "OpenAI speech generation succeeded for user {UserId}; audio size {AudioSizeBytes} bytes, file name {FileName}.",
                userId,
                audioData.ToMemory().Length,
                fileName);

            logger.LogDebug("Registering subscription usage after speech generation for user {UserId}.", userId);
            await subscriptionService.RegisterRequest(userId);
            logger.LogDebug("Subscription usage registered after speech generation for user {UserId}.", userId);

            return new FileContainer(audioData, "audio/mpeg", fileName);
        }
    }
}
