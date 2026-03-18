namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Adds a convenience overload for IMcpClientObserver.
/// </summary>

public static class McpClientObserverExtensions
{
    /// <summary>
    /// Observes MCP client activity without requiring an explicit cancellation token.
    /// </summary>

    public static ValueTask ObserveAsync(this IMcpClientObserver observer, McpClientObservation observation)
        => observer.ObserveAsync(observation, CancellationToken.None);
}
