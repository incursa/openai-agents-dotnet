#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.Reflection;
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
        Assert.Equal(ToolOriginType.Mcp, toolCall.ToolOrigin?.Type);
        Assert.Equal("mail", toolCall.ToolOrigin?.McpServerName);
        Assert.Equal("msg_42", toolCall.Arguments?["message_id"]?.GetValue<string>());
    }

    /// <summary>Typed MCP approval items still map when ids and arguments are omitted.</summary>
    /// <intent>Protect the typed MCP approval branch that generates fallback ids and preserves explicit approval reasons.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-015</scenario>
    /// <behavior>Typed MCP approval items without ids or arguments still map to approval-required MCP tool calls with generated ids and preserved approval reasons.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedMcpApprovalItemsWithoutOptionalFields()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle approvals.",
        });

        OpenAiResponsesResponse response = CreateTypedResponseWithoutRoundTrip(new JsonObject
        {
            ["id"] = "resp-typed-approval-no-id",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_approval_request",
                    ["name"] = "delete_message",
                    ["approval_reason"] = "confirm destructive action",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal("delete_message", toolCall.ToolName);
        Assert.True(toolCall.RequiresApproval);
        Assert.Equal("confirm destructive action", toolCall.ApprovalReason);
        Assert.Equal("mcp", toolCall.ToolType);
        Assert.Equal(ToolOriginType.Mcp, toolCall.ToolOrigin?.Type);
        Assert.Null(toolCall.Arguments);
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
        Assert.Equal(ToolOriginType.Mcp, toolCall.ToolOrigin?.Type);
        Assert.Equal("mail", toolCall.ToolOrigin?.McpServerName);
        Assert.Equal("invoice", toolCall.Arguments?["query"]?.GetValue<string>());
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Typed MCP tool-call items still map when their ids are omitted.</summary>
    /// <intent>Protect the typed MCP call branch that generates fallback ids and keeps MCP calls non-approval by default.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-016</scenario>
    /// <behavior>Typed MCP call items without ids still map to executable MCP tool calls with generated ids and parsed arguments.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedMcpToolCallItemsWithoutIds()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle MCP calls.",
        });

        OpenAiResponsesResponse response = CreateTypedResponseWithoutRoundTrip(new JsonObject
        {
            ["id"] = "resp-typed-mcp-call-no-id",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_call",
                    ["name"] = "search_mail",
                    ["arguments"] = """{"query":"invoice"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal("search_mail", toolCall.ToolName);
        Assert.Equal("mcp", toolCall.ToolType);
        Assert.False(toolCall.RequiresApproval);
        Assert.Equal(ToolOriginType.Mcp, toolCall.ToolOrigin?.Type);
        Assert.Equal("invoice", toolCall.Arguments?["query"]?.GetValue<string>());
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
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Unhandled typed hosted-tool items do not fabricate actionable runtime state.</summary>
    /// <intent>Protect the typed response-item default branch from inventing tool calls, handoffs, or final output for hosted-tool items the mapper does not yet project.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-019</scenario>
    /// <behavior>Typed `web_search_call` items are ignored by the mapper rather than being coerced into unrelated runtime items.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_IgnoresUnhandledTypedHostedToolItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle hosted tool results.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-web-search",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "web_search_call",
                    ["id"] = "ws_1",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        Assert.Empty(turn.ToolCalls);
        Assert.Empty(turn.Handoffs);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items));
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Typed reasoning items are emitted as reasoning run items when SDK parsing succeeds.</summary>
    /// <intent>Protect the typed reasoning branch in the top-level response mapper.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-011</scenario>
    /// <behavior>Typed reasoning output items emit reasoning run items and preserve the serialized reasoning type metadata.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedReasoningItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle reasoning.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-reasoning",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "reasoning",
                    ["id"] = "rs_1",
                    ["summary"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] = "thinking",
                        },
                    },
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        IReadOnlyList<AgentRunItem> items = Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items);
        AgentRunItem runItem = Assert.Single(items);
        Assert.Equal(AgentItemTypes.Reasoning, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("reasoning", runItem.Data?["type"]?.GetValue<string>());
        Assert.Null(turn.FinalOutput);
    }

    /// <summary>Typed function-call items preserve status and tool metadata through the top-level mapper.</summary>
    /// <intent>Protect the typed function-call branch when the SDK parses the output item successfully.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-012</scenario>
    /// <behavior>Typed function-call items map to function tool calls with their parsed arguments and status.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedFunctionCallItems()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle tool calls.",
        });

        OpenAiResponsesResponse response = CreateTypedResponse(new JsonObject
        {
            ["id"] = "resp-typed-function-call",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = "fc_1",
                    ["call_id"] = "call_1",
                    ["name"] = "lookup_customer",
                    ["arguments"] = """{"customer_id":"42"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.Equal("call_1", toolCall.CallId);
        Assert.Equal("lookup_customer", toolCall.ToolName);
        Assert.Equal("function", toolCall.ToolType);
        Assert.False(toolCall.RequiresApproval);
        Assert.Equal(ToolOriginType.Function, toolCall.ToolOrigin?.Type);
        Assert.Equal("42", toolCall.Arguments?["customer_id"]?.GetValue<string>());
    }

    /// <summary>Typed function-call items retain status when they route through handoff mapping without a raw round-trip.</summary>
    /// <intent>Protect the typed function-call branch that now reads status directly from the SDK model instead of fallback serialization.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-017</scenario>
    /// <behavior>Typed function-call handoffs without a raw round-trip preserve parsed arguments and completion status in the handoff request.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedFunctionCallItemsWithoutRoundTripIntoHandoffs()
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

        OpenAiResponsesResponse response = CreateTypedResponseWithoutRoundTrip(new JsonObject
        {
            ["id"] = "resp-typed-function-handoff",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function_call",
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

    /// <summary>Typed MCP call items retain status when they route through handoff mapping without a raw round-trip.</summary>
    /// <intent>Protect the typed MCP call branch that now reads status from the SDK patch instead of fallback serialization.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-018</scenario>
    /// <behavior>Typed MCP handoffs without a raw round-trip preserve parsed arguments and completion status in the handoff request.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task ResponseMapper_MapsTypedMcpToolCallItemsWithoutRoundTripIntoHandoffs()
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

        OpenAiResponsesResponse response = CreateTypedResponseWithoutRoundTrip(new JsonObject
        {
            ["id"] = "resp-typed-mcp-handoff",
            ["output"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp_call",
                    ["name"] = "route_to_mail",
                    ["arguments"] = """{"query":"invoice"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentHandoffRequest<TestContext> handoff = Assert.Single(turn.Handoffs);
        JsonObject arguments = Assert.IsType<JsonObject>(handoff.Arguments);
        Assert.Equal("mail", handoff.HandoffName);
        Assert.Same(mailAgent, handoff.TargetAgent);
        Assert.Equal("invoice", arguments["query"]?.GetValue<string>());
        Assert.Equal("completed", handoff.Reason);
        Assert.Empty(turn.ToolCalls);
        Assert.Null(turn.FinalOutput);
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
        Assert.Equal(ToolOriginType.Function, runItem.ToolOrigin?.Type);
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

    /// <summary>Typed streamed message items map through the typed helper branch when SDK parsing succeeds.</summary>
    /// <intent>Protect the typed message branch in the streaming item mapper.</intent>
    /// <scenario>LIB-OAI-STREAM-POS-003</scenario>
    /// <behavior>Typed streamed message items emit message-output run items with their extracted text and structured payload.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void TryMapStreamingOutputItem_MapsTypedMessageItems()
    {
        JsonObject item = new()
        {
            ["type"] = "message",
            ["id"] = "msg_stream",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = "hello",
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
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.MessageOutput, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("hello", runItem.Text);
        Assert.Equal("hello", runItem.Data?["value"]?["value"]?.GetValue<string>());
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
        Assert.Equal(ToolOriginType.Mcp, runItem.ToolOrigin?.Type);
        Assert.Equal("mail", runItem.ToolOrigin?.McpServerName);
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

    /// <summary>Malformed streamed message payloads fall back to the raw message-item mapper.</summary>
    /// <intent>Protect the raw streaming message fallback when typed SDK parsing rejects the payload shape.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-004</scenario>
    /// <behavior>Malformed streamed message items still emit message-output run items from the raw payload.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapStreamingOutputItem_FallsBackToRawMessagePayloadWhenTypedParsingFails()
    {
        JsonObject item = new()
        {
            ["type"] = "message",
            ["content"] = new JsonObject
            {
                ["text"] = "hello",
                ["value"] = "structured",
            },
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.MessageOutput, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("hello", runItem.Text);
    }

    /// <summary>Malformed streamed function-call payloads fall back to the raw tool-call mapper.</summary>
    /// <intent>Protect the raw streaming tool-call fallback when typed SDK parsing rejects the argument shape.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-005</scenario>
    /// <behavior>Malformed streamed function-call items still emit tool-call run items with the raw object arguments preserved.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapStreamingOutputItem_FallsBackToRawFunctionCallPayloadWhenTypedParsingFails()
    {
        JsonObject item = new()
        {
            ["type"] = "function_call",
            ["id"] = "fc_raw_stream",
            ["name"] = "lookup_customer",
            ["arguments"] = new JsonObject
            {
                ["customer_id"] = "42",
            },
            ["status"] = "completed",
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(OpenAiResponsesResponseMapper.TryMapStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.ToolCall, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("lookup_customer", runItem.Name);
        Assert.Equal("fc_raw_stream", runItem.ToolCallId);
        Assert.Equal("42", runItem.Data?["customer_id"]?.GetValue<string>());
        Assert.Equal("completed", runItem.Status);
    }

    /// <summary>Malformed streamed MCP tool-list payloads fall back to the raw list-tools mapper.</summary>
    /// <intent>Protect the raw streaming MCP list-tools fallback when typed parsing fails.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-006</scenario>
    /// <behavior>Malformed streamed MCP list-tools items still emit MCP list-tools run items carrying the raw payload.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapStreamingOutputItem_FallsBackToRawMcpToolListPayloadWhenTypedParsingFails()
    {
        JsonObject item = new()
        {
            ["type"] = "mcp_list_tools",
            ["server_label"] = "mail",
            ["tools"] = "bad-tools-shape",
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

    /// <summary>Raw-output fallback also maps raw messages, raw function calls, and raw MCP list-tools items.</summary>
    /// <intent>Protect the raw response fallback branches that remain reachable when typed SDK parsing fails at the top level.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-013</scenario>
    /// <behavior>When typed response parsing fails, raw message, function-call, and MCP list-tools items still map with correct first-message coalescing and raw metadata.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawMessagesFunctionCallsAndMcpListTools()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle fallback items.",
        });

        OpenAiResponsesResponse response = new("resp-raw-mixed", new JsonObject
        {
            ["id"] = "resp-raw-mixed",
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
                    ["type"] = "message",
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
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "output_text",
                            ["text"] = "second",
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = "fc_raw",
                    ["name"] = "lookup_customer",
                    ["arguments"] = """{"customer_id":"42"}""",
                    ["status"] = "completed",
                    ["approval_required"] = false,
                },
                new JsonObject
                {
                    ["type"] = "mcp_list_tools",
                    ["server_label"] = "mail",
                    ["tools"] = new JsonArray(),
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        Assert.Null(turn.FinalOutput);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.Equal("fc_raw", toolCall.CallId);
        Assert.Equal("lookup_customer", toolCall.ToolName);
        Assert.Equal("function", toolCall.ToolType);
        Assert.Equal("42", toolCall.Arguments?["customer_id"]?.GetValue<string>());

        IReadOnlyList<AgentRunItem> items = Assert.IsAssignableFrom<IReadOnlyList<AgentRunItem>>(turn.Items);
        Assert.Equal(4, items.Count);

        AgentRunItem firstMessage = Assert.IsType<AgentRunItem>(items[1]);
        AgentRunItem secondMessage = Assert.IsType<AgentRunItem>(items[2]);
        AgentRunItem listTools = Assert.IsType<AgentRunItem>(items[3]);

        Assert.Equal(AgentItemTypes.MessageOutput, firstMessage.ItemType);
        Assert.Equal("first", firstMessage.Text);
        Assert.Equal("first-structured", firstMessage.Data?["value"]?["value"]?.GetValue<string>());

        Assert.Equal(AgentItemTypes.MessageOutput, secondMessage.ItemType);
        Assert.Equal("first", secondMessage.Text);
        Assert.Equal("first-structured", secondMessage.Data?["value"]?["value"]?.GetValue<string>());

        Assert.Equal(AgentItemTypes.McpListTools, listTools.ItemType);
        Assert.Equal("mcp_list_tools", listTools.Data?["type"]?.GetValue<string>());
    }

    /// <summary>Raw function-call fallback preserves call-id precedence and explicit approval metadata.</summary>
    /// <intent>Protect the raw function-call fallback path when malformed sibling items force top-level raw mapping.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-017</scenario>
    /// <behavior>Raw function-call items prefer `call_id` over `id`, preserve explicit approval flags and reasons, and keep custom tool types.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackPreservesRawFunctionCallMetadata()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle fallback tool calls.",
        });

        OpenAiResponsesResponse response = new("resp-raw-function-call", new JsonObject
        {
            ["id"] = "resp-raw-function-call",
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
                    ["type"] = "function_call",
                    ["id"] = "fc_raw_id",
                    ["call_id"] = "fc_raw_call",
                    ["name"] = "lookup_customer",
                    ["arguments"] = """{"customer_id":"42"}""",
                    ["status"] = "in_progress",
                    ["approval_required"] = true,
                    ["approval_reason"] = "manager review required",
                    ["tool_type"] = "custom_function",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.Equal("fc_raw_call", toolCall.CallId);
        Assert.Equal("lookup_customer", toolCall.ToolName);
        Assert.Equal("42", toolCall.Arguments?["customer_id"]?.GetValue<string>());
        Assert.True(toolCall.RequiresApproval);
        Assert.Equal("manager review required", toolCall.ApprovalReason);
        Assert.Equal("custom_function", toolCall.ToolType);
    }

    /// <summary>Raw tool-call aliases still map when ids are omitted.</summary>
    /// <intent>Protect the raw `tool_call` alias path and the helper branch that generates fallback call ids.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-018</scenario>
    /// <behavior>Raw `tool_call` items without ids still map to function tool calls with generated ids, parsed arguments, and default non-approval metadata.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawToolCallAliasWithoutIds()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle fallback tool-call aliases.",
        });

        OpenAiResponsesResponse response = new("resp-raw-tool-call-alias", new JsonObject
        {
            ["id"] = "resp-raw-tool-call-alias",
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
                    ["type"] = "tool_call",
                    ["name"] = "lookup_customer",
                    ["arguments"] = """{"customer_id":"42"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal("lookup_customer", toolCall.ToolName);
        Assert.Equal("42", toolCall.Arguments?["customer_id"]?.GetValue<string>());
        Assert.False(toolCall.RequiresApproval);
        Assert.Null(toolCall.ApprovalReason);
        Assert.Equal("function", toolCall.ToolType);
    }

    /// <summary>Raw function-call fallbacks still map when optional metadata is absent.</summary>
    /// <intent>Protect the raw function-call fallback defaults for missing names, arguments, approval metadata, and status.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-020</scenario>
    /// <behavior>Raw `function_call` items without optional fields still map to function tool calls with generated ids, empty names, and null arguments.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawFunctionCallsWithoutOptionalMetadata()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle sparse fallback tool calls.",
        });

        OpenAiResponsesResponse response = new("resp-raw-function-call-sparse", new JsonObject
        {
            ["id"] = "resp-raw-function-call-sparse",
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
                    ["type"] = "function_call",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal(string.Empty, toolCall.ToolName);
        Assert.Null(toolCall.Arguments);
        Assert.False(toolCall.RequiresApproval);
        Assert.Null(toolCall.ApprovalReason);
        Assert.Equal("function", toolCall.ToolType);
    }

    /// <summary>Raw MCP tool-call aliases still map when ids are omitted.</summary>
    /// <intent>Protect the raw `mcp_tool_call` alias path and its fallback-id generation.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-019</scenario>
    /// <behavior>Raw `mcp_tool_call` items without ids still map to MCP tool calls with generated ids and parsed arguments.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawMcpToolCallAliasWithoutIds()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle fallback MCP aliases.",
        });

        OpenAiResponsesResponse response = new("resp-raw-mcp-tool-call-alias", new JsonObject
        {
            ["id"] = "resp-raw-mcp-tool-call-alias",
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
                    ["type"] = "mcp_tool_call",
                    ["name"] = "search_mail",
                    ["arguments"] = """{"query":"invoice"}""",
                    ["status"] = "completed",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal("search_mail", toolCall.ToolName);
        Assert.Equal("invoice", toolCall.Arguments?["query"]?.GetValue<string>());
        Assert.False(toolCall.RequiresApproval);
        Assert.Null(toolCall.ApprovalReason);
        Assert.Equal("mcp", toolCall.ToolType);
    }

    /// <summary>Raw MCP approval fallbacks still map when names are absent.</summary>
    /// <intent>Protect the raw MCP approval fallback defaults for missing tool names and arguments.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-021</scenario>
    /// <behavior>Raw `mcp_approval_request` items without ids, names, or arguments still map to approval-required MCP tool calls with generated ids and empty names.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawApprovalRequestsWithoutNames()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle sparse approval fallback items.",
        });

        OpenAiResponsesResponse response = new("resp-raw-approval-no-name", new JsonObject
        {
            ["id"] = "resp-raw-approval-no-name",
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
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal(string.Empty, toolCall.ToolName);
        Assert.True(toolCall.RequiresApproval);
        Assert.Null(toolCall.Arguments);
        Assert.Null(toolCall.ApprovalReason);
        Assert.Equal("mcp", toolCall.ToolType);
    }

    /// <summary>Raw MCP call fallbacks still map when optional metadata is absent.</summary>
    /// <intent>Protect the raw MCP call fallback defaults for missing names, arguments, and statuses.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-022</scenario>
    /// <behavior>Raw `mcp_call` items without optional fields still map to MCP tool calls with generated ids, empty names, and null arguments.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawMcpCallsWithoutOptionalMetadata()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle sparse MCP fallback items.",
        });

        OpenAiResponsesResponse response = new("resp-raw-mcp-call-sparse", new JsonObject
        {
            ["id"] = "resp-raw-mcp-call-sparse",
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
                    ["type"] = "mcp_call",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal(string.Empty, toolCall.ToolName);
        Assert.Null(toolCall.Arguments);
        Assert.False(toolCall.RequiresApproval);
        Assert.Null(toolCall.ApprovalReason);
        Assert.Equal("mcp", toolCall.ToolType);
    }

    /// <summary>Raw-output fallback maps raw MCP approval items even when optional fields are absent.</summary>
    /// <intent>Protect the raw approval fallback path for approval items that omit ids or reasons.</intent>
    /// <scenario>LIB-OAI-RESP-MAP-014</scenario>
    /// <behavior>Raw MCP approval items without ids still map to approval-required MCP tool calls with generated call ids and null arguments.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task ResponseMapper_FallbackMapsRawApprovalRequestsWithoutOptionalFields()
    {
        OpenAiResponsesTurnPlan<TestContext> plan = await CreatePlanAsync(new Agent<TestContext>
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle approval fallback items.",
        });

        OpenAiResponsesResponse response = new("resp-raw-approval", new JsonObject
        {
            ["id"] = "resp-raw-approval",
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
                    ["name"] = "delete_message",
                },
            },
        });

        AgentTurnResponse<TestContext> turn = new OpenAiResponsesResponseMapper().Map(response, plan);

        AgentToolCall<TestContext> toolCall = Assert.Single(turn.ToolCalls);
        Assert.False(string.IsNullOrWhiteSpace(toolCall.CallId));
        Assert.Equal("delete_message", toolCall.ToolName);
        Assert.True(toolCall.RequiresApproval);
        Assert.Equal("mcp", toolCall.ToolType);
        Assert.Null(toolCall.Arguments);
        Assert.Null(toolCall.ApprovalReason);
    }

    /// <summary>The raw unknown streaming helper preserves message text and structured payloads.</summary>
    /// <intent>Protect the private raw streaming message fallback branch that public parsing does not reach deterministically.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-007</scenario>
    /// <behavior>The unknown streaming helper maps raw message items into message-output run items with extracted text, structured data, and timestamps.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapUnknownStreamingOutputItem_MapsRawMessageItems()
    {
        JsonObject item = new()
        {
            ["type"] = "message",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = "hello",
                },
                new JsonObject
                {
                    ["type"] = "output_json",
                    ["value"] = new JsonObject
                    {
                        ["value"] = "structured",
                    },
                },
            },
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(InvokeUnknownStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.MessageOutput, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("hello", runItem.Text);
        Assert.Equal("structured", runItem.Data?["value"]?["value"]?.GetValue<string>());
        Assert.NotNull(runItem.TimestampUtc);
    }

    /// <summary>The raw unknown streaming helper preserves reasoning payloads.</summary>
    /// <intent>Protect the private raw streaming reasoning fallback branch.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-008</scenario>
    /// <behavior>The unknown streaming helper maps raw reasoning items into reasoning run items carrying the original payload.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapUnknownStreamingOutputItem_MapsRawReasoningItems()
    {
        JsonObject item = new()
        {
            ["type"] = "reasoning",
            ["summary"] = new JsonArray("thinking"),
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(InvokeUnknownStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.Reasoning, runItem.ItemType);
        Assert.Equal(item.ToJsonString(), runItem.Data?.ToJsonString());
        Assert.NotNull(runItem.TimestampUtc);
    }

    /// <summary>The raw unknown streaming helper prefers `call_id` over `id` for raw tool calls.</summary>
    /// <intent>Protect the private raw streaming tool-call fallback branch from regressing call-id precedence.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-009</scenario>
    /// <behavior>The unknown streaming helper maps raw function-call items with `call_id` precedence, parsed arguments, and preserved status.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapUnknownStreamingOutputItem_PrefersCallIdForRawFunctionCalls()
    {
        JsonObject item = new()
        {
            ["type"] = "function_call",
            ["id"] = "fc_id",
            ["call_id"] = "fc_call",
            ["name"] = "lookup_customer",
            ["arguments"] = new JsonObject
            {
                ["customer_id"] = "42",
            },
            ["status"] = "completed",
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(InvokeUnknownStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.ToolCall, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("lookup_customer", runItem.Name);
        Assert.Equal("fc_call", runItem.ToolCallId);
        Assert.Equal("42", runItem.Data?["customer_id"]?.GetValue<string>());
        Assert.Equal("completed", runItem.Status);
        Assert.NotNull(runItem.TimestampUtc);
    }

    /// <summary>The raw unknown streaming helper maps `tool_call` aliases the same way as function calls.</summary>
    /// <intent>Protect the private raw streaming alias branch and keep the remaining timeout line observable.</intent>
    /// <scenario>LIB-OAI-STREAM-NEG-010</scenario>
    /// <behavior>The unknown streaming helper maps raw `tool_call` items into tool-call run items with the expected alias semantics.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public void TryMapUnknownStreamingOutputItem_MapsRawToolCallAliases()
    {
        JsonObject item = new()
        {
            ["type"] = "tool_call",
            ["id"] = "tc_id",
            ["name"] = "lookup_customer",
            ["arguments"] = new JsonObject
            {
                ["customer_id"] = "42",
            },
            ["status"] = "queued",
        };

        AgentRunItem runItem = Assert.IsType<AgentRunItem>(InvokeUnknownStreamingOutputItem("triage", item));

        Assert.Equal(AgentItemTypes.ToolCall, runItem.ItemType);
        Assert.Equal("assistant", runItem.Role);
        Assert.Equal("lookup_customer", runItem.Name);
        Assert.Equal("tc_id", runItem.ToolCallId);
        Assert.Equal("42", runItem.Data?["customer_id"]?.GetValue<string>());
        Assert.Equal("queued", runItem.Status);
        Assert.NotNull(runItem.TimestampUtc);
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

    private static OpenAiResponsesResponse CreateTypedResponseWithoutRoundTrip(JsonObject raw)
    {
        OpenAiResponsesResponse response = new(raw["id"]?.GetValue<string>() ?? string.Empty, raw.DeepClone() as JsonObject ?? new JsonObject());
        PropertyInfo resultProperty = typeof(OpenAiResponsesResponse).GetProperty("Result", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenAiResponsesResponse.Result was not found.");

        resultProperty.SetValue(response, OpenAiSdkSerialization.ReadModel<ResponseResult>(raw));
        return response;
    }

    private static AgentRunItem? InvokeUnknownStreamingOutputItem(string agentName, JsonObject item)
    {
        MethodInfo method = typeof(OpenAiResponsesResponseMapper).GetMethod("TryMapUnknownStreamingOutputItem", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenAiResponsesResponseMapper.TryMapUnknownStreamingOutputItem was not found.");

        return method.Invoke(null, [agentName, item]) as AgentRunItem;
    }

    private sealed record TestContext(string UserId, string TenantId);
}
