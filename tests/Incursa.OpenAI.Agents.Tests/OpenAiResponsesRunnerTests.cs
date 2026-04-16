#pragma warning disable OPENAI001

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents.Mcp;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests the OpenAI runner wrapper that composes the core runner with the OpenAI turn executor.</summary>
public sealed class OpenAiResponsesRunnerTests
{
    /// <summary>Omitted infrastructure collaborators fall back to concrete defaults instead of remaining null.</summary>
    /// <intent>Protect constructor defaults for approval, observation, session persistence, and MCP transport composition.</intent>
    /// <scenario>LIB-OAI-RUNNER-001</scenario>
    /// <behavior>Constructing the runner with only a client creates non-null default collaborators for session storage, approvals, observation, and MCP HTTP transport.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void Constructor_CreatesDefaultCollaborators()
    {
        OpenAiResponsesRunner runner = new(new RecordingResponsesClient());

        Assert.IsType<InMemoryAgentSessionStore>(GetPrivateField<object>(runner, "sessionStore"));
        Assert.IsType<AllowAllAgentApprovalService>(GetPrivateField<object>(runner, "approvalService"));
        Assert.IsType<NullAgentRuntimeObserver>(GetPrivateField<object>(runner, "observer"));
        Assert.IsType<HttpClient>(GetPrivateField<object>(runner, "mcpHttpClient"));
    }

    /// <summary>Injected approval and observation collaborators change runtime behavior instead of being silently replaced.</summary>
    /// <intent>Protect dependency injection of custom approval and runtime-observation collaborators on the public OpenAI runner.</intent>
    /// <scenario>LIB-OAI-RUNNER-002</scenario>
    /// <behavior>When a required-approval tool call is returned, the injected approval service decides the outcome and the injected observer records the approval-required lifecycle.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RunAsync_UsesInjectedApprovalServiceAndObserver()
    {
        RecordingResponsesClient client = new(
        [
            CreateFunctionCallResponse("resp-approval", "delete_message", """{"message_id":"msg_42"}"""),
        ]);
        RequiredApprovalService approvalService = new("manager review required");
        RecordingRuntimeObserver observer = new();
        var executions = 0;

        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle requests.",
            Tools =
            [
                new AgentTool<TestContext>
                {
                    Name = "delete_message",
                    RequiresApproval = true,
                    ExecuteAsync = (_, _) =>
                    {
                        executions++;
                        return ValueTask.FromResult(AgentToolResult.FromText("deleted"));
                    },
                },
            ],
        };

        OpenAiResponsesRunner runner = new(client, null, approvalService, null, null, null, null, observer);

        AgentRunResult<TestContext> result = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(agent, "delete the spam message", new TestContext(), "session-approval"));

        Assert.Equal(AgentRunStatus.ApprovalRequired, result.Status);
        Assert.Equal("delete_message", result.ApprovalRequest?.ToolName);
        Assert.Equal("manager review required", result.ApprovalRequest?.Reason);
        Assert.Equal(0, executions);
        Assert.Equal(1, approvalService.Calls);
        Assert.Contains(observer.Observations, observation => observation.EventName == AgentRuntimeEventNames.RunStarted);
        Assert.Contains(observer.Observations, observation => observation.EventName == AgentRuntimeEventNames.ApprovalRequired);
        Assert.Contains(observer.Observations, observation =>
            observation.EventName == AgentRuntimeEventNames.RunCompleted
            && observation.Status == AgentRunStatus.ApprovalRequired);
    }

    /// <summary>Injected MCP HTTP clients are used for runtime tool discovery instead of being replaced.</summary>
    /// <intent>Protect custom MCP transport injection on the OpenAI runner when streamable MCP servers are enabled.</intent>
    /// <scenario>LIB-OAI-RUNNER-003</scenario>
    /// <behavior>The runner uses the supplied MCP `HttpClient` to list remote tools and includes the discovered proxy tool in the outgoing OpenAI request.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RunAsync_UsesInjectedMcpHttpClientForRuntimeToolDiscovery()
    {
        RecordingResponsesClient client = new(
        [
            CreateFinalTextResponse("resp-mcp", "done"),
        ]);
        RecordingHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","description":"List mail","inputSchema":{"type":"object"}}]}}""", Encoding.UTF8, "application/json"),
        }));
        using HttpClient mcpHttpClient = new(handler);

        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle mail.",
            StreamableMcpServers =
            [
                new StreamableHttpMcpServerDefinition("mail", new Uri("https://example.test/mcp")),
            ],
        };

        OpenAiResponsesRunner runner = new(client, null, null, null, mcpHttpClient, null, null, null);

        AgentRunResult<TestContext> result = await runner.RunAsync(
            AgentRunRequest<TestContext>.FromUserInput(agent, "check mail", new TestContext(), "session-mcp"));

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Equal("done", result.FinalOutput?.Text);
        Assert.Single(client.Requests);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains(client.Requests[0].Tools, item => item is FunctionTool function && function.FunctionName == "mcp_mail__list_messages");
    }

    /// <summary>Streaming wrapper execution emits both raw model events and normalized run items.</summary>
    /// <intent>Protect the OpenAI runner streaming path that composes the streaming turn executor with the core runner event loop.</intent>
    /// <scenario>LIB-OAI-RUNNER-004</scenario>
    /// <behavior>Streaming runs emit the seeded user input, raw model events, mapped message items, and the final output item.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RunStreamingAsync_EmitsRawAndNormalizedEvents()
    {
        RecordingResponsesClient client = new(
            streamTurns:
            [
                [
                    new OpenAiResponsesStreamEvent("response.output_item.done", new JsonObject
                    {
                        ["type"] = "response.output_item.done",
                        ["output_index"] = 0,
                        ["item"] = CreateMessageItem("streamed hello"),
                    }),
                    new OpenAiResponsesStreamEvent("response.completed", new JsonObject
                    {
                        ["type"] = "response.completed",
                        ["response"] = new JsonObject
                        {
                            ["id"] = "resp-stream",
                            ["output"] = new JsonArray
                            {
                                CreateMessageItem("streamed hello"),
                            },
                        },
                    }),
                ],
            ]);

        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Answer briefly.",
        };

        OpenAiResponsesRunner runner = new(client);
        List<AgentStreamEvent> events = [];

        await foreach (AgentStreamEvent item in runner.RunStreamingAsync(
            AgentRunRequest<TestContext>.FromUserInput(agent, "hello", new TestContext(), "session-stream")))
        {
            events.Add(item);
        }

        Assert.Contains(events, item => item.EventType == AgentStreamEventTypes.RawModelEvent);
        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.UserInput);
        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.MessageOutput && item.Item.Text == "streamed hello");
        Assert.Contains(events, item => item.Item?.ItemType == AgentItemTypes.FinalOutput && item.Item.Text == "streamed hello");
    }

    private static JsonObject CreateMessageItem(string text)
        => new()
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
        };

    private static OpenAiResponsesResponse CreateFinalTextResponse(string responseId, string text)
        => new(responseId, new JsonObject
        {
            ["id"] = responseId,
            ["output"] = new JsonArray
            {
                CreateMessageItem(text),
            },
        });

    private static OpenAiResponsesResponse CreateFunctionCallResponse(string responseId, string name, string arguments)
        => new(responseId, new JsonObject
        {
            ["id"] = responseId,
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = "fc_1",
                    ["call_id"] = "call_1",
                    ["name"] = name,
                    ["arguments"] = arguments,
                    ["status"] = "completed",
                },
            },
        });

    private static object GetPrivateField<TField>(OpenAiResponsesRunner runner, string fieldName)
    {
        FieldInfo field = typeof(OpenAiResponsesRunner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        return field.GetValue(runner)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was null.");
    }

    private sealed class RecordingResponsesClient : IOpenAiResponsesClient
    {
        private readonly Queue<OpenAiResponsesResponse> responses;
        private readonly Queue<IReadOnlyList<OpenAiResponsesStreamEvent>> streamTurns;

        public RecordingResponsesClient(
            IReadOnlyList<OpenAiResponsesResponse>? responses = null,
            IReadOnlyList<IReadOnlyList<OpenAiResponsesStreamEvent>>? streamTurns = null)
        {
            this.responses = new Queue<OpenAiResponsesResponse>(responses ?? []);
            this.streamTurns = new Queue<IReadOnlyList<OpenAiResponsesStreamEvent>>(streamTurns ?? []);
        }

        public List<CreateResponseOptions> Requests { get; } = [];

        public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Options ?? OpenAiSdkSerialization.ReadModel<CreateResponseOptions>(request.Body));

            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No response configured for CreateResponseAsync.");
            }

            return Task.FromResult(responses.Dequeue());
        }

        public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(
            OpenAiResponsesRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (streamTurns.Count == 0)
            {
                throw new InvalidOperationException("No stream turn configured for StreamResponseAsync.");
            }

            foreach (OpenAiResponsesStreamEvent item in streamTurns.Dequeue())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    private sealed class RequiredApprovalService : IAgentApprovalService
    {
        private readonly string reason;

        public RequiredApprovalService(string reason)
        {
            this.reason = reason;
        }

        public int Calls { get; private set; }

        public ValueTask<ApprovalDecision> EvaluateAsync<TContext>(AgentApprovalContext<TContext> context, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(ApprovalDecision.Require(reason));
        }
    }

    private sealed class RecordingRuntimeObserver : IAgentRuntimeObserver
    {
        public List<AgentRuntimeObservation> Observations { get; } = [];

        public ValueTask ObserveAsync(AgentRuntimeObservation observation, CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return handler(request, cancellationToken);
        }
    }

    private sealed record TestContext
    {
        public TestContext()
            : this("user-1", "tenant-1")
        {
        }

        public TestContext(string userId, string tenantId)
        {
            UserId = userId;
            TenantId = tenantId;
        }

        public string UserId { get; init; }

        public string TenantId { get; init; }
    }
}
