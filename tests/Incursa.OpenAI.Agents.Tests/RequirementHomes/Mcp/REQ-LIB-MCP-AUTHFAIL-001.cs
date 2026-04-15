using System.Net;
using System.Text;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>MCP authentication failures stop immediately and do not retry.</summary>
public sealed class REQ_LIB_MCP_AUTHFAIL_001
{
    /// <summary>Authentication failures do not retry and surface as `McpAuthenticationException`.</summary>
    /// <intent>Protect correct classification of non-retryable MCP auth failures.</intent>
    /// <scenario>LIB-MCP-AUTHFAIL-001</scenario>
    /// <behavior>Unauthorized responses throw `McpAuthenticationException` without performing additional attempts.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task StreamableMcpClient_DoesNotRetryAuthenticationFailures()
    {
        var calls = 0;
        RecordingHandler handler = new((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":"1","error":{"code":401,"message":"nope"}}""", Encoding.UTF8, "application/json"),
            });
        });

        StreamableHttpMcpClient client = new(
            new HttpClient(handler),
            "mail",
            new Uri("https://example.test/mcp"),
            null,
            null,
            null,
            null,
            null,
            false,
            new McpClientOptions
            {
                RetryCount = 3,
                RetryDelay = TimeSpan.Zero,
            });

        await Assert.ThrowsAsync<McpAuthenticationException>(() => client.CallToolAsync("list_messages"));
        Assert.Equal(1, calls);
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
