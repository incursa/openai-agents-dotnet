namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Configures retry and observation behavior for MCP calls.
/// </summary>

public sealed class McpClientOptions
{
    /// <summary>
    /// Gets or sets the number of retry attempts for transient MCP failures.
    /// </summary>

    public int RetryCount { get; init; } = 2;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the observer that receives MCP client telemetry.
    /// </summary>

    public IMcpClientObserver? Observer { get; init; }
}
