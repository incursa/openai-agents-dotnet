using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Calls the OpenAI Responses API over HTTP.
/// </summary>

public sealed class OpenAiResponsesClient : IOpenAiResponsesClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly string responsesPath;

    /// <summary>Creates a client that uses the default responses path.</summary>
    public OpenAiResponsesClient(HttpClient httpClient)
        : this(httpClient, "v1/responses")
    {
    }

    /// <summary>Creates a client for the specified responses endpoint path.</summary>
    public OpenAiResponsesClient(HttpClient httpClient, string responsesPath)
    {
        this.httpClient = httpClient;
        this.responsesPath = responsesPath;
    }

    /// <summary>Creates a non-streamed response.</summary>
    public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request)
        => CreateResponseAsync(request, CancellationToken.None);

    /// <summary>Creates a non-streamed response using the supplied cancellation token.</summary>
    public async Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(responsesPath, request.Body, SerializerOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        JsonObject raw = await response.Content.ReadFromJsonAsync<JsonObject>(SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? new JsonObject();
        var id = raw["id"]?.GetValue<string>() ?? string.Empty;
        return new OpenAiResponsesResponse(id, raw);
    }

    /// <summary>Streams raw response events.</summary>
    public IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request)
        => StreamResponseAsync(request, CancellationToken.None);

    /// <summary>Streams raw response events using the supplied cancellation token.</summary>
    public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Clone once per stream call so we can set the stream flag without mutating caller-owned request object.
        JsonObject body = request.Body.DeepClone() as JsonObject ?? new JsonObject();
        body["stream"] = true;

        using HttpRequestMessage message = new(HttpMethod.Post, responsesPath)
        {
            Content = JsonContent.Create(body, options: SerializerOptions),
        };
        message.Headers.Accept.ParseAdd("text/event-stream");

        using HttpResponseMessage response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(stream, Encoding.UTF8);

        // Parse SSE stream incrementally and emit model events until DONE or EOF.
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                yield break;
            }

            JsonObject? data;
            try
            {
                // Only emit lines that are valid JSON objects; ignore malformed fragments.
                data = JsonNode.Parse(payload) as JsonObject;
            }
            catch
            {
                continue;
            }

            if (data is null)
            {
                continue;
            }

            yield return new OpenAiResponsesStreamEvent(data["type"]?.GetValue<string>() ?? "unknown", data);
        }
    }
}
