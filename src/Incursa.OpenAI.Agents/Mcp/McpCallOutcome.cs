namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines McpCallOutcome values used by the runtime.
/// </summary>

public enum McpCallOutcome
{
    /// <summary>
    /// The call completed successfully.
    /// </summary>
    Success,
    /// <summary>
    /// The call did not complete immediately and a retry was scheduled.
    /// </summary>
    RetryScheduled,
    /// <summary>
    /// The call failed due to a transport-level problem.
    /// </summary>
    TransportFailure,
    /// <summary>
    /// The call failed due to an authentication or authorization problem.
    /// </summary>
    AuthenticationFailure,
    /// <summary>
    /// The call reached the server but failed there.
    /// </summary>
    ServerFailure,
}
