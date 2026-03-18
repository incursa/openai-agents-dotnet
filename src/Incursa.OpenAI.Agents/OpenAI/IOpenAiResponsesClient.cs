namespace Incursa.OpenAI.Agents;

/// <summary>
/// Defines the client contract for calling the OpenAI Responses API.
/// </summary>

public interface IOpenAiResponsesClient
{
    /// <summary>Creates a non-streamed response for the specified request.</summary>
    Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken);

    /// <summary>Streams response events for the specified request.</summary>
    IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken);
}
