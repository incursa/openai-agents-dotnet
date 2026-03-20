using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Extensions;

/// <summary>
/// Configures runtime observation logging for the agent services.
/// </summary>

public sealed class AgentRuntimeOptions
{
    /// <summary>
    /// Gets or sets whether runtime observations are written to the logging pipeline.
    /// </summary>

    public bool EnableLoggingObserver { get; set; } = true;
}

/// <summary>
/// Configures session retention behavior for file-backed agent sessions.
/// </summary>

public sealed class AgentSessionRetentionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of conversation items retained per session.
    /// </summary>

    public int? MaxConversationItems { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of turns retained per session.
    /// </summary>

    public int? MaxTurns { get; set; }

    /// <summary>
    /// Gets or sets the compaction mode used when the session store trims history.
    /// </summary>

    public Sessions CompactionMode { get; set; } = Sessions.KeepLatestWindow;

    /// <summary>
    /// Gets or sets the absolute lifetime for a session before expiration.
    /// </summary>

    public TimeSpan? AbsoluteLifetime { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration window for active sessions.
    /// </summary>

    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets whether expired sessions are cleaned up when loaded.
    /// </summary>

    public bool CleanupExpiredOnLoad { get; set; } = true;

    /// <summary>
    /// Gets or sets whether expired sessions are cleaned up when saved.
    /// </summary>

    public bool CleanupExpiredOnSave { get; set; } = true;

    internal AgentSessionStoreOptions ToStoreOptions()
        => new()
        {
            MaxConversationItems = MaxConversationItems,
            MaxTurns = MaxTurns,
            CompactionMode = CompactionMode,
            AbsoluteLifetime = AbsoluteLifetime,
            SlidingExpiration = SlidingExpiration,
            CleanupExpiredOnLoad = CleanupExpiredOnLoad,
            CleanupExpiredOnSave = CleanupExpiredOnSave,
        };
}

/// <summary>
/// Configures the OpenAI Responses integration and its MCP client settings.
/// </summary>

public sealed class OpenAiResponsesOptions
{
    /// <summary>
    /// Gets or sets the named `HttpClient` used for OpenAI Responses calls.
    /// </summary>

    public string HttpClientName { get; set; } = "openai";

    /// <summary>
    /// Gets or sets the named `HttpClient` used for MCP transport calls.
    /// </summary>

    public string McpHttpClientName { get; set; } = "incursa-agents-mcp";

    /// <summary>
    /// Gets or sets the relative path for the OpenAI Responses endpoint.
    /// </summary>

    public string ResponsesPath { get; set; } = "v1/responses";

    /// <summary>
    /// Gets or sets the base address applied to the OpenAI `HttpClient`.
    /// </summary>

    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the API key used for OpenAI authentication.
    /// </summary>

    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets how many times MCP calls are retried after transient failures.
    /// </summary>

    public int McpRetryCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay between MCP retry attempts.
    /// </summary>

    public TimeSpan McpRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets whether MCP client observations are logged.
    /// </summary>

    public bool EnableMcpLoggingObserver { get; set; } = true;

    internal McpClientOptions ToMcpClientOptions(IMcpClientObserver? observer)
        => new()
        {
            RetryCount = McpRetryCount,
            RetryDelay = McpRetryDelay,
            Observer = observer,
        };
}

/// <summary>
/// Configures the OpenAI audio integration.
/// </summary>
public sealed class OpenAiAudioOptions : OpenAiAudioValidationOptions
{
    /// <summary>
    /// Gets or sets the named <see cref="HttpClient"/> used for OpenAI audio calls.
    /// </summary>
    public string HttpClientName { get; set; } = "openai-audio";

    /// <summary>
    /// Gets or sets the base address applied to the OpenAI audio <see cref="HttpClient"/>.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the API key used for OpenAI authentication.
    /// </summary>
    public string? ApiKey { get; set; }
}
