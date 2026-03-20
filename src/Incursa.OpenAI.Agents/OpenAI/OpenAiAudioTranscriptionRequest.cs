namespace Incursa.OpenAI.Agents;

/// <summary>
/// Configures an OpenAI audio transcription request.
/// </summary>
public sealed class OpenAiAudioTranscriptionRequest
{
    /// <summary>Creates a request for the specified transcription model.</summary>
    public OpenAiAudioTranscriptionRequest(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        Model = model;
    }

    /// <summary>Gets or sets the upstream model identifier used for transcription.</summary>
    public string Model { get; init; }

    /// <summary>Gets or sets an optional language hint for the transcription.</summary>
    public string? Language { get; init; }

    /// <summary>Gets or sets an optional prompt that guides the transcription.</summary>
    public string? Prompt { get; init; }

    /// <summary>Gets or sets whether word-level timestamps should be requested.</summary>
    public bool IncludeWordTimestamps { get; init; }

    /// <summary>Gets or sets whether segment-level timestamps should be requested.</summary>
    public bool IncludeSegmentTimestamps { get; init; }

    /// <summary>Gets or sets whether token log probabilities should be requested.</summary>
    public bool IncludeLogProbabilities { get; init; }
}
