using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>MCP tool filtering keeps allowed tools and drops blocked tools safely.</summary>
public sealed class REQ_LIB_MCP_FILTER_001
{
    /// <summary>Dynamic tool filters can hide tools and tolerate filter failures without leaking blocked tools into the final tool list.</summary>
    /// <intent>Protect runtime tool visibility filtering for hosted MCP discovery.</intent>
    /// <scenario>LIB-MCP-FILTER-001</scenario>
    /// <behavior>Allowed tools remain visible while blocked tools and filter failures are excluded from the final tool list.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task StreamableMcpClient_AppliesDynamicToolFilterAndSkipsFilterErrors()
    {
        RecordingHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"allowed_tool","inputSchema":{"type":"object"}},{"name":"error_tool","inputSchema":{"type":"object"}},{"name":"blocked_tool","inputSchema":{"type":"object"}}]}}""", Encoding.UTF8, "application/json"),
        }));

        StreamableHttpMcpClient client = new(
            new HttpClient(handler),
            "mail",
            new Uri("https://example.test/mcp"),
            null,
            null,
            null,
            null,
            new McpToolFilter
            {
                FilterAsync = (ctx, tool, _) =>
                {
                    Assert.Equal("mail", ctx.ServerLabel);
                    return tool.Name switch
                    {
                        "allowed_tool" => ValueTask.FromResult(true),
                        "blocked_tool" => ValueTask.FromResult(false),
                        _ => throw new InvalidOperationException("bad filter"),
                    };
                },
            },
            false,
            null);

        IReadOnlyList<McpToolDescriptor> tools = await client.ListToolsAsync();

        McpToolDescriptor allowed = Assert.Single(tools);
        Assert.Equal("allowed_tool", allowed.Name);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
