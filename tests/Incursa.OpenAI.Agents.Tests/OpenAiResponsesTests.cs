#pragma warning disable OPENAI001

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for the OpenAI Responses adapter and request mapping.</summary>
public sealed class OpenAiResponsesTests
{
    private static readonly JsonObject ExampleOutputSchema = new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["value"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray("value"),
        ["additionalProperties"] = false,
    };

    /// <summary>Invalid output schemas are rejected before the request reaches the OpenAI SDK.</summary>
    /// <intent>Prevent malformed output schemas from surfacing as remote 400 responses.</intent>
    /// <scenario>LIB-OAI-MAP-OUTPUT-001</scenario>
    /// <behavior>Constructing an output contract with a non-object schema fails locally.</behavior>
    [Fact]
    public void AgentOutputContract_RejectsNonObjectSchemas()
    {
        var error = Assert.Throws<ArgumentException>(() => new AgentOutputContract(new JsonArray()));

        Assert.Equal("schema", error.ParamName);
    }

    /// <summary>Turn execution resolves local MCP servers into OpenAI tool definitions before sending the request.</summary>
    /// <intent>Protect local MCP server translation in the OpenAI adapter.</intent>
    /// <scenario>LIB-OAI-MCP-001</scenario>
    /// <behavior>Resolved streamable MCP tools are added to the request body before the turn completes.</behavior>
    [Fact]
    public async Task TurnExecutor_AddsLocalMcpToolsToRequestOptions()
    {
        List<CreateResponseOptions> requests = new();
        Queue<OpenAiResponsesResponse> responses = new([
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

        RecordingResponsesClient client = new(requests, responses);
        RecordingHandler mcpHandler = new((_, count) =>
        {
            var payload = count == 1
                ? """{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","description":"List mail","inputSchema":{"type":"object"}}]}}"""
                : """{"jsonrpc":"2.0","id":"1","result":{"content":[{"text":"ok"}]}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        });

        OpenAiResponsesTurnExecutor<TestContext> executor = new(
            client,
            new HttpClient(mcpHandler));

        Agent<TestContext> agent = new()
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
        Assert.Contains(requests[0].Tools, item => item is FunctionTool function && function.FunctionName == "mcp_mail__list_messages");
    }

    /// <summary>Handoff normalization can strip pre-handoff tool-call items from mapped model input.</summary>
    /// <intent>Protect run-level handoff history shaping in the adapter.</intent>
    /// <scenario>LIB-OAI-HANDOFF-001</scenario>
    /// <behavior>Normalized model input omits pre-handoff function-call and function-call-output items when that mode is enabled.</behavior>
    [Fact]
    public async Task RequestMapper_NormalizesHandoffModelInputAfterHandoff()
    {
        Agent<TestContext> delegateAgent = new()
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        OpenAiResponsesRequestMapper mapper = new();
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
            null,
            new AgentRunOptions<TestContext> { HandoffHistoryMode = AgentHandoffHistoryMode.NormalizeModelInputAfterHandoff }));

        Assert.DoesNotContain(plan.Options.InputItems, item => item is FunctionCallResponseItem);
        Assert.DoesNotContain(plan.Options.InputItems, item => item is FunctionCallOutputResponseItem);
    }

    /// <summary>Run-level model input filters are applied during request mapping.</summary>
    /// <intent>Protect consumer control over the model-visible conversation input.</intent>
    /// <scenario>LIB-OAI-FILTER-001</scenario>
    /// <behavior>Mapped input reflects the filtered conversation instead of the full stored conversation.</behavior>
    [Fact]
    public async Task RequestMapper_UsesRunLevelModelInputFilter()
    {
        Agent<TestContext> agent = new()
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        OpenAiResponsesRequestMapper mapper = new();
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

        Assert.Equal(2, plan.Options.InputItems.Count);
        Assert.DoesNotContain(plan.Options.InputItems, item => item is FunctionCallOutputResponseItem);
    }

    /// <summary>Runtime execution uses typed SDK options instead of rebuilding them from the JSON snapshot.</summary>
    /// <intent>Prevent the compatibility request snapshot from becoming the live transport source of truth.</intent>
    /// <scenario>LIB-OAI-CLIENT-001</scenario>
    /// <behavior>When typed options and a snapshot disagree, the client uses the typed options for the SDK call.</behavior>
    [Fact]
    public void ResponsesClient_PrefersTypedOptionsOverJsonSnapshot()
    {
        CreateResponseOptions options = new()
        {
            Model = "gpt-5.4",
        };

        OpenAiResponsesRequest request = new(
            options,
            new JsonObject
            {
                ["model"] = "wrong-model",
            });

        MethodInfo method = typeof(OpenAiResponsesClient).GetMethod("GetRequestOptions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("OpenAiResponsesClient.GetRequestOptions was not found.");

        CreateResponseOptions actual = Assert.IsType<CreateResponseOptions>(method.Invoke(null, [request, true]));

        Assert.Equal("gpt-5.4", actual.Model);
        Assert.True(actual.StreamingEnabled);
    }

    /// <summary>Wrapping typed options does not force snapshot serialization for SDK-only tool types.</summary>
    /// <intent>Prevent request construction from crashing on hosted MCP tools due to SDK serializer limitations.</intent>
    /// <scenario>LIB-OAI-CLIENT-002</scenario>
    /// <behavior>Creating an internal request from typed options succeeds and leaves the compatibility JSON body empty until explicitly provided.</behavior>
    [Fact]
    public async Task OpenAiResponsesRequest_DoesNotSerializeTypedOptionsSnapshot()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            HostedMcpTools =
            [
                new HostedMcpToolDefinition("mail", new Uri("https://mail.example.test/mcp"), "connector-1", null, true, null, null, "Hosted mail connector"),
            ],
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            null,
            null,
            null));

        OpenAiResponsesRequest request = new(plan.Options);

        Assert.Same(plan.Options, request.Options);
        Assert.Empty(request.Body);
    }

    /// <summary>Reasoning item IDs can be omitted from mapped input when configured.</summary>
    /// <intent>Protect request-shaping options for reasoning items.</intent>
    /// <scenario>LIB-OAI-REASONING-001</scenario>
    /// <behavior>Mapped reasoning items omit their IDs when the configured reasoning-item policy is Omit.</behavior>
    [Fact]
    public async Task RequestMapper_OmitsReasoningIdsWhenConfigured()
    {
        Agent<TestContext> agent = new()
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        OpenAiResponsesRequestMapper mapper = new();
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

        ReasoningResponseItem reasoning = Assert.IsType<ReasoningResponseItem>(Assert.Single(plan.Options.InputItems));
        Assert.Null(reasoning.Id);
    }

    /// <summary>Hosted MCP approval requests are surfaced as approval-required tool calls.</summary>
    /// <intent>Protect MCP approval handling after switching to the official Responses SDK item models.</intent>
    /// <scenario>LIB-OAI-MCP-APPROVAL-001</scenario>
    /// <behavior>MCP approval request output items map to approval-required MCP tool calls with parsed arguments.</behavior>
    [Fact]
    public async Task ResponseMapper_MapsMcpApprovalRequestsToApprovalRequiredToolCalls()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle mail approvals.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-approval",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Delete the spam message" },
            ],
            null,
            null,
            null));

        OpenAiResponsesResponse response = new("resp-approval-1", new JsonObject
        {
            ["id"] = "resp-approval-1",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_approval_request",
                    ["id"] = "apr_1",
                    ["server_label"] = "mail",
                    ["name"] = "delete_message",
                    ["arguments"] = """{"message_id":"msg_42"}""",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.True(toolCall.RequiresApproval);
        Assert.Equal("mcp", toolCall.ToolType);
        Assert.Equal("delete_message", toolCall.ToolName);
        Assert.Equal("msg_42", toolCall.Arguments?["message_id"]?.GetValue<string>());
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Structured final outputs keep the caller-supplied CLR type metadata for downstream consumers.</summary>
    /// <intent>Preserve typed output metadata after removing CLR-based schema generation.</intent>
    /// <scenario>LIB-OAI-RESP-OUTPUT-001</scenario>
    /// <behavior>Mapped final output carries the output contract CLR type when one is supplied.</behavior>
    [Fact]
    public async Task ResponseMapper_PreservesOutputTypeMetadataFromExplicitSchemaContract()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Route work",
            OutputContract = new AgentOutputContract(ExampleOutputSchema, null, typeof(ExampleOutput)),
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-output",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Say hello" },
            ],
            null,
            null,
            null));

        OpenAiResponsesResponse response = new("resp-output-1", new JsonObject
        {
            ["id"] = "resp-output-1",
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
                            ["text"] = """{"value":"hello"}""",
                        },
                        new JsonObject
                        {
                            ["type"] = "output_json",
                            ["value"] = new JsonObject
                            {
                                ["value"] = "hello",
                            },
                        },
                    },
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        Assert.Equal(typeof(ExampleOutput), turn.FinalOutput?.OutputType);
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
        private readonly List<CreateResponseOptions> requests;
        private readonly Queue<OpenAiResponsesResponse> responses;

        public RecordingResponsesClient(List<CreateResponseOptions> requests, Queue<OpenAiResponsesResponse> responses)
        {
            this.requests = requests;
            this.responses = responses;
        }

        public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
        {
            requests.Add(request.Options ?? OpenAiSdkSerialization.ReadModel<CreateResponseOptions>(request.Body));
            return Task.FromResult(responses.Dequeue());
        }

        public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
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
