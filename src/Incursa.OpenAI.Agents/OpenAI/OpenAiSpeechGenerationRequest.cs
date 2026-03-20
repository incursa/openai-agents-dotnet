namespace Incursa.OpenAI.Agents;

/// <summary>
/// Configures an OpenAI speech-generation request.
/// </summary>
public sealed class OpenAiSpeechGenerationRequest
{
    /// <summary>Creates a request with required model, input text, and voice.</summary>
    public OpenAiSpeechGenerationRequest(string model, string input, string voice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(voice);

        Model = model;
        Input = input;
        Voice = voice;
    }

    /// <summary>Gets or sets the upstream model identifier used for speech generation.</summary>
    public string Model { get; init; }

    /// <summary>Gets or sets the text to convert into audio.</summary>
    public string Input { get; init; }

    /// <summary>Gets or sets the upstream voice identifier.</summary>
    public string Voice { get; init; }

    /// <summary>Gets or sets the optional output format identifier.</summary>
    public string? Format { get; init; }

    /// <summary>Gets or sets optional voice instructions.</summary>
    public string? Instructions { get; init; }

    /// <summary>Gets or sets the requested playback speed ratio.</summary>
    public float? SpeedRatio { get; init; }
}
