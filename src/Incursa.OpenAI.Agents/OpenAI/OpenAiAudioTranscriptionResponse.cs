using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents the normalized output from an OpenAI audio transcription.
/// </summary>
public sealed class OpenAiAudioTranscriptionResponse
{
    /// <summary>Creates a transcription response.</summary>
    public OpenAiAudioTranscriptionResponse(
        string text,
        string? language,
        TimeSpan? duration,
        IReadOnlyList<OpenAiTranscribedWord>? words,
        IReadOnlyList<OpenAiTranscribedSegment>? segments,
        JsonObject raw)
    {
        Text = text;
        Language = language;
        Duration = duration;
        Words = words ?? [];
        Segments = segments ?? [];
        Raw = raw;
    }

    /// <summary>Gets or sets the transcribed text.</summary>
    public string Text { get; init; }

    /// <summary>Gets or sets the detected or requested language.</summary>
    public string? Language { get; init; }

    /// <summary>Gets or sets the input audio duration when available.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Gets or sets the word-level timestamps.</summary>
    public IReadOnlyList<OpenAiTranscribedWord> Words { get; init; }

    /// <summary>Gets or sets the segment-level timestamps.</summary>
    public IReadOnlyList<OpenAiTranscribedSegment> Segments { get; init; }

    /// <summary>Gets or sets the raw JSON returned by the OpenAI SDK.</summary>
    public JsonObject Raw { get; init; }
}
