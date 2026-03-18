namespace Incursa.OpenAI.Agents;

/// <summary>Defines how an in-memory or file-backed session is compacted when saving.</summary>
public enum Sessions
{
    /// <summary>Keep the full conversation and turn history.</summary>
    None,

    /// <summary>Keep the latest configured conversation window when trimming.</summary>
    KeepLatestWindow,
}
