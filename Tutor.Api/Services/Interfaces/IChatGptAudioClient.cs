using System.ClientModel;
using OpenAI.Audio;

namespace Tutor.Api.Services.Interfaces;

public interface IChatGptAudioClient
{
    string Transcribe(Stream audioStream, string fileName, AudioTranscriptionOptions options);
    Task<ClientResult<BinaryData>> Speech(string text, CancellationToken token);
}
