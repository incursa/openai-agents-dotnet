namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the client contract for calling OpenAI audio APIs.
/// </summary>
public interface IOpenAiAudioClient
{
    /// <summary>Transcribes the provided audio stream using the supplied request settings.</summary>
    Task<OpenAiAudioTranscriptionResponse> TranscribeAsync(
        Stream audio,
        string audioFileName,
        OpenAiAudioTranscriptionRequest request,
        CancellationToken cancellationToken);

    /// <summary>Generates spoken audio for the supplied text request.</summary>
    Task<OpenAiSpeechGenerationResponse> GenerateSpeechAsync(
        OpenAiSpeechGenerationRequest request,
        CancellationToken cancellationToken);
}
