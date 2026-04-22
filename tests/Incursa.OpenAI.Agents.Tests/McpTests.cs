using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;
using ModelContextProtocol.Protocol;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for MCP discovery, auth, and tool invocation behavior.</summary>
public sealed class McpTests
{
    /// <summary>Streamable MCP requests apply dynamic auth headers and metadata on each request.</summary>
    /// <intent>Protect per-user MCP request shaping for hosted application scenarios.</intent>
    /// <scenario>LIB-MCP-AUTH-001</scenario>
    /// <behavior>Requests carry resolver-provided bearer tokens, dynamic headers, and metadata payload content.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    public async Task StreamableMcpClient_InsertsDynamicHeadersAndMetadataPerRequest()
    {
        List<(HttpRequestMessage Request, string Body)> recorded = new();
        RecordingHandler handler = new(async (request, _) =>
        {
            recorded.Add((CloneRequest(request), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync()));
            var payload = recorded.Count == 1
                ? """{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","description":"List messages","inputSchema":{"type":"object"}}]}}"""
                : """{"jsonrpc":"2.0","id":"1","result":{"content":[{"text":"ok"}]}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        });

        HttpClient httpClient = new(handler);
        DelegateMcpAuthResolver resolver = new(_ => new McpAuthResult(
            "token-123",
            new Dictionary<string, string>
            {
                ["X-Tenant"] = "tenant-a",
                ["X-User"] = "user-a",
            }));
        DelegateMcpMetadataResolver metadataResolver = new(_ => ValueTask.FromResult<JsonObject?>(new JsonObject
        {
            ["tenant_id"] = "tenant-a",
        }));

        StreamableMcpClientFactory factory = new(
            httpClient,
            resolver,
            () => new McpAuthContext { UserId = "user-a", TenantId = "tenant-a", SessionKey = "session-1" },
            metadataResolver);
        IStreamableMcpClient client = factory.Create(new StreamableHttpMcpServerDefinition("mail", new Uri("https://example.test/mcp")) { CacheToolsList = true });

        IReadOnlyList<McpToolDescriptor> tools = await client.ListToolsAsync();
        McpToolCallResult result = await client.CallToolAsync("list_messages", new JsonObject { ["folder"] = "inbox" });

        Assert.Single(tools);
        Assert.Equal("ok", result.Text);
        Assert.Equal(2, recorded.Count);
        Assert.Equal("Bearer token-123", recorded[0].Request.Headers.Authorization?.ToString());
        Assert.Contains(recorded[0].Request.Headers, header => header.Key == "X-Tenant" && header.Value.Contains("tenant-a"));
        Assert.Contains(recorded[0].Request.Headers, header => header.Key == "X-User" && header.Value.Contains("user-a"));
        Assert.Contains("\"_meta\"", recorded[1].Body);
        Assert.Contains("tenant-a", recorded[1].Body);
    }

    /// <summary>Tool discovery is cached when MCP tool-list caching is enabled.</summary>
    /// <intent>Protect the caching contract for streamable MCP clients.</intent>
    /// <scenario>LIB-MCP-CACHE-001</scenario>
    /// <behavior>Repeated tool discovery calls reuse the cached result instead of issuing a second HTTP request.</behavior>
    [Fact]
    public async Task StreamableMcpClient_CachesToolListWhenEnabled()
    {
        var calls = 0;
        RecordingHandler handler = new((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","inputSchema":{"type":"object"}}]}}""", Encoding.UTF8, "application/json"),
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
            true,
            null);

        await client.ListToolsAsync();
        await client.ListToolsAsync();

        Assert.Equal(1, calls);
    }

    /// <summary>Resource APIs round-trip cursor parameters and return typed protocol payloads.</summary>
    /// <intent>Protect the streamable MCP resource surface added alongside tool discovery.</intent>
    /// <scenario>LIB-MCP-RESOURCES-001</scenario>
    /// <behavior>Resource list, resource template list, and resource read calls send the expected JSON-RPC methods and deserialize the typed protocol responses.</behavior>
    [Fact]
    public async Task StreamableMcpClient_ListsAndReadsResourcesWithCursors()
    {
        List<JsonObject> requests = new();
        int callCount = 0;
        RecordingHandler handler = new(async (request, _) =>
        {
            requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsObject());
            callCount++;

            string payload = callCount switch
            {
                1 => """{"jsonrpc":"2.0","id":"1","result":{"resources":[{"name":"doc","uri":"file:///docs/doc.txt","mimeType":"text/plain","description":"Doc","title":"Document"}],"nextCursor":"cursor-2"}}""",
                2 => """{"jsonrpc":"2.0","id":"1","result":{"resourceTemplates":[{"name":"template","uriTemplate":"file:///docs/{name}.txt","mimeType":"text/plain","description":"Template","title":"Template","isTemplated":true}],"nextCursor":"cursor-3"}}""",
                _ => """{"jsonrpc":"2.0","id":"1","result":{"contents":[{"uri":"file:///docs/doc.txt","mimeType":"text/plain","text":"hello world"}]}}""",
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        });

        StreamableHttpMcpClient client = new(
            new HttpClient(handler),
            "docs",
            new Uri("https://example.test/mcp"));

        ListResourcesResult resources = await client.ListResourcesAsync("cursor-1");
        ListResourceTemplatesResult templates = await client.ListResourceTemplatesAsync("cursor-2");
        ReadResourceResult resource = await client.ReadResourceAsync("file:///docs/doc.txt");

        Assert.Equal(3, requests.Count);
        Assert.Equal("resources/list", requests[0]["method"]?.GetValue<string>());
        Assert.Equal("cursor-1", requests[0]["params"]?["cursor"]?.GetValue<string>());
        Assert.Equal("resources/templates/list", requests[1]["method"]?.GetValue<string>());
        Assert.Equal("cursor-2", requests[1]["params"]?["cursor"]?.GetValue<string>());
        Assert.Equal("resources/read", requests[2]["method"]?.GetValue<string>());
        Assert.Equal("file:///docs/doc.txt", requests[2]["params"]?["uri"]?.GetValue<string>());

        Assert.Single(resources.Resources);
        Assert.Equal("cursor-2", resources.NextCursor);
        Assert.Single(templates.ResourceTemplates);
        Assert.Equal("cursor-3", templates.NextCursor);
        TextResourceContents content = Assert.IsType<TextResourceContents>(Assert.Single(resource.Contents));
        Assert.Equal("hello world", content.Text);
    }

    /// <summary>Convenience overloads forward expected defaults to the token-aware MCP client contract.</summary>
    /// <intent>Protect the extension-method shims added around streamable MCP clients.</intent>
    /// <scenario>LIB-MCP-RESOURCES-001</scenario>
    /// <behavior>Extension overloads forward null/default cursors, URIs, arguments, and `CancellationToken.None` to the underlying client methods.</behavior>
    [Fact]
    public async Task StreamableMcpClient_ExtensionOverloadsForwardExpectedDefaults()
    {
        RecordingStreamableClient client = new();
        JsonObject arguments = new() { ["query"] = "invoice" };

        IReadOnlyList<McpToolDescriptor> tools = await client.ListToolsAsync();
        ListResourcesResult resourcesWithoutCursor = await client.ListResourcesAsync();
        ListResourcesResult resourcesWithCursor = await client.ListResourcesAsync("cursor-1");
        ListResourceTemplatesResult templatesWithoutCursor = await client.ListResourceTemplatesAsync();
        ListResourceTemplatesResult templatesWithCursor = await client.ListResourceTemplatesAsync("cursor-2");
        ReadResourceResult readResult = await client.ReadResourceAsync("file:///docs/doc.txt");
        McpToolCallResult toolWithoutArguments = await client.CallToolAsync("search_mail");
        McpToolCallResult toolWithArguments = await client.CallToolAsync("search_mail", arguments);

        Assert.Same(client.Tools, tools);
        Assert.Same(client.ResourcesResult, resourcesWithoutCursor);
        Assert.Same(client.ResourcesResult, resourcesWithCursor);
        Assert.Same(client.ResourceTemplatesResult, templatesWithoutCursor);
        Assert.Same(client.ResourceTemplatesResult, templatesWithCursor);
        Assert.Same(client.ReadResult, readResult);
        Assert.Same(client.ToolCallResult, toolWithoutArguments);
        Assert.Same(client.ToolCallResult, toolWithArguments);

        Assert.Collection(
            client.Invocations,
            invocation =>
            {
                Assert.Equal("ListTools", invocation.Method);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("ListResources", invocation.Method);
                Assert.Null(invocation.Cursor);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("ListResources", invocation.Method);
                Assert.Equal("cursor-1", invocation.Cursor);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("ListResourceTemplates", invocation.Method);
                Assert.Null(invocation.Cursor);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("ListResourceTemplates", invocation.Method);
                Assert.Equal("cursor-2", invocation.Cursor);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("ReadResource", invocation.Method);
                Assert.Equal("file:///docs/doc.txt", invocation.Uri);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("CallTool", invocation.Method);
                Assert.Equal("search_mail", invocation.ToolName);
                Assert.Null(invocation.Arguments);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            },
            invocation =>
            {
                Assert.Equal("CallTool", invocation.Method);
                Assert.Equal("search_mail", invocation.ToolName);
                Assert.Same(arguments, invocation.Arguments);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            });
    }

    /// <summary>Transient MCP failures retry and emit observations when retry settings allow it.</summary>
    /// <intent>Protect resiliency and observation behavior for MCP tool calls.</intent>
    /// <scenario>LIB-MCP-RETRY-001</scenario>
    /// <behavior>Retryable failures schedule a retry, eventually succeed, and emit retry and success observations.</behavior>
    [Fact]
    public async Task StreamableMcpClient_RetriesTransientHttpFailuresAndObservesAttempts()
    {
        var calls = 0;
        List<McpClientObservation> observations = new();
        RecordingHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("""{"jsonrpc":"2.0","id":"1","error":{"code":503,"message":"busy"}}""", Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":"1","result":{"content":[{"text":"ok"}]}}""", Encoding.UTF8, "application/json"),
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
                RetryCount = 1,
                RetryDelay = TimeSpan.Zero,
                Observer = new RecordingObserver(observations),
            });

        McpToolCallResult result = await client.CallToolAsync("list_messages");

        Assert.Equal("ok", result.Text);
        Assert.Equal(2, calls);
        Assert.Contains(observations, obs => obs.Outcome == McpCallOutcome.RetryScheduled);
        Assert.Contains(observations, obs => obs.Outcome == McpCallOutcome.Success);
    }

    private sealed class DelegateMcpAuthResolver : IUserScopedMcpAuthResolver
    {
        private readonly Func<McpAuthContext, McpAuthResult> handler;

        public DelegateMcpAuthResolver(Func<McpAuthContext, McpAuthResult> handler)
        {
            this.handler = handler;
        }

        public ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(handler(context));
    }

    private sealed class DelegateMcpMetadataResolver : IMcpToolMetadataResolver
    {
        private readonly Func<McpToolMetadataContext, ValueTask<JsonObject?>> handler;

        public DelegateMcpMetadataResolver(Func<McpToolMetadataContext, ValueTask<JsonObject?>> handler)
        {
            this.handler = handler;
        }

        public ValueTask<JsonObject?> ResolveAsync(McpToolMetadataContext context, CancellationToken cancellationToken)
            => handler(context);
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

    private sealed class RecordingObserver : IMcpClientObserver
    {
        private readonly List<McpClientObservation> observations;

        public RecordingObserver(List<McpClientObservation> observations)
        {
            this.observations = observations;
        }

        public ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken)
        {
            observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingStreamableClient : IStreamableMcpClient
    {
        public Uri ServerUrl { get; } = new("https://example.test/mcp");

        public string ServerLabel { get; } = "mail";

        public IReadOnlyList<McpToolDescriptor> Tools { get; } = [new McpToolDescriptor("search_mail")];

        public ListResourcesResult ResourcesResult { get; } = new();

        public ListResourceTemplatesResult ResourceTemplatesResult { get; } = new();

        public ReadResourceResult ReadResult { get; } = new();

        public McpToolCallResult ToolCallResult { get; } = new("ok");

        public List<McpInvocation> Invocations { get; } = [];

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        {
            Invocations.Add(new McpInvocation("ListTools", null, null, null, null, cancellationToken));
            return Task.FromResult(Tools);
        }

        public Task<ListResourcesResult> ListResourcesAsync(string? cursor, CancellationToken cancellationToken)
        {
            Invocations.Add(new McpInvocation("ListResources", cursor, null, null, null, cancellationToken));
            return Task.FromResult(ResourcesResult);
        }

        public Task<ListResourceTemplatesResult> ListResourceTemplatesAsync(string? cursor, CancellationToken cancellationToken)
        {
            Invocations.Add(new McpInvocation("ListResourceTemplates", cursor, null, null, null, cancellationToken));
            return Task.FromResult(ResourceTemplatesResult);
        }

        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            Invocations.Add(new McpInvocation("ReadResource", null, uri, null, null, cancellationToken));
            return Task.FromResult(ReadResult);
        }

        public Task<McpToolCallResult> CallToolAsync(string toolName, JsonNode? arguments, CancellationToken cancellationToken)
        {
            Invocations.Add(new McpInvocation("CallTool", null, null, toolName, arguments, cancellationToken));
            return Task.FromResult(ToolCallResult);
        }
    }

    private sealed record McpInvocation(
        string Method,
        string? Cursor,
        string? Uri,
        string? ToolName,
        JsonNode? Arguments,
        CancellationToken CancellationToken);

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
