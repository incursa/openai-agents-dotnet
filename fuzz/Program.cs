#pragma warning disable SCME0001

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Reflection;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;
using System.ClientModel.Primitives;
using SharpFuzz;

namespace Incursa.OpenAI.Agents.Fuzz;

public static class Program
{
    private static readonly OpenAiResponsesRequestMapper RequestMapper = new();
    private static readonly OpenAiResponsesResponseMapper ResponseMapper = new();
    private static readonly OpenAiResponsesTurnPlan<TestContext> Plan = CreatePlan();
    private static readonly MethodInfo ApplyJsonPatchValueMethod = typeof(OpenAiResponsesRequestMapper).GetMethod("ApplyJsonPatchValue", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("OpenAiResponsesRequestMapper.ApplyJsonPatchValue was not found.");

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

        if (input[0] == (byte)'Q')
        {
            FuzzRequestMapping(input);
        }
        else if ((input[0] & 1) == 0)
        {
            FuzzOpenAiResponses(input);
        }
        else
        {
            FuzzMcpClient(input);
        }
    }

    private static void FuzzRequestMapping(byte[] input)
    {
        string text = input.Length > 1 ? Encoding.UTF8.GetString(input, 1, input.Length - 1) : string.Empty;
        char? discriminator = text.Length > 0 ? text[0] : null;
        string payload = text.Length > 1 ? text[1..] : string.Empty;

        switch (discriminator)
        {
            case 'D':
                SerializeRequest(CreatePlan(new Agent<TestContext>
                {
                    Name = "fuzz",
                    Model = "gpt-5.4",
                    Instructions = "Exercise request mapping.",
                    ModelSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["custom_large_double"] = 1e100d,
                    },
                }));
                break;
            case 'H':
                SerializeRequest(CreatePlan(new Agent<TestContext>
                {
                    Name = "fuzz",
                    Model = "gpt-5.4",
                    Instructions = "Exercise request mapping.",
                    HostedMcpTools =
                    [
                        new HostedMcpToolDefinition(SanitizeToolName(payload)),
                    ],
                }));
                break;
            case 'U':
                JsonPatch patch = new(Array.Empty<byte>());
                object?[] parameters =
                [
                    patch,
                    "$.custom_uri",
                    JsonValue.Create(new Uri($"https://example.test/{SanitizeToolName(payload).ToLowerInvariant()}")),
                ];

                ApplyJsonPatchValueMethod.Invoke(null, parameters);
                _ = parameters[0]?.ToString();
                break;
            default:
                SerializeRequest(CreatePlan());
                break;
        }
    }

    private static void FuzzOpenAiResponses(byte[] input)
    {
        string text = input.Length > 1 ? Encoding.UTF8.GetString(input, 1, input.Length - 1) : string.Empty;
        JsonObject item = BuildStreamingItem(text);

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

    private static OpenAiResponsesTurnPlan<TestContext> CreatePlan(Agent<TestContext>? agent = null)
    {
        agent ??= new Agent<TestContext>
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

    private static void SerializeRequest(OpenAiResponsesTurnPlan<TestContext> plan)
    {
        using HttpClient httpClient = new(new ResponsesHandler())
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "fuzz-key");

        OpenAiResponsesClient client = new(httpClient, "v1/responses");
        client.CreateResponseAsync(new OpenAiResponsesRequest(plan.Options)).GetAwaiter().GetResult();
    }

    private static JsonObject BuildStreamingItem(string text)
    {
        char? discriminator = text.Length > 0 ? text[0] : null;
        string payload = text.Length > 1 ? text[1..] : string.Empty;

        return discriminator switch
        {
            'F' => CreateFunctionCallItem(payload, malformedArguments: false),
            'B' => CreateFunctionCallItem(payload, malformedArguments: true),
            'R' => new JsonObject
            {
                ["type"] = "reasoning",
                ["id"] = "rs_fuzz",
                ["summary"] = new JsonArray
                {
                    payload,
                },
            },
            'A' => new JsonObject
            {
                ["type"] = "mcp_approval_request",
                ["id"] = "apr_fuzz",
                ["server_label"] = "mail",
                ["name"] = SanitizeToolName(payload),
                ["arguments"] = JsonValue.Create(payload),
            },
            'L' => new JsonObject
            {
                ["type"] = "mcp_list_tools",
                ["server_label"] = "mail",
                ["tools"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = SanitizeToolName(payload),
                    },
                },
            },
            _ => new JsonObject
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
            },
        };
    }

    private static JsonObject CreateFunctionCallItem(string text, bool malformedArguments)
        => new()
        {
            ["type"] = "function_call",
            ["id"] = "fc_fuzz",
            ["call_id"] = "call_fuzz",
            ["name"] = SanitizeToolName(text),
            ["arguments"] = malformedArguments
                ? JsonValue.Create(text)
                : JsonValue.Create(new JsonObject
                {
                    ["value"] = text,
                }.ToJsonString()),
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

    private sealed class ResponsesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"resp-fuzz","output":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed record TestContext(string UserId, string TenantId);
}
