namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for observing MCP client activity.
/// </summary>

public interface IMcpClientObserver
{
    /// <summary>
    /// Observes one MCP client call and its outcome.
    /// </summary>
    ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken);
}
