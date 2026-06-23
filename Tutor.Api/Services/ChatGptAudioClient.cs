using OpenAI.Audio;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Services;

public sealed class ChatGptAudioClient : IChatGptAudioClient
{
    private readonly AudioClient _client = new("whisper-1", Environment.GetEnvironmentVariable("TutorKey"));

    public string Transcribe(Stream audioStream, string fileName, AudioTranscriptionOptions options)
    {
        var transcription = _client.TranscribeAudio(audioStream, fileName, options);
        return transcription.Value.Text;
    }
}
