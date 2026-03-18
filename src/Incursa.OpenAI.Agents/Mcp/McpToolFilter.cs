namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Configures allow and deny filtering for discovered MCP tools.
/// </summary>

public sealed record McpToolFilter
{
    /// <summary>
    /// Creates a filter that allows every tool by default.
    /// </summary>

    public McpToolFilter()
        : this(null, null, null)
    {
    }

    public McpToolFilter(
        IReadOnlyCollection<string>? includeNames,
        IReadOnlyCollection<string>? excludeNames,
        Func<McpToolFilterContext, McpToolDescriptor, CancellationToken, ValueTask<bool>>? filterAsync)
    {
        IncludeNames = includeNames;
        ExcludeNames = excludeNames;
        FilterAsync = filterAsync;
    }

    /// <summary>
    /// Gets or sets the tool names that are explicitly allowed.
    /// </summary>

    public IReadOnlyCollection<string>? IncludeNames { get; init; }

    /// <summary>
    /// Gets or sets the tool names that are explicitly denied.
    /// </summary>

    public IReadOnlyCollection<string>? ExcludeNames { get; init; }

    /// <summary>
    /// Gets or sets the custom async filter applied after name checks.
    /// </summary>

    public Func<McpToolFilterContext, McpToolDescriptor, CancellationToken, ValueTask<bool>>? FilterAsync { get; init; }

    /// <summary>
    /// Evaluates whether a discovered tool should be exposed to the agent.
    /// </summary>

    public async ValueTask<bool> AllowsAsync(string serverLabel, McpAuthContext authContext, McpToolDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (IncludeNames is { Count: > 0 } && !IncludeNames.Contains(descriptor.Name, StringComparer.Ordinal))
        {
            return false;
        }

        if (ExcludeNames is { Count: > 0 } && ExcludeNames.Contains(descriptor.Name, StringComparer.Ordinal))
        {
            return false;
        }

        if (FilterAsync is not null)
        {
            return await FilterAsync(new McpToolFilterContext(serverLabel, authContext), descriptor, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
