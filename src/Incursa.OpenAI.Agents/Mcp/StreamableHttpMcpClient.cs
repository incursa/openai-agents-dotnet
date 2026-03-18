using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Mcp;

/// <summary>
/// Implements client operations for StreamableHttpMcp.
/// </summary>

public sealed class StreamableHttpMcpClient : IStreamableMcpClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly IReadOnlyDictionary<string, string> staticHeaders;
    private readonly IUserScopedMcpAuthResolver? authResolver;
    private readonly IMcpToolMetadataResolver? metadataResolver;
    private readonly McpToolFilter? toolFilter;
    private readonly bool cacheToolsList;
    private readonly McpAuthContext authContext;
    private readonly McpClientOptions options;
    private IReadOnlyList<McpToolDescriptor>? cachedTools;

    public StreamableHttpMcpClient(
        HttpClient httpClient,
        string serverLabel,
        Uri serverUrl)
        : this(httpClient, serverLabel, serverUrl, null, null, null, null, null, false, null)
    {
    }

    public StreamableHttpMcpClient(
        HttpClient httpClient,
        string serverLabel,
        Uri serverUrl,
        IReadOnlyDictionary<string, string>? staticHeaders,
        IUserScopedMcpAuthResolver? authResolver,
        McpAuthContext? authContext,
        IMcpToolMetadataResolver? metadataResolver,
        McpToolFilter? toolFilter,
        bool cacheToolsList,
        McpClientOptions? options)
    {
        this.httpClient = httpClient;
        ServerLabel = serverLabel;
        ServerUrl = serverUrl;
        this.staticHeaders = staticHeaders ?? new Dictionary<string, string>(StringComparer.Ordinal);
        this.authResolver = authResolver;
        this.authContext = authContext ?? new McpAuthContext();
        this.metadataResolver = metadataResolver;
        this.toolFilter = toolFilter;
        this.cacheToolsList = cacheToolsList;
        this.options = options ?? new McpClientOptions();
    }

    /// <summary>
    /// Gets the MCP server URL.
    /// </summary>

    public Uri ServerUrl { get; }

    /// <summary>
    /// Gets the label used to identify the MCP server.
    /// </summary>

    public string ServerLabel { get; }

    /// <summary>
    /// Lists tools without requiring an explicit cancellation token.
    /// </summary>

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync()
        => ListToolsAsync(CancellationToken.None);

    /// <summary>
    /// Lists tools using the supplied cancellation token.
    /// </summary>

    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
    {
        // Reuse cached tool metadata if enabled and already available.
        if (cacheToolsList && cachedTools is not null)
        {
            return cachedTools;
        }

        JsonObject? response = await SendAsync("tools/list", null, new StreamableHttpMcpRequest("tools/list", new JsonObject()), cancellationToken).ConfigureAwait(false);

        if (response?["tools"] is not JsonArray toolsArray)
        {
            return Array.Empty<McpToolDescriptor>();
        }

        var tools = new List<McpToolDescriptor>();
        foreach (JsonObject toolNode in toolsArray.OfType<JsonObject>())
        {
            // Convert raw tool RPC payload into a strongly typed descriptor.
            var descriptor = new McpToolDescriptor(
                toolNode["name"]?.GetValue<string>() ?? string.Empty,
                toolNode["description"]?.GetValue<string>(),
                toolNode["inputSchema"],
                toolNode["approval_required"]?.GetValue<bool>() ?? false);

            try
            {
                // Dynamic tool filters can short-circuit noisy tools; failures are intentionally non-fatal.
                if (toolFilter is null || await toolFilter.AllowsAsync(ServerLabel, authContext, descriptor, cancellationToken).ConfigureAwait(false))
                {
                    tools.Add(descriptor);
                }
            }
            catch
            {
                // Dynamic filter failures should not take down tool discovery.
            }
        }

        // Persist filter result when cache is enabled to avoid repeated list calls in the same executor lifetime.
        if (cacheToolsList)
        {
            cachedTools = tools;
        }

        return tools;
    }

    /// <summary>
    /// Calls a tool without requiring an explicit cancellation token.
    /// </summary>

    public Task<McpToolCallResult> CallToolAsync(string toolName)
        => CallToolAsync(toolName, null, CancellationToken.None);

    /// <summary>
    /// Calls a tool with arguments without requiring an explicit cancellation token.
    /// </summary>

    public Task<McpToolCallResult> CallToolAsync(string toolName, JsonNode? arguments)
        => CallToolAsync(toolName, arguments, CancellationToken.None);

    /// <summary>
    /// Calls a tool with arguments using the supplied cancellation token.
    /// </summary>

    public async Task<McpToolCallResult> CallToolAsync(string toolName, JsonNode? arguments, CancellationToken cancellationToken)
    {
        JsonNode? mergedArgs = arguments?.DeepClone();

        // Inject optional metadata (for tracing/authorization context) before forwarding the tool invocation.
        if (metadataResolver is not null)
        {
            JsonObject? meta = await metadataResolver.ResolveAsync(new McpToolMetadataContext(ServerLabel, toolName, authContext, arguments), cancellationToken).ConfigureAwait(false);
            if (meta is not null)
            {
                JsonObject obj = mergedArgs as JsonObject ?? new JsonObject();
                obj["_meta"] = meta;
                mergedArgs = obj;
            }
        }

        JsonObject? response = await SendAsync(
            "tools/call",
            toolName,
            new StreamableHttpMcpRequest("tools/call", new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = mergedArgs,
            }),
            cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            return new McpToolCallResult();
        }

        string? text = null;

        // Normalize tool response content arrays into a single text blob for human-readable run item display.
        if (response["content"] is JsonArray contentArray)
        {
            var parts = contentArray
                .OfType<JsonObject>()
                .Select(item => item["text"]?.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (parts.Length > 0)
            {
                text = string.Join(Environment.NewLine, parts);
            }
        }

        return new McpToolCallResult(text, response);
    }

    private async Task<JsonObject?> SendAsync(
        string method,
        string? toolName,
        StreamableHttpMcpRequest request,
        CancellationToken cancellationToken)
    {
        // Retry loop with bounded attempts and adaptive outcome observation hooks.
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using HttpRequestMessage message = await CreateRequestMessageAsync(request, cancellationToken).ConfigureAwait(false);
            DateTimeOffset start = DateTimeOffset.UtcNow;

            try
            {
                using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                var payloadText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                JsonObject? payload = null;
                if (!string.IsNullOrWhiteSpace(payloadText))
                {
                    try
                    {
                        payload = JsonNode.Parse(payloadText) as JsonObject;
                    }
                    catch
                    {
                        payload = null;
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // Auth failures are terminal for this call and surfaced as explicit auth exceptions.
                    var authException = new McpAuthenticationException(
                        ServerLabel,
                        method,
                        toolName,
                        $"MCP server '{ServerLabel}' rejected authentication for method '{method}'.",
                        response.StatusCode);
                    await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.AuthenticationFailure, response.StatusCode, authException.Message, cancellationToken).ConfigureAwait(false);
                    throw authException;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Retry only configured transient failures; otherwise map to server exception and fail fast.
                    if (IsRetryableStatusCode(response.StatusCode) && attempt <= options.RetryCount)
                    {
                        await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.RetryScheduled, response.StatusCode, $"Retrying HTTP {(int)response.StatusCode}.", cancellationToken).ConfigureAwait(false);
                        await Task.Delay(options.RetryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var serverException = new McpServerException(
                        ServerLabel,
                        method,
                        toolName,
                        $"MCP server '{ServerLabel}' returned HTTP {(int)response.StatusCode}: {payloadText}",
                        response.StatusCode);
                    await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.ServerFailure, response.StatusCode, serverException.Message, cancellationToken).ConfigureAwait(false);
                    throw serverException;
                }

                if (payload?["error"] is JsonObject error)
                {
                    // JSON-RPC error envelopes are mapped to server exceptions with error code context.
                    var code = error["code"]?.GetValue<int?>();
                    var messageText = error["message"]?.GetValue<string>() ?? "Unknown MCP error.";
                    var serverException = new McpServerException(
                        ServerLabel,
                        method,
                        toolName,
                        $"MCP server '{ServerLabel}' returned JSON-RPC error {code}: {messageText}",
                        response.StatusCode,
                        code);
                    await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.ServerFailure, response.StatusCode, serverException.Message, cancellationToken).ConfigureAwait(false);
                    throw serverException;
                }

                await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.Success, response.StatusCode, null, cancellationToken).ConfigureAwait(false);
                return payload?["result"] as JsonObject;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt, cancellationToken))
            {
                await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.RetryScheduled, null, ex.Message, cancellationToken).ConfigureAwait(false);
                await Task.Delay(options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
            {
                var transportException = new McpTransportException(
                    ServerLabel,
                    method,
                    toolName,
                    $"MCP transport failed for server '{ServerLabel}' and method '{method}'.",
                    ex);
                await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.TransportFailure, null, transportException.Message, cancellationToken).ConfigureAwait(false);
                throw transportException;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                var transportException = new McpTransportException(
                    ServerLabel,
                    method,
                    toolName,
                    $"MCP request timed out for server '{ServerLabel}' and method '{method}'.",
                    ex);
                await ObserveAsync(method, toolName, attempt, start, McpCallOutcome.TransportFailure, null, transportException.Message, cancellationToken).ConfigureAwait(false);
                throw transportException;
            }
        }
    }

    private async Task<HttpRequestMessage> CreateRequestMessageAsync(StreamableHttpMcpRequest request, CancellationToken cancellationToken)
    {
        // Build a JSON-RPC request and combine static headers + resolved auth resolver headers.
        var message = new HttpRequestMessage(HttpMethod.Post, ServerUrl)
        {
            Content = JsonContent.Create(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = request.Id ?? Guid.NewGuid().ToString("n"),
                ["method"] = request.Method,
                ["params"] = request.Params?.DeepClone(),
            }, options: SerializerOptions),
        };

        foreach (KeyValuePair<string, string> pair in staticHeaders)
        {
            message.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }

        if (authResolver is not null)
        {
            McpAuthResult auth = await authResolver.ResolveAsync(authContext, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(auth.BearerToken))
            {
                message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.BearerToken);
            }

            if (auth.Headers is not null)
            {
                foreach (KeyValuePair<string, string> pair in auth.Headers)
                {
                    message.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
                }
            }
        }

        return message;
    }

    private bool ShouldRetry(Exception exception, int attempt, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || attempt > options.RetryCount)
        {
            return false;
        }

        // Retry policy currently covers transient transport failures and caller-side cancellation-independent timeouts.
        return exception is HttpRequestException
            || exception is TaskCanceledException;
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            || (int)statusCode >= 500;

    private async ValueTask ObserveAsync(
        string method,
        string? toolName,
        int attempt,
        DateTimeOffset start,
        McpCallOutcome outcome,
        HttpStatusCode? statusCode,
        string? detail,
        CancellationToken cancellationToken)
    {
        if (options.Observer is null)
        {
            return;
        }

        try
        {
            await options.Observer.ObserveAsync(
                new McpClientObservation(ServerLabel, method, toolName, attempt, DateTimeOffset.UtcNow - start, outcome, statusCode, detail),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Observability must not break MCP execution.
        }
    }
}
