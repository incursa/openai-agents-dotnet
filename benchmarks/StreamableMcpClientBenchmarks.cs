using BenchmarkDotNet.Attributes;
using Incursa.OpenAI.Agents.Mcp;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Benchmarks;

/// <summary>Benchmarks the streamable MCP discovery and tool-call paths.</summary>
[MemoryDiagnoser]
public class StreamableMcpClientBenchmarks
{
    private StreamableHttpMcpClient client = default!;

    [GlobalSetup]
    public void Setup()
    {
        client = new StreamableHttpMcpClient(
            new HttpClient(new BenchmarkHandler()),
            "mail",
            new Uri("https://example.test/mcp"),
            null,
            null,
            null,
            null,
            null,
            false,
            null);
    }

    [Benchmark]
    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync()
        => client.ListToolsAsync();

    [Benchmark]
    public Task<McpToolCallResult> CallToolAsync()
        => client.CallToolAsync("list_messages", new JsonObject { ["folder"] = "inbox" });

    private sealed class BenchmarkHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            string payload = body.Contains("\"tools/call\"", StringComparison.Ordinal)
                ? """{"jsonrpc":"2.0","id":"1","result":{"content":[{"text":"ok"}]}}"""
                : """{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","description":"List messages","inputSchema":{"type":"object"}}]}}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }
    }
}
