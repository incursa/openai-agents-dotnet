namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines McpCallOutcome values used by the runtime.
/// </summary>

public enum McpCallOutcome
{
    Success,
    RetryScheduled,
    TransportFailure,
    AuthenticationFailure,
    ServerFailure,
}
