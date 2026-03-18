namespace Incursa.OpenAI.Agents;

/// <summary>Thrown when persisted session version does not match expected runtime version.</summary>
public sealed class AgentSessionVersionMismatchException : InvalidOperationException
{
    /// <summary>Creates an exception for a version mismatch.</summary>
    public AgentSessionVersionMismatchException(string sessionKey, long expectedVersion, long actualVersion)
        : base($"Session '{sessionKey}' expected version {expectedVersion} but found {actualVersion}.")
    {
        SessionKey = sessionKey;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>Gets the session key that triggered the conflict.</summary>
    public string SessionKey { get; }

    /// <summary>Gets the caller expected version.</summary>
    public long ExpectedVersion { get; }

    /// <summary>Gets the version currently persisted.</summary>
    public long ActualVersion { get; }
}
