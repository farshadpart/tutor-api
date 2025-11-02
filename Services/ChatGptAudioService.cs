using OpenAI.Audio;

namespace Tutor.Api.Services
{
    public class ChatGptAudioService
    {
        private readonly AudioClient _client;
        private readonly SubscriptionService _subscriptionService;

        public ChatGptAudioService(SubscriptionService subscriptionService)
        {
            _client = new("whisper-1", Environment.GetEnvironmentVariable("TutorKey"));
            _subscriptionService = subscriptionService;
        }

        public async Task<string> Transcribe(IFormFile audioFile, string userId)
        {
            await _subscriptionService.Assert(userId);

            AudioTranscriptionOptions options = new()
            {
                ResponseFormat = AudioTranscriptionFormat.Verbose,
                TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
                Language = "en"
            };

            AudioTranscription transcription = _client.TranscribeAudio(audioFile.OpenReadStream(), audioFile.FileName, options);

            await _subscriptionService.RegisterRequest(userId);
            return transcription.Text;
        }
    }
}
