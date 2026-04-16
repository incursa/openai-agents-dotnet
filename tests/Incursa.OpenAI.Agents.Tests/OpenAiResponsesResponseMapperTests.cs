#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests fallback and edge-case handling in OpenAI Responses output mapping.</summary>
public sealed class OpenAiResponsesResponseMapperTests
{
    /// <summary>Malformed function-call arguments are preserved instead of crashing mapping.</summary>
    /// <intent>Protect the tool-call parsing path from malformed argument payloads returned by the model.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-001</scenario>
    /// <behavior>Invalid JSON arguments are wrapped in a value object so downstream execution can still reason about the payload.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_WrapsMalformedFunctionArgumentsInValueObject()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle requests.",
        });

        OpenAiResponsesResponse response = new("resp-bad-args", new JsonObject
        {
            ["id"] = "resp-bad-args",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = "fc_1",
                    ["call_id"] = "call_1",
                    ["name"] = "lookup_customer",
                    ["arguments"] = "{bad-json",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        JsonObject arguments = Assert.IsType<JsonObject>(toolCall.Arguments);
        Assert.Equal("{bad-json", arguments["value"]?.GetValue<string>());
        Assert.Equal("function", toolCall.ToolType);
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Handoff tool calls are surfaced as handoff requests instead of normal tool calls.</summary>
    /// <intent>Protect handoff routing when the model calls the generated handoff tool.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-002</scenario>
    /// <behavior>Mapped output routes matching handoff tool calls into the handoff list and preserves parsed arguments and status.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsHandoffToolCallsIntoHandoffRequests()
    {
        Agent<TestContext> mailAgent = new()
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "Handle mail.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Route work.",
            Handoffs =
            [
                new AgentHandoff<TestContext>
                {
                    Name = "mail",
                    TargetAgent = mailAgent,
                    ToolNameOverride = "route_to_mail",
                    Description = "Transfer to mail.",
                },
            ],
        });

        OpenAiResponsesResponse response = new("resp-handoff", new JsonObject
        {
            ["id"] = "resp-handoff",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = "fc_mail",
                    ["call_id"] = "call_mail",
                    ["name"] = "route_to_mail",
                    ["arguments"] = """{"topic":"billing"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentHandoffRequest<TestContext> handoff = Assert.Single(turn.Handoffs);
        JsonObject arguments = Assert.IsType<JsonObject>(handoff.Arguments);
        Assert.Equal("mail", handoff.HandoffName);
        Assert.Same(mailAgent, handoff.TargetAgent);
        Assert.Equal("billing", arguments["topic"]?.GetValue<string>());
        Assert.Equal("completed", handoff.Reason);
        Assert.Empty(turn.ToolCalls);
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Structured-only assistant outputs still produce a final text payload.</summary>
    /// <intent>Protect callers that expect a final text value even when the model only emits structured output parts.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-003</scenario>
    /// <behavior>When text parts are absent, the mapper serializes the structured content into the final text field.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_UsesStructuredContentAsFinalTextWhenTextIsMissing()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Respond with structured output.",
        });

        OpenAiResponsesResponse response = new("resp-structured", new JsonObject
        {
            ["id"] = "resp-structured",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "message",
                    ["content"] = new JsonArray
                    {
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

        AgentFinalOutput finalOutput = Assert.IsType<AgentFinalOutput>(turn.FinalOutput);
        Assert.NotNull(finalOutput.StructuredValue);
        Assert.Equal(finalOutput.StructuredValue!.ToJsonString(), finalOutput.Text);
        Assert.Equal("resp-structured", finalOutput.ResponseId);
    }

    /// <summary>Typed SDK message items preserve the first final output instead of overwriting it with later messages.</summary>
    /// <intent>Protect the typed response-item path, including coalescing behavior for final text and structured content.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-006</scenario>
    /// <behavior>When multiple typed message items are present, the mapper keeps the first final text and structured payload while still recording message run items.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_KeepsFirstTypedMessageOutputWhenLaterMessagesExist()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Respond with typed messages.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-message",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "message",
                    ["id"] = "msg_1",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "output_text",
                            ["text"] = "first",
                        },
                        new JsonObject
                        {
                            ["type"] = "output_json",
                            ["value"] = new JsonObject
                            {
                                ["value"] = "first-structured",
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "message",
                    ["id"] = "msg_2",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "output_text",
                            ["text"] = "second",
                        },
                        new JsonObject
                        {
                            ["type"] = "output_json",
                            ["value"] = new JsonObject
                            {
                                ["value"] = "second-structured",
                            },
                        },
                    },
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentFinalOutput finalOutput = Assert.IsType<AgentFinalOutput>(turn.FinalOutput);
        Assert.Equal("first", finalOutput.Text);
        Assert.Equal("first-structured", finalOutput.StructuredValue?["value"]?["value"]?.GetValue<string>());

        IReadOnlyList<AgentRunItem> items = Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items);
        Assert.Equal(2, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(AgentItemTypes.MessageOutput, item.ItemType);
            Assert.Equal("assistant", item.Role);
            Assert.Equal("first", item.Text);
            Assert.Equal("first-structured", item.Data?["value"]?["value"]?.GetValue<string>());
        });
    }

    /// <summary>Typed MCP approval items map directly into approval-required tool calls.</summary>
    /// <intent>Protect the typed MCP approval path used when the SDK successfully parses the response result.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-007</scenario>
    /// <behavior>Typed MCP approval items preserve the approval id, tool name, parsed arguments, and MCP tool type.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedMcpApprovalItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle approvals.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-approval",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_approval_request",
                    ["id"] = "apr_typed",
                    ["server_label"] = "mail",
                    ["name"] = "delete_message",
                    ["arguments"] = """{"message_id":"msg_42"}""",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.Equal("apr_typed", toolCall.CallId);
        Assert.Equal("delete_message", toolCall.ToolName);
        Assert.True(toolCall.RequiresApproval);
        Assert.Equal("mcp", toolCall.ToolType);
        Assert.Equal("msg_42", toolCall.Arguments?["message_id"]?.GetValue<string>());
    }

    /// <summary>Typed MCP tool-call items surface as executable MCP tool calls.</summary>
    /// <intent>Protect the typed MCP call path after SDK parsing succeeds.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-008</scenario>
    /// <behavior>Typed MCP call items preserve their call id, name, status, parsed arguments, and MCP tool type.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedMcpToolCallItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle MCP calls.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-mcp-call",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_call",
                    ["id"] = "mcp_call_1",
                    ["server_label"] = "mail",
                    ["name"] = "search_mail",
                    ["arguments"] = """{"query":"invoice"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.Equal("mcp_call_1", toolCall.CallId);
        Assert.Equal("search_mail", toolCall.ToolName);
        Assert.Equal("mcp", toolCall.ToolType);
        Assert.Equal("invoice", toolCall.Arguments?["query"]?.GetValue<string>());
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Typed MCP tool-list items surface as MCP list-tools run items.</summary>
    /// <intent>Protect the typed MCP tool-list path used by hosted connector discovery.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-009</scenario>
    /// <behavior>Typed MCP tool-list items emit a list-tools run item with the expected system role and type.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedMcpToolListItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Inspect MCP tools.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-mcp-list",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_list_tools",
                    ["server_label"] = "mail",
                    ["tools"] = new JsonArray(),
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        IReadOnlyList<AgentRunItem> items = Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items);
        AgentRunItem runItem = Assert.Single(items);
        Assert.Equal(AgentItemTypes.McpListTools, runItem.ItemType);
        Assert.Equal("system", runItem.Role);
        Assert.Equal("mcp_list_tools", runItem.Data?["type"]?.GetValue<string>());
    }

    /// <summary>Streaming tool-call items preserve malformed argument payloads instead of throwing.</summary>
    /// <intent>Protect incremental UI/event consumers from malformed streamed function arguments.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-001</scenario>
    /// <behavior>Malformed streamed function-call arguments are wrapped in a value object on the emitted run item.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapStreamingOutputItem_WrapsMalformedFunctionArgumentsInValueObject()
    {
        JsonObject item = new()
        {
            ["type"] = "function_call",
            ["id"] = "fc_stream",
            ["call_id"] = "call_stream",
            ["name"] = "lookup_customer",
            ["arguments"] = "{not-json",
            ["status"] = "in_progress",
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        JsonObject arguments = Assert.IsType<JsonObject>(runItem.Data);
        Assert.Equal(AgentItemTypes.ToolCall, runItem.ItemType);
        Assert.Equal("lookup_customer", runItem.Name);
        Assert.Equal("call_stream", runItem.ToolCallId);
        Assert.Equal("{not-json", arguments["value"]?.GetValue<string>());
        Assert.Equal("in_progress", runItem.Status);
    }

    /// <summary>Unknown streaming item types do not emit misleading run items.</summary>
    /// <intent>Keep the streamed item surface narrow and explicit for unsupported item types.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-002</scenario>
    /// <behavior>Unrecognized streamed item types return null instead of being coerced into a generic event.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapStreamingOutputItem_ReturnsNullForUnknownItemTypes()
    {
        JsonObject item = new()
        {
            ["type"] = "hosted_tool_call",
            ["name"] = "unsupported",
        };

        AgentRunItem? runItem = OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item);

        Assert.Null(runItem);
    }

    /// <summary>Malformed reasoning payloads still surface as reasoning items during streaming.</summary>
    /// <intent>Protect streamed reasoning persistence from malformed summary shapes emitted by upstream providers.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-003</scenario>
    /// <behavior>When typed reasoning deserialization fails, the mapper falls back to the raw reasoning payload instead of throwing.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapStreamingOutputItem_FallsBackToRawReasoningPayloadWhenTypedParsingFails()
    {
        JsonObject item = new()
        {
            ["type"] = "reasoning",
            ["id"] = "rs_bad",
            ["summary"] = new JsonArray("plain-text-summary"),
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.Reasoning, runItem.ItemType);
        Assert.Equal(item.ToJsonString(), runItem.Data?.ToJsonString());
    }

    /// <summary>Typed MCP streaming items map to tool-call run items.</summary>
    /// <intent>Protect the typed streamed MCP call path used by live event consumers.</intent>
    /// <scenario>LIB-OAI-STREAM-POS-001</scenario>
    /// <behavior>Typed streamed MCP call items preserve the name, call id, parsed arguments, and completion status.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void TryMapStreamingOutputItem_MapsTypedMcpToolCallItems()
    {
        JsonObject item = new()
        {
            ["type"] = "mcp_call",
            ["id"] = "mcp_stream_1",
            ["server_label"] = "mail",
            ["name"] = "search_mail",
            ["arguments"] = """{"query":"invoice"}""",
            ["status"] = "completed",
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.ToolCall, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("search_mail", runItem.Name);
        Assert.Equal("mcp_stream_1", runItem.ToolCallId);
        Assert.Equal("invoice", runItem.Data?["query"]?.GetValue<string>());
        Assert.Equal("completed", runItem.Status);
    }

    /// <summary>Typed MCP list-tools streaming items map to system run items.</summary>
    /// <intent>Protect the typed streamed MCP discovery path.</intent>
    /// <scenario>LIB-OAI-STREAM-POS-002</scenario>
    /// <behavior>Typed streamed MCP list-tools items emit a system run item carrying the cloned raw payload.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void TryMapStreamingOutputItem_MapsTypedMcpToolListItems()
    {
        JsonObject item = new()
        {
            ["type"] = "mcp_list_tools",
            ["server_label"] = "mail",
            ["tools"] = new JsonArray(),
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.McpListTools, runItem.ItemType);
        Assert.Equal("system", runItem.Role);
        Assert.Equal(item.ToJsonString(), runItem.Data?.ToJsonString());
    }

    /// <summary>Malformed MCP tool-list payloads do not crash response mapping.</summary>
    /// <intent>Protect hosted MCP discovery from malformed upstream tool metadata.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-004</scenario>
    /// <behavior>When typed MCP tool-list serialization fails, the mapper still emits an MCP list-tools run item with fallback metadata.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallsBackWhenMcpToolListSerializationFails()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Inspect MCP tools.",
        });

        OpenAiResponsesResponse response = new("resp-mcp-list", new JsonObject
        {
            ["id"] = "resp-mcp-list",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_list_tools",
                    ["server_label"] = "mail",
                    ["tools"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "search_mail",
                        },
                    },
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        IReadOnlyList<AgentRunItem> items = Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items);
        AgentRunItem runItem = Assert.Single(items);
        JsonObject data = Assert.IsType<JsonObject>(runItem.Data);
        Assert.Equal(AgentItemTypes.McpListTools, runItem.ItemType);
        Assert.Equal("mcp_list_tools", data["type"]?.GetValue<string>());
    }

    /// <summary>Malformed raw response items do not prevent the mapper from emitting fallback run items.</summary>
    /// <intent>Protect top-level response processing when one output item cannot be deserialized by the OpenAI SDK types.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-005</scenario>
    /// <behavior>When typed response parsing fails, the mapper falls back to the raw output array and still emits a reasoning run item.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallsBackToRawOutputWhenTypedResponseParsingFails()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle reasoning.",
        });

        JsonObject rawReasoning = new()
        {
            ["type"] = "reasoning",
            ["id"] = "rs_bad",
            ["summary"] = new JsonArray("plain-text-summary"),
        };

        OpenAiResponsesResponse response = new("resp-bad-reasoning", new JsonObject
        {
            ["id"] = "resp-bad-reasoning",
            ["output"] = new JsonArray
            {
                rawReasoning.DeepClone(),
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        IReadOnlyList<AgentRunItem> items = Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items);
        AgentRunItem runItem = Assert.Single(items);
        Assert.Equal(AgentItemTypes.Reasoning, runItem.ItemType);
        Assert.Equal(rawReasoning.ToJsonString(), runItem.Data?.ToJsonString());
    }

    /// <summary>Raw-output fallback still maps MCP approval and call items when top-level typed parsing fails.</summary>
    /// <intent>Protect raw fallback handling for hosted MCP items in partially malformed responses.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-010</scenario>
    /// <behavior>When typed response parsing fails, raw MCP approval and MCP call items still map to MCP tool calls with parsed arguments.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawMcpApprovalAndCallItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle MCP fallback items.",
        });

        OpenAiResponsesResponse response = new("resp-raw-mcp", new JsonObject
        {
            ["id"] = "resp-raw-mcp",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "reasoning",
                    ["id"] = "rs_bad",
                    ["summary"] = new JsonArray("plain-text-summary"),
                },
                new JsonObject
                {
                    ["type"] = "mcp_approval_request",
                    ["id"] = "apr_raw",
                    ["name"] = "delete_message",
                    ["arguments"] = """{"message_id":"msg_42"}""",
                    ["approval_reason"] = "confirm destructive action",
                },
                new JsonObject
                {
                    ["type"] = "mcp_call",
                    ["id"] = "mcp_raw",
                    ["name"] = "search_mail",
                    ["arguments"] = """{"query":"invoice"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        Assert.Equal(2, turn.ToolCalls.Count);

        AgentToolCall<TestContext> approval = Assert.Single(turn.ToolCalls, call => call.RequiresApproval);
        Assert.Equal("apr_raw", approval.CallId);
        Assert.Equal("mcp", approval.ToolType);
        Assert.Equal("msg_42", approval.Arguments?["message_id"]?.GetValue<string>());

        AgentToolCall<TestContext> mcpCall = Assert.Single(turn.ToolCalls, call => !call.RequiresApproval);
        Assert.Equal("mcp_raw", mcpCall.CallId);
        Assert.Equal("search_mail", mcpCall.ToolName);
        Assert.Equal("mcp", mcpCall.ToolType);
        Assert.Equal("invoice", mcpCall.Arguments?["query"]?.GetValue<string>());
    }

    private static ValueTask<OpenAiResponsesTurnPlan<TestContext>> CreatePlanAsync(Agent<TestContext> agent)
        => new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", agent.Name) { Text = "hello" },
            ],
            "hello",
            null,
            null));

    private static OpenAiResponsesResponse CreateTypedResponse(JsonObject raw)
        => new(OpenAiSdkSerialization.ReadModel<ResponseResult>(raw));

    private sealed record TestContext(string UserId, string TenantId);
}
