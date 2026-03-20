namespace Incursa.OpenAI.Agents;

/// <summary>
/// Adds cancellation-token-free helpers for <see cref="IOpenAiAudioClient"/>.
/// </summary>
public static class OpenAiAudioClientExtensions
{
    /// <summary>Transcribes audio using no cancellation token.</summary>
    public static Task<OpenAiAudioTranscriptionResponse> TranscribeAsync(
        this IOpenAiAudioClient client,
        Stream audio,
        string audioFileName,
        OpenAiAudioTranscriptionRequest request)
        => client.TranscribeAsync(audio, audioFileName, request, CancellationToken.None);

    /// <summary>Generates speech using no cancellation token.</summary>
    public static Task<OpenAiSpeechGenerationResponse> GenerateSpeechAsync(
        this IOpenAiAudioClient client,
        OpenAiSpeechGenerationRequest request)
        => client.GenerateSpeechAsync(request, CancellationToken.None);
}
