namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Describes a streamable MCP server exposed to an agent.
/// </summary>

public sealed record StreamableHttpMcpServerDefinition
{
    /// <summary>
    /// Creates a server definition from its label and URL.
    /// </summary>

    public StreamableHttpMcpServerDefinition(string serverLabel, Uri serverUrl)
        : this(serverLabel, serverUrl, null, false, null, null, null, false)
    {
    }

    /// <summary>
    /// Creates a server definition with explicit connector, approval, and filtering settings.
    /// </summary>
    public StreamableHttpMcpServerDefinition(
        string serverLabel,
        Uri serverUrl,
        string? connectorId,
        bool approvalRequired,
        IReadOnlyDictionary<string, string>? headers,
        string? description,
        McpToolFilter? toolFilter,
        bool cacheToolsList)
    {
        ServerLabel = serverLabel;
        ServerUrl = serverUrl;
        ConnectorId = connectorId;
        ApprovalRequired = approvalRequired;
        Headers = headers;
        Description = description;
        ToolFilter = toolFilter;
        CacheToolsList = cacheToolsList;
    }

    /// <summary>
    /// Gets or sets the label used to identify the MCP server.
    /// </summary>

    public string ServerLabel { get; init; }

    /// <summary>
    /// Gets or sets the URL used to contact the MCP server.
    /// </summary>

    public Uri ServerUrl { get; init; }

    /// <summary>
    /// Gets or sets the connector identifier associated with the server.
    /// </summary>

    public string? ConnectorId { get; init; }

    /// <summary>
    /// Gets or sets whether tool calls from this server require approval.
    /// </summary>

    public bool ApprovalRequired { get; init; }

    /// <summary>
    /// Gets or sets static headers applied to requests for this server.
    /// </summary>

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets or sets the human-readable description for the server.
    /// </summary>

    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the filter used to limit discovered tools.
    /// </summary>

    public McpToolFilter? ToolFilter { get; init; }

    /// <summary>
    /// Gets or sets whether tool discovery results are cached.
    /// </summary>

    public bool CacheToolsList { get; init; }
}
