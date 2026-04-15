#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Streaming OpenAI Responses execution reconstructs completed tool-call arguments.</summary>
public sealed class REQ_LIB_OAI_STREAM_001
{
    /// <summary>Streaming execution reconstructs completed function-call arguments for emitted tool-call run items and final tool-call results.</summary>
    /// <intent>Protect streamed function-call fidelity when argument fragments complete later in the stream.</intent>
    /// <scenario>LIB-OAI-STREAM-001</scenario>
    /// <behavior>Completed function arguments appear on emitted tool-call run items and on the final response tool-call data.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task StreamingTurnExecutor_UsesCompletedFunctionArgumentsForRunItemAndResponse()
    {
        StreamingResponsesClient streamClient = new([
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

        OpenAiResponsesTurnExecutor<TestContext> executor = new(streamClient);
        List<AgentStreamEvent> events = [];
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

    private sealed record TestContext(string UserId, string TenantId);

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
}
