#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Calls the OpenAI Responses API via the official OpenAI .NET SDK.
/// </summary>

public sealed class OpenAiResponsesClient : IOpenAiResponsesClient
{
    private readonly ResponsesClient responsesClient;

    /// <summary>Creates a client that uses the default responses path.</summary>
    public OpenAiResponsesClient(HttpClient httpClient)
        : this(httpClient, "v1/responses")
    {
    }

    /// <summary>Creates a client for the specified responses endpoint path.</summary>
    public OpenAiResponsesClient(HttpClient httpClient, string responsesPath)
        : this(CreateSdkClient(httpClient, responsesPath))
    {
    }

    internal OpenAiResponsesClient(ResponsesClient responsesClient)
    {
        this.responsesClient = responsesClient;
    }

    /// <summary>Creates a non-streamed response.</summary>
    public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request)
        => CreateResponseAsync(request, CancellationToken.None);

    /// <summary>Creates a non-streamed response using the supplied cancellation token.</summary>
    public async Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
    {
        CreateResponseOptions options = GetRequestOptions(request, stream: false);

        try
        {
            ClientResult<ResponseResult> result = await responsesClient.CreateResponseAsync(options, cancellationToken).ConfigureAwait(false);
            return new OpenAiResponsesResponse(result.Value);
        }
        catch (ClientResultException ex)
        {
            throw CreateDetailedException("create response", ex);
        }
    }

    /// <summary>Streams raw response events.</summary>
    public IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request)
        => StreamResponseAsync(request, CancellationToken.None);

    /// <summary>Streams raw response events using the supplied cancellation token.</summary>
    public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CreateResponseOptions options = GetRequestOptions(request, stream: true);

        AsyncCollectionResult<StreamingResponseUpdate> updates;
        try
        {
            updates = responsesClient.CreateResponseStreamingAsync(options, cancellationToken);
        }
        catch (ClientResultException ex)
        {
            throw CreateDetailedException("start response stream", ex);
        }

        await foreach (StreamingResponseUpdate update in updates.ConfigureAwait(false))
        {
            yield return new OpenAiResponsesStreamEvent(update);
        }
    }

    private static ResponsesClient CreateSdkClient(HttpClient httpClient, string responsesPath)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        OpenAIClientOptions options = new()
        {
            Endpoint = ResolveEndpoint(httpClient.BaseAddress, responsesPath),
            Transport = new HttpClientPipelineTransport(httpClient),
        };

        AuthenticationHeaderValue? authorization = httpClient.DefaultRequestHeaders.Authorization;
        if (authorization is null || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new InvalidOperationException("OpenAiResponsesClient requires an HttpClient with a bearer Authorization header when constructed from HttpClient.");
        }

        return new ResponsesClient(new ApiKeyCredential(authorization.Parameter), options);
    }

    private static Uri ResolveEndpoint(Uri? baseAddress, string responsesPath)
    {
        if (baseAddress is null)
        {
            return new Uri("https://api.openai.com/v1");
        }

        string normalizedPath = responsesPath.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return baseAddress;
        }

        if (normalizedPath.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            string prefix = normalizedPath[..^"/responses".Length];
            return string.IsNullOrWhiteSpace(prefix)
                ? baseAddress
                : new Uri(baseAddress, prefix.Trim('/') + "/");
        }

        if (string.Equals(normalizedPath, "responses", StringComparison.OrdinalIgnoreCase))
        {
            return baseAddress;
        }

        throw new NotSupportedException($"Responses path '{responsesPath}' is not supported by the official OpenAI .NET client. Expected a path ending in '/responses'.");
    }

    private static CreateResponseOptions GetRequestOptions(OpenAiResponsesRequest request, bool stream)
    {
        ArgumentNullException.ThrowIfNull(request);

        CreateResponseOptions options = request.Options
            ?? OpenAiSdkSerialization.ReadModel<CreateResponseOptions>(request.Body);

        options.StreamingEnabled = stream;
        return options;
    }

    private static Exception CreateDetailedException(string operation, ClientResultException exception)
    {
        string? body = null;
        try
        {
            body = exception.GetRawResponse()?.Content?.ToString();
        }
        catch
        {
        }

        string message = body is null
            ? $"OpenAI Responses API failed to {operation}: {exception.Message}"
            : $"OpenAI Responses API failed to {operation}: {exception.Message}{Environment.NewLine}Response body: {body}";

        return new InvalidOperationException(message, exception);
    }
}
