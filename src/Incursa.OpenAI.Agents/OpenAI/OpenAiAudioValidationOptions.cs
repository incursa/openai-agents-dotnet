namespace Incursa.OpenAI.Agents;

/// <summary>
/// Configures local preflight validation for audio transcription inputs.
/// </summary>
public class OpenAiAudioValidationOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed audio size in bytes for seekable streams.
    /// Set to <see langword="null"/> to disable the size cap.
    /// </summary>
    public long? MaxFileSizeBytes { get; set; } = 25L * 1024L * 1024L;

    /// <summary>
    /// Gets or sets the allowed audio file extensions.
    /// </summary>
    public ICollection<string> AllowedFileExtensions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".flac",
        ".m4a",
        ".mp3",
        ".ogg",
        ".wav",
        ".webm",
    };

    /// <summary>
    /// Gets or sets whether known audio headers are checked before upload.
    /// </summary>
    public bool EnableContentSniffing { get; set; } = true;
}
