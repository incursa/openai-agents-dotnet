using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Tests;

public sealed class OpenAiResponsesTests
{
    /// <summary>Request mapping includes tools, handoffs, hosted MCP tools, and structured output definitions.</summary>
    /// <intent>Protect the main request-mapping contract for the OpenAI Responses adapter.</intent>
    /// <scenario>LIB-OAI-MAP-001</scenario>
    /// <behavior>Mapped requests preserve model selection, previous-response IDs, tool payloads, MCP payloads, and response-format metadata.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    public async Task RequestMapper_MapsToolsHandoffsAndStructuredOutput()
    {
        var mailAgent = new Agent<TestContext>
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            HandoffDescription = "Handles mailbox work.",
        };

        var triage = new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Route work",
            Tools =
            [
                new AgentTool<TestContext>
                {
                    Name = "lookup_customer",
                    Description = "Look up a customer",
                    InputSchema = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["customer_id"] = new JsonObject { ["type"] = "string" },
                        },
                        ["required"] = new JsonArray("customer_id"),
                    },
                    ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("ok")),
                },
            ],
            Handoffs =
            [
                new AgentHandoff<TestContext>
                {
                    Name = "mail",
                    TargetAgent = mailAgent,
                    Description = "Transfer to mail",
                },
            ],
            OutputContract = AgentOutputContract.For<ExampleOutput>(),
            HostedMcpTools =
            [
                new HostedMcpToolDefinition("mail", new Uri("https://mail.example.test/mcp"), "connector-1", null, true, null, null, "Hosted mail connector"),
            ],
        };

        var mapper = new OpenAiResponsesRequestMapper();
        OpenAiResponsesTurnPlan<TestContext> plan = await mapper.CreateAsync(new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            "Need mail help",
            "resp-previous",
            null));

        Assert.Equal("gpt-5.4", plan.Body["model"]?.GetValue<string>());
        Assert.Equal("resp-previous", plan.Body["previous_response_id"]?.GetValue<string>());
        Assert.NotNull(plan.Body["tools"]);
        Assert.NotNull(plan.Body["response_format"]);
        Assert.Equal("json_schema", plan.Body["response_format"]?["type"]?.GetValue<string>());
        Assert.Single(plan.HandoffMap);
        Assert.Contains(plan.Body["tools"]!.AsArray(), item => item!["type"]?.GetValue<string>() == "mcp");
    }

    /// <summary>Turn execution resolves local MCP servers into OpenAI tool definitions before sending the request.</summary>
    /// <intent>Protect local MCP server translation in the OpenAI adapter.</intent>
    /// <scenario>LIB-OAI-MCP-001</scenario>
    /// <behavior>Resolved streamable MCP tools are added to the request body before the turn completes.</behavior>
    [Fact]
    public async Task TurnExecutor_AddsLocalMcpToolsToRequestBody()
    {
        var requests = new List<JsonObject>();
        var responses = new Queue<OpenAiResponsesResponse>([
            new OpenAiResponsesResponse("resp-1", new JsonObject
            {
                ["id"] = "resp-1",
                ["output"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "message",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "output_text",
                                ["text"] = "done",
                            },
                        },
                    },
                },
            }),
        ]);

        var client = new RecordingResponsesClient(requests, responses);
        var mcpHandler = new RecordingHandler((_, count) =>
        {
            var payload = count == 1
                ? """{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","description":"List mail","inputSchema":{"type":"object"}}]}}"""
                : """{"jsonrpc":"2.0","id":"1","result":{"content":[{"text":"ok"}]}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        });

        var executor = new OpenAiResponsesTurnExecutor<TestContext>(
            client,
            new HttpClient(mcpHandler));

        var agent = new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            StreamableMcpServers =
            [
                new StreamableHttpMcpServerDefinition("mail", new Uri("https://mail.example.test/mcp")),
            ],
        };

        AgentTurnResponse<TestContext> response = await executor.ExecuteTurnAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            "Need mail help",
            null,
            null), CancellationToken.None);

        Assert.Equal("done", response.FinalOutput?.Text);
        Assert.Single(requests);
        Assert.Contains(requests[0]["tools"]!.AsArray(), item => item!["name"]?.GetValue<string>() == "mcp_mail__list_messages");
    }

    /// <summary>Handoff normalization can strip pre-handoff tool-call items from mapped model input.</summary>
    /// <intent>Protect run-level handoff history shaping in the adapter.</intent>
    /// <scenario>LIB-OAI-HANDOFF-001</scenario>
    /// <behavior>Normalized model input omits pre-handoff function-call and function-call-output items when that mode is enabled.</behavior>
    [Fact]
    public async Task RequestMapper_NormalizesHandoffModelInputAfterHandoff()
    {
        var delegateAgent = new Agent<TestContext>
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        var mapper = new OpenAiResponsesRequestMapper();
        OpenAiResponsesTurnPlan<TestContext> plan = await mapper.CreateAsync(new AgentTurnRequest<TestContext>(
            delegateAgent,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            2,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "help" },
                new AgentConversationItem(AgentItemTypes.ToolCall, "assistant", "triage") { Name = "lookup_customer", ToolCallId = "call-1", Data = new JsonObject { ["customer_id"] = "42" } },
                new AgentConversationItem(AgentItemTypes.ToolOutput, "tool", "triage") { Name = "lookup_customer", ToolCallId = "call-1", Text = "customer-42" },
                new AgentConversationItem(AgentItemTypes.HandoffRequested, "assistant", "triage") { Name = "mail", Text = "delegate", Data = new JsonObject { ["topic"] = "mail" } },
                new AgentConversationItem(AgentItemTypes.HandoffOccurred, "system", "delegate") { Name = "mail", Text = "delegate", Data = new JsonObject { ["topic"] = "mail" } },
            ],
            null,
            "resp-1",
            new AgentRunOptions<TestContext> { HandoffHistoryMode = AgentHandoffHistoryMode.NormalizeModelInputAfterHandoff }));

        JsonArray input = plan.Body["input"]!.AsArray();
        Assert.DoesNotContain(input, item => item?["type"]?.GetValue<string>() == "function_call");
        Assert.DoesNotContain(input, item => item?["type"]?.GetValue<string>() == "function_call_output");
    }

    /// <summary>Run-level model input filters are applied during request mapping.</summary>
    /// <intent>Protect consumer control over the model-visible conversation input.</intent>
    /// <scenario>LIB-OAI-FILTER-001</scenario>
    /// <behavior>Mapped input reflects the filtered conversation instead of the full stored conversation.</behavior>
    [Fact]
    public async Task RequestMapper_UsesRunLevelModelInputFilter()
    {
        var agent = new Agent<TestContext>
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        var mapper = new OpenAiResponsesRequestMapper();
        OpenAiResponsesTurnPlan<TestContext> plan = await mapper.CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            3,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "delegate") { Text = "hello" },
                new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "delegate") { Text = "world" },
                new AgentConversationItem(AgentItemTypes.ToolOutput, "tool", "delegate") { Name = "lookup_customer", ToolCallId = "call-1", Text = "customer-42" },
            ],
            null,
            "resp-2",
            new AgentRunOptions<TestContext> { ModelInputFilterAsync = (ctx, _) => ValueTask.FromResult<IReadOnlyList<AgentConversationItem>>(ctx.Conversation.Where(item => item.Role != "tool").ToArray()) }));

        JsonArray input = plan.Body["input"]!.AsArray();
        Assert.Equal(2, input.Count);
        Assert.DoesNotContain(input, item => item?["type"]?.GetValue<string>() == "function_call_output");
    }

    /// <summary>Reasoning item IDs can be omitted from mapped input when configured.</summary>
    /// <intent>Protect request-shaping options for reasoning items.</intent>
    /// <scenario>LIB-OAI-REASONING-001</scenario>
    /// <behavior>Mapped reasoning items omit their IDs when the configured reasoning-item policy is Omit.</behavior>
    [Fact]
    public async Task RequestMapper_OmitsReasoningIdsWhenConfigured()
    {
        var agent = new Agent<TestContext>
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        var mapper = new OpenAiResponsesRequestMapper();
        OpenAiResponsesTurnPlan<TestContext> plan = await mapper.CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            1,
            [
                new AgentConversationItem(AgentItemTypes.Reasoning, "assistant", "delegate") { Data = new JsonObject { ["type"] = "reasoning", ["id"] = "rs_123", ["summary"] = new JsonArray("thinking") } },
            ],
            null,
            null,
            new AgentRunOptions<TestContext> { ReasoningItemIdPolicy = ReasoningItemIdPolicy.Omit }));

        JsonArray input = plan.Body["input"]!.AsArray();
        JsonNode? reasoning = Assert.Single(input, item => item?["type"]?.GetValue<string>() == "reasoning");
        Assert.Null(reasoning?["id"]);
    }

    /// <summary>Streaming execution reconstructs completed function arguments for emitted tool-call items and final tool-call results.</summary>
    /// <intent>Protect streamed function-call fidelity when argument fragments complete later in the stream.</intent>
    /// <scenario>LIB-OAI-STREAM-001</scenario>
    /// <behavior>Completed function arguments appear on emitted tool-call run items and on the final response tool-call data.</behavior>
    [Fact]
    public async Task StreamingTurnExecutor_UsesCompletedFunctionArgumentsForRunItemAndResponse()
    {
        var streamClient = new StreamingResponsesClient([
            [
                new OpenAiResponsesStreamEvent("response.output_item.added", new JsonObject
                {
                    ["type"] = "response.output_item.added",
                    ["output_index"] = 0,
                    ["item"] = new JsonObject
                    {
                        ["type"] = "function_call",
                        ["id"] = "fc_1",
                        ["call_id"] = "call_1",
                        ["name"] = "lookup_customer",
                        ["arguments"] = "",
                    },
                }),
                new OpenAiResponsesStreamEvent("response.function_call_arguments.done", new JsonObject
                {
                    ["type"] = "response.function_call_arguments.done",
                    ["output_index"] = 0,
                    ["arguments"] = """{"customer_id":"42"}""",
                }),
                new OpenAiResponsesStreamEvent("response.output_item.done", new JsonObject
                {
                    ["type"] = "response.output_item.done",
                    ["output_index"] = 0,
                    ["item"] = new JsonObject
                    {
                        ["type"] = "function_call",
                        ["id"] = "fc_1",
                        ["call_id"] = "call_1",
                        ["name"] = "lookup_customer",
                        ["arguments"] = "",
                    },
                }),
                new OpenAiResponsesStreamEvent("response.completed", new JsonObject
                {
                    ["type"] = "response.completed",
                    ["response"] = new JsonObject
                    {
                        ["id"] = "resp-stream-1",
                        ["output"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "function_call",
                                ["id"] = "fc_1",
                                ["call_id"] = "call_1",
                                ["name"] = "lookup_customer",
                                ["arguments"] = """{"customer_id":"42"}""",
                            },
                        },
                    },
                }),
            ],
        ]);

        var executor = new OpenAiResponsesTurnExecutor<TestContext>(streamClient);
        var events = new List<AgentStreamEvent>();
        AgentTurnResponse<TestContext> response = await executor.ExecuteStreamingTurnAsync(
            new AgentTurnRequest<TestContext>(
                new Agent<TestContext>
                {
                    Name = "triage",
                    Model = "gpt-5.4",
                    Instructions = "Route work",
                    Tools =
                    [
                        new AgentTool<TestContext>
                        {
                            Name = "lookup_customer",
                            ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("unused")),
                        },
                    ],
                },
                new TestContext("user-1", "tenant-1"),
                "session-stream",
                1,
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "lookup customer" },
                ]),
            evt =>
            {
                events.Add(evt);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        AgentStreamEvent toolCallEvent = Assert.Single(events, evt => evt.EventType == AgentStreamEventTypes.RunItem && evt.Item?.ItemType == AgentItemTypes.ToolCall);
        Assert.Equal("42", toolCallEvent.Item!.Data?["customer_id"]?.GetValue<string>());
        Assert.Single(response.ToolCalls);
        Assert.Equal("42", response.ToolCalls[0].Arguments?["customer_id"]?.GetValue<string>());
        Assert.Contains(events, evt => evt.EventType == AgentStreamEventTypes.RawModelEvent);
    }

    private sealed record ExampleOutput
    {
        public ExampleOutput(string value)
        {
            Value = value;
        }

        public string Value { get; init; }
    }

    private sealed record TestContext
    {
        public TestContext(string userId, string tenantId)
        {
            UserId = userId;
            TenantId = tenantId;
        }

        public string UserId { get; init; }

        public string TenantId { get; init; }
    }

    private sealed class RecordingResponsesClient : IOpenAiResponsesClient
    {
        private readonly List<JsonObject> requests;
        private readonly Queue<OpenAiResponsesResponse> responses;

        public RecordingResponsesClient(List<JsonObject> requests, Queue<OpenAiResponsesResponse> responses)
        {
            this.requests = requests;
            this.responses = responses;
        }

        public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
        {
            requests.Add(request.Body.DeepClone() as JsonObject ?? new JsonObject());
            return Task.FromResult(responses.Dequeue());
        }

        public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class StreamingResponsesClient : IOpenAiResponsesClient
    {
        private readonly Queue<IReadOnlyList<OpenAiResponsesStreamEvent>> turns;

        public StreamingResponsesClient(IReadOnlyList<IReadOnlyList<OpenAiResponsesStreamEvent>> turns)
        {
            this.turns = new Queue<IReadOnlyList<OpenAiResponsesStreamEvent>>(turns);
        }

        public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (OpenAiResponsesStreamEvent item in turns.Dequeue())
            {
                yield return item;
                await Task.Yield();
            }
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> handler;
        private int count;

        public RecordingHandler(Func<HttpRequestMessage, int, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            count++;
            return Task.FromResult(handler(request, count));
        }
    }
}
