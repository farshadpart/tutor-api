using OpenAI.Audio;

namespace Tutor.Api.Services
{
    public class ChatGptAudioService
    {
        private readonly AudioClient _client;

        public ChatGptAudioService()
        {
            _client = new("whisper-1", Environment.GetEnvironmentVariable("TutorKey"));
        }

        public string Transcribe(IFormFile audioFile)
        {
            AudioTranscriptionOptions options = new()
            {
                ResponseFormat = AudioTranscriptionFormat.Verbose,
                TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
                Language = "en"
            };

            AudioTranscription transcription = _client.TranscribeAudio(audioFile.OpenReadStream(), audioFile.FileName, options);
            
            return transcription.Text;
        }
    }
}
