using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;
using SharpFuzz;

namespace Incursa.OpenAI.Agents.Fuzz;

public static class Program
{
    private static readonly OpenAiResponsesRequestMapper RequestMapper = new();
    private static readonly OpenAiResponsesResponseMapper ResponseMapper = new();
    private static readonly OpenAiResponsesTurnPlan<TestContext> Plan = CreatePlan();

    public static void Main(string[] args)
    {
        Fuzzer.OutOfProcess.Run(ConsumeInput);
    }

    private static void ConsumeInput(Stream stream)
    {
        byte[] input = ReadAllBytes(stream);
        if (input.Length == 0)
        {
            return;
        }

        if ((input[0] & 1) == 0)
        {
            FuzzOpenAiResponses(input);
        }
        else
        {
            FuzzMcpClient(input);
        }
    }

    private static void FuzzOpenAiResponses(byte[] input)
    {
        string text = input.Length > 1 ? Encoding.UTF8.GetString(input, 1, input.Length - 1) : string.Empty;
        JsonObject item = BuildStreamingItem(text, input[0]);

        OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("fuzz-agent", item);
        OpenAiResponsesStreaming.TryGetOutputItem(new OpenAiResponsesStreamEvent("response.output_item.added", new JsonObject
        {
            ["item"] = item.DeepClone(),
        }), out _);
        OpenAiResponsesStreaming.TryGetResponseId(new OpenAiResponsesStreamEvent("response.completed", new JsonObject
        {
            ["response"] = new JsonObject
            {
                ["id"] = "resp-fuzz",
            },
        }), out _);

        OpenAiResponsesResponse response = new("resp-fuzz", new JsonObject
        {
            ["id"] = "resp-fuzz",
            ["output"] = new JsonArray
            {
                item.DeepClone(),
            },
        });

        ResponseMapper.Map(response, Plan);
    }

    private static void FuzzMcpClient(byte[] input)
    {
        string payload = input.Length > 1 ? Encoding.UTF8.GetString(input, 1, input.Length - 1) : string.Empty;
        HttpStatusCode status = (input[0] >> 1) switch
        {
            0 => HttpStatusCode.OK,
            1 => HttpStatusCode.Unauthorized,
            2 => HttpStatusCode.ServiceUnavailable,
            _ => HttpStatusCode.BadGateway,
        };

        using HttpClient httpClient = new(new FuzzHandler(status, payload));
        StreamableHttpMcpClient client = new(
            httpClient,
            "fuzz",
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
            });

        try
        {
            client.ListToolsAsync().GetAwaiter().GetResult();
        }
        catch (McpException)
        {
        }

        try
        {
            client.CallToolAsync("fuzz_tool", new JsonObject
            {
                ["input"] = payload,
            }).GetAwaiter().GetResult();
        }
        catch (McpException)
        {
        }
    }

    private static OpenAiResponsesTurnPlan<TestContext> CreatePlan()
    {
        Agent<TestContext> agent = new()
        {
            Name = "fuzz",
            Model = "gpt-5.4",
            Instructions = "Exercise parser paths.",
        };

        AgentTurnRequest<TestContext> request = new(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-fuzz",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "fuzz") { Text = "hello" },
            ]);

        return RequestMapper.CreateAsync(request).AsTask().GetAwaiter().GetResult();
    }

    private static JsonObject BuildStreamingItem(string text, byte selector)
        => (selector & 1) == 0
            ? new JsonObject
            {
                ["type"] = "message",
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "output_text",
                        ["text"] = text,
                    },
                },
            }
            : new JsonObject
            {
                ["type"] = "function_call",
                ["id"] = "fc_fuzz",
                ["call_id"] = "call_fuzz",
                ["name"] = SanitizeToolName(text),
                ["arguments"] = new JsonObject
                {
                    ["value"] = text,
                }.ToJsonString(),
            };

    private static string SanitizeToolName(string text)
    {
        string sanitized = new(text
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .Take(24)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "fuzz_tool" : sanitized;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed class FuzzHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly string payload;

        public FuzzHandler(HttpStatusCode status, string payload)
        {
            this.status = status;
            this.payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed record TestContext(string UserId, string TenantId);
}
