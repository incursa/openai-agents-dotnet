namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Describes a hosted MCP tool exposed through OpenAI Responses.
/// </summary>

public sealed record HostedMcpToolDefinition
{
    /// <summary>
    /// Creates a hosted MCP definition that only specifies the server label.
    /// </summary>

    public HostedMcpToolDefinition(string serverLabel)
        : this(serverLabel, null, null, null, false, null, null, null)
    {
    }

    /// <summary>
    /// Creates a hosted MCP definition with the supplied server and authorization settings.
    /// </summary>
    public HostedMcpToolDefinition(
        string serverLabel,
        Uri? serverUrl,
        string? connectorId,
        string? authorization,
        bool approvalRequired,
        IReadOnlyDictionary<string, string>? headers,
        string? approvalReason,
        string? description)
    {
        ServerLabel = serverLabel;
        ServerUrl = serverUrl;
        ConnectorId = connectorId;
        Authorization = authorization;
        ApprovalRequired = approvalRequired;
        Headers = headers;
        ApprovalReason = approvalReason;
        Description = description;
    }

    /// <summary>
    /// Gets or sets the server label used to identify the hosted MCP server.
    /// </summary>

    public string ServerLabel { get; init; }

    /// <summary>
    /// Gets or sets the server endpoint used for hosted MCP calls.
    /// </summary>

    public Uri? ServerUrl { get; init; }

    /// <summary>
    /// Gets or sets the connector identifier associated with the hosted server.
    /// </summary>

    public string? ConnectorId { get; init; }

    /// <summary>
    /// Gets or sets the authorization header value used for hosted MCP calls.
    /// </summary>

    public string? Authorization { get; init; }

    /// <summary>
    /// Gets or sets whether tool calls require approval before execution.
    /// </summary>

    public bool ApprovalRequired { get; init; }

    /// <summary>
    /// Gets or sets static headers applied to hosted MCP requests.
    /// </summary>

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets or sets the reason shown when approval is required.
    /// </summary>

    public string? ApprovalReason { get; init; }

    /// <summary>
    /// Gets or sets the human-readable description of the hosted MCP server.
    /// </summary>

    public string? Description { get; init; }
}
