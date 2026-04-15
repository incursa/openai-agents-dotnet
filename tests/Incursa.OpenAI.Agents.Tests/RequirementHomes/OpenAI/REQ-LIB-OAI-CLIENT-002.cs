#pragma warning disable OPENAI001

using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>OpenAI Responses client construction and error handling remain stable for public consumption.</summary>
public sealed class REQ_LIB_OAI_CLIENT_002
{
    /// <summary>Endpoint resolution keeps the documented base URI behavior for blank, responses, and v1-responses paths.</summary>
    /// <intent>Protect the client endpoint normalization contract used by the OpenAI transport adapter.</intent>
    /// <scenario>LIB-OAI-CLIENT-002</scenario>
    /// <behavior>Null base addresses fall back to the OpenAI default endpoint, blank and `/responses` paths keep the base address, `v1/responses` keeps the version prefix, and unsupported paths are rejected.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void OpenAiResponsesClient_ResolvesEndpointVariants()
    {
        Uri baseAddress = new("https://example.test/api/");

        Assert.Equal(new Uri("https://api.openai.com/v1"), ResolveEndpoint(null, "v1/responses"));
        Assert.Equal(baseAddress, ResolveEndpoint(baseAddress, "   "));
        Assert.Equal(baseAddress, ResolveEndpoint(baseAddress, "/responses"));
        Assert.Equal(new Uri("https://example.test/api/v1/"), ResolveEndpoint(baseAddress, "v1/responses"));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => ResolveEndpoint(baseAddress, "v2/chat"));
        Assert.Contains("not supported", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>HttpClient construction rejects calls that do not carry a bearer authorization header.</summary>
    /// <intent>Protect the SDK handoff guard so consumers get an immediate local failure.</intent>
    /// <scenario>LIB-OAI-CLIENT-003</scenario>
    /// <behavior>Constructing the client without a bearer authorization header fails before any request is sent.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void OpenAiResponsesClient_RejectsClientsWithoutBearerAuthorizationHeader()
    {
        HttpClient httpClient = new(new PassThroughHandler())
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "token");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new OpenAiResponsesClient(httpClient));
        Assert.Contains("bearer Authorization header", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>API failures retain the response body details when the OpenAI SDK raises a client error.</summary>
    /// <intent>Protect the public exception surface so troubleshooting includes the upstream response body.</intent>
    /// <scenario>LIB-OAI-CLIENT-004</scenario>
    /// <behavior>OpenAI client failures are wrapped with the upstream response body appended to the error message.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task OpenAiResponsesClient_WrapsApiFailuresWithResponseBodyDetail()
    {
        HttpClient httpClient = new(new JsonResponseHandler(HttpStatusCode.BadRequest, """{"error":{"message":"forced failure"}}"""))
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-api-key");

        OpenAiResponsesClient client = new(httpClient, "v1/responses");

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateResponseAsync(new OpenAiResponsesRequest(new CreateResponseOptions
            {
                Model = "gpt-5.4",
            }), CancellationToken.None));

        Assert.Contains("OpenAI Responses API failed to create response", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Response body:", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("forced failure", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ResolveEndpoint(Uri? baseAddress, string responsesPath)
    {
        MethodInfo method = typeof(OpenAiResponsesClient).GetMethod("ResolveEndpoint", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("OpenAiResponsesClient.ResolveEndpoint was not found.");

        try
        {
            return (Uri)method.Invoke(null, [baseAddress, responsesPath])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private sealed class PassThroughHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"resp-1","output":[]}""", Encoding.UTF8, "application/json"),
            });
    }

    private sealed class JsonResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string body;

        public JsonResponseHandler(HttpStatusCode statusCode, string body)
        {
            this.statusCode = statusCode;
            this.body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
