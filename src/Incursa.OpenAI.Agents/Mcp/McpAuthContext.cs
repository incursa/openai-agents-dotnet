namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Carries user and session metadata used to resolve MCP authorization.
/// </summary>

public sealed record McpAuthContext
{
    /// <summary>
    /// Creates an empty auth context when no user or session metadata is available.
    /// </summary>

    public McpAuthContext()
        : this(null, null, null, null, null, null, null, null)
    {
    }

    public McpAuthContext(
        string? userId,
        string? tenantId,
        string? workspaceId,
        string? mailboxId,
        string? connectionId,
        string? sessionKey,
        string? agentName,
        IReadOnlyDictionary<string, object?>? contextItems)
    {
        UserId = userId;
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        MailboxId = mailboxId;
        ConnectionId = connectionId;
        SessionKey = sessionKey;
        AgentName = agentName;
        ContextItems = contextItems;
    }

    /// <summary>
    /// Gets or sets the user identifier associated with the MCP call.
    /// </summary>

    public string? UserId { get; init; }

    /// <summary>
    /// Gets or sets the tenant identifier associated with the MCP call.
    /// </summary>

    public string? TenantId { get; init; }

    /// <summary>
    /// Gets or sets the workspace identifier associated with the MCP call.
    /// </summary>

    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Gets or sets the mailbox identifier associated with the MCP call.
    /// </summary>

    public string? MailboxId { get; init; }

    /// <summary>
    /// Gets or sets the connection identifier associated with the MCP call.
    /// </summary>

    public string? ConnectionId { get; init; }

    /// <summary>
    /// Gets or sets the agent session key associated with the MCP call.
    /// </summary>

    public string? SessionKey { get; init; }

    /// <summary>
    /// Gets or sets the agent name associated with the MCP call.
    /// </summary>

    public string? AgentName { get; init; }

    /// <summary>
    /// Gets or sets additional context values used by auth resolvers.
    /// </summary>

    public IReadOnlyDictionary<string, object?>? ContextItems { get; init; }
}
