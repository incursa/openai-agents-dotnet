namespace Incursa.OpenAI.Agents;

/// <summary>
/// Represents generated speech audio returned by OpenAI.
/// </summary>
public sealed class OpenAiSpeechGenerationResponse
{
    /// <summary>Creates a generated speech response.</summary>
    public OpenAiSpeechGenerationResponse(BinaryData audio, string format, string contentType)
    {
        Audio = audio;
        Format = format;
        ContentType = contentType;
    }

    /// <summary>Gets or sets the generated audio bytes.</summary>
    public BinaryData Audio { get; init; }

    /// <summary>Gets or sets the output audio format identifier.</summary>
    public string Format { get; init; }

    /// <summary>Gets or sets the output audio content type.</summary>
    public string ContentType { get; init; }
}
