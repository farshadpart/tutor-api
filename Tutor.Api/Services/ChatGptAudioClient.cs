using System.ClientModel;
using OpenAI.Audio;
using Tutor.Api.Services.Interfaces;

namespace Tutor.Api.Services;

public sealed class ChatGptAudioClient : IChatGptAudioClient
{
    private readonly AudioClient _whisperClient = new("whisper-1", Environment.GetEnvironmentVariable("TutorKey"));
    private readonly AudioClient _speechClient = new("gpt-4o-mini-tts", Environment.GetEnvironmentVariable("TutorKey"));

    public string Transcribe(Stream audioStream, string fileName, AudioTranscriptionOptions options)
    {
        var transcription = _whisperClient.TranscribeAudio(audioStream, fileName, options);
        return transcription.Value.Text;
    }

    public async Task<ClientResult<BinaryData>> Speech(string text, CancellationToken token)
    {
        return await _speechClient.GenerateSpeechAsync(text, GeneratedSpeechVoice.Nova, null, token);
    }
}
