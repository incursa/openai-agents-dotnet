using System.Net;
using System.Text;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>MCP JSON-RPC failures surface useful server exceptions.</summary>
public sealed class REQ_LIB_MCP_ERROR_001
{
    /// <summary>JSON-RPC server errors surface as helpful MCP server exceptions.</summary>
    /// <intent>Protect caller-facing diagnostics for MCP server-side failures.</intent>
    /// <scenario>LIB-MCP-ERROR-001</scenario>
    /// <behavior>JSON-RPC error responses throw `McpServerException` with the server message included.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task StreamableMcpClient_ThrowsHelpfulErrorForJsonRpcErrors()
    {
        RecordingHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":"1","error":{"code":-32603,"message":"server exploded"}}""", Encoding.UTF8, "application/json"),
        }));

        StreamableHttpMcpClient client = new(
            new HttpClient(handler),
            "mail",
            new Uri("https://example.test/mcp"));

        McpServerException error = await Assert.ThrowsAsync<McpServerException>(() => client.CallToolAsync("list_messages"));
        Assert.Contains("server exploded", error.Message);
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
