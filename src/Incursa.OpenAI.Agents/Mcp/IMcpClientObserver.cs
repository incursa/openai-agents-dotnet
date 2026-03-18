namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Defines the contract for observing MCP client activity.
/// </summary>

public interface IMcpClientObserver
{
    ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken);
}
