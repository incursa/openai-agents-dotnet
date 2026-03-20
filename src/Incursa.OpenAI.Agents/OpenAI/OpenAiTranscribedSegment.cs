namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents a transcribed segment and its timing information.
/// </summary>
public sealed class OpenAiTranscribedSegment
{
    /// <summary>Creates a transcribed segment.</summary>
    public OpenAiTranscribedSegment(
        TimeSpan startTime,
        TimeSpan endTime,
        int seekOffset,
        IReadOnlyList<int>? tokenIds,
        float averageLogProbability,
        float noSpeechProbability)
    {
        StartTime = startTime;
        EndTime = endTime;
        SeekOffset = seekOffset;
        TokenIds = tokenIds ?? [];
        AverageLogProbability = averageLogProbability;
        NoSpeechProbability = noSpeechProbability;
    }

    /// <summary>Gets or sets the segment start time.</summary>
    public TimeSpan StartTime { get; init; }

    /// <summary>Gets or sets the segment end time.</summary>
    public TimeSpan EndTime { get; init; }

    /// <summary>Gets or sets the seek offset for the segment.</summary>
    public int SeekOffset { get; init; }

    /// <summary>Gets or sets the raw token identifiers for the segment.</summary>
    public IReadOnlyList<int> TokenIds { get; init; }

    /// <summary>Gets or sets the average log probability for the segment.</summary>
    public float AverageLogProbability { get; init; }

    /// <summary>Gets or sets the no-speech probability for the segment.</summary>
    public float NoSpeechProbability { get; init; }
}
