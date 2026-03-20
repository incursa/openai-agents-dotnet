namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents a transcribed word and its timing information.
/// </summary>
public sealed class OpenAiTranscribedWord
{
    /// <summary>Creates a transcribed word.</summary>
    public OpenAiTranscribedWord(TimeSpan startTime, TimeSpan endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>Gets or sets the start time of the word.</summary>
    public TimeSpan StartTime { get; init; }

    /// <summary>Gets or sets the end time of the word.</summary>
    public TimeSpan EndTime { get; init; }
}
