using OpenAI.Audio;
using Tutor.Api.Models.Exceptions;
using SerilogTimings;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Services
{
    public class ChatGptAudioService(
        SubscriptionService subscriptionService,
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
    }
}
