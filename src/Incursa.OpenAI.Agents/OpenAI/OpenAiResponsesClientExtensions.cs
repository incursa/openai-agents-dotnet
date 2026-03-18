namespace Incursa.OpenAI.Agents;

/// <summary>
/// Adds cancellation-token-free helpers for IOpenAiResponsesClient.
/// </summary>

public static class OpenAiResponsesClientExtensions
{
    /// <summary>Creates a non-streamed response using no cancellation token.</summary>
    public static Task<OpenAiResponsesResponse> CreateResponseAsync(this IOpenAiResponsesClient client, OpenAiResponsesRequest request)
        => client.CreateResponseAsync(request, CancellationToken.None);

    /// <summary>Streams response events using no cancellation token.</summary>
    public static IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(this IOpenAiResponsesClient client, OpenAiResponsesRequest request)
        => client.StreamResponseAsync(request, CancellationToken.None);
}
