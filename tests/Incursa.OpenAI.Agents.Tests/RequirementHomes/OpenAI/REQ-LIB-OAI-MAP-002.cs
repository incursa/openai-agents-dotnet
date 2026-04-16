#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using System.Text;
using System.Text.Json.Nodes;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;
using System.ClientModel.Primitives;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Request mapping preserves the OpenAI Responses turn shape for public consumption.</summary>
public sealed class REQ_LIB_OAI_MAP_002
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

    /// <summary>Request mapping preserves request metadata, tool schemas, handoff translation, hosted MCP translation, and model-setting patches.</summary>
    /// <intent>Protect the public request shape that is sent to the OpenAI Responses API.</intent>
    /// <scenario>LIB-OAI-MAP-002</scenario>
    /// <behavior>Mapped requests keep metadata, preserve explicit tool schemas, fall back to default tool schemas, honor handoff overrides, translate hosted MCP tools, and ignore explicit top-level transport fields in model settings.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_MapsMetadataToolsHandoffsHostedMcpAndModelSettings()
    {
        Agent<TestContext> mailAgent = new()
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            HandoffDescription = "Fallback mail handoff description.",
        };

        Agent<TestContext> triage = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = null,
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
                        ["additionalProperties"] = false,
                    },
                    ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("ok")),
                },
                new AgentTool<TestContext>
                {
                    Name = "lookup_order",
                    Description = "Look up an order",
                    ExecuteAsync = (_, _) => ValueTask.FromResult(AgentToolResult.FromText("ok")),
                },
            ],
            Handoffs =
            [
                new AgentHandoff<TestContext>
                {
                    Name = "mail",
                    TargetAgent = mailAgent,
                    ToolNameOverride = "route_to_mail",
                    Description = "Explicit handoff description.",
                },
            ],
            HostedMcpTools =
            [
                new HostedMcpToolDefinition(
                    "mail",
                    new Uri("https://mail.example.test/mcp"),
                    "connector-1",
                    "Bearer hosted-token",
                    true,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["x-trace"] = "abc",
                        ["x-tenant"] = "tenant-1",
                    },
                    "approval because hosted",
                    "Hosted mail connector"),
            ],
            ModelSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["model"] = "wrong-model",
                ["instructions"] = "wrong instructions",
                ["previous_response_id"] = "wrong-response",
                ["metadata"] = new JsonObject { ["spoof"] = "bad" },
                ["input"] = new JsonArray("bad"),
                ["tools"] = new JsonArray("bad"),
                ["stream"] = true,
                ["custom_string"] = "value",
                ["custom_bool"] = true,
                ["custom_int"] = 42,
                ["custom_long"] = 1234567890123L,
                ["custom_double"] = 12.5d,
                ["custom_null"] = null,
                ["custom_object"] = new JsonObject
                {
                    ["nested_text"] = "inner",
                    ["nested_flag"] = false,
                    ["nested_count"] = 7,
                    ["nested_fallback"] = 12345678901234567890m,
                },
                ["custom_array"] = new JsonArray("alpha", 2, null),
            },
        };

        OpenAiResponsesRequestMapper mapper = new();
        OpenAiResponsesTurnPlan<TestContext> plan = await mapper.CreateAsync(new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            4,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            "Need mail help",
            "resp-previous",
            null));

        Assert.Null(plan.Options.Instructions);
        Assert.Equal("gpt-5.4", plan.Options.Model);
        Assert.Equal("resp-previous", plan.Options.PreviousResponseId);
        Assert.Equal("session-1", plan.Options.Metadata["session_key"]);
        Assert.Equal("triage", plan.Options.Metadata["agent_name"]);
        Assert.Equal("4", plan.Options.Metadata["turn_number"]);
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.model")));
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.instructions")));
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.previous_response_id")));
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.metadata")));
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.input")));
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.tools")));
        Assert.False(plan.Options.Patch.Contains(Encoding.UTF8.GetBytes("$.stream")));

        Assert.Equal(4, plan.Options.Tools.Count);

        FunctionTool explicitTool = Assert.Single(plan.Options.Tools.OfType<FunctionTool>(), tool => tool.FunctionName == "lookup_customer");
        string explicitToolJson = OpenAiSdkSerialization.ToJsonObject(explicitTool).ToJsonString();
        Assert.Contains("\"lookup_customer\"", explicitToolJson);
        Assert.Contains("\"customer_id\"", explicitToolJson);
        Assert.Contains("\"additionalProperties\":false", explicitToolJson);

        FunctionTool fallbackTool = Assert.Single(plan.Options.Tools.OfType<FunctionTool>(), tool => tool.FunctionName == "lookup_order");
        string fallbackToolJson = OpenAiSdkSerialization.ToJsonObject(fallbackTool).ToJsonString();
        Assert.Contains("\"lookup_order\"", fallbackToolJson);
        Assert.Contains("\"additionalProperties\":true", fallbackToolJson);

        FunctionTool handoffTool = Assert.Single(plan.Options.Tools.OfType<FunctionTool>(), tool => tool.FunctionName == "route_to_mail");
        string handoffToolJson = OpenAiSdkSerialization.ToJsonObject(handoffTool).ToJsonString();
        Assert.Contains("\"route_to_mail\"", handoffToolJson);
        Assert.Contains("Explicit handoff description.", handoffToolJson);
        Assert.DoesNotContain("Fallback mail handoff description.", handoffToolJson);
        Assert.Contains("\"additionalProperties\":true", handoffToolJson);

        Assert.Single(plan.HandoffMap);
        Assert.Contains("route_to_mail", plan.HandoffMap);

        McpTool hostedTool = Assert.Single(plan.Options.Tools.OfType<McpTool>());
        Assert.Equal("connector-1", hostedTool.ConnectorId);
        Assert.Equal("Bearer hosted-token", hostedTool.AuthorizationToken);
        Assert.Equal("Hosted mail connector", hostedTool.ServerDescription);
        Assert.Equal("abc", hostedTool.Headers?["x-trace"]);
        Assert.Equal("tenant-1", hostedTool.Headers?["x-tenant"]);
        JsonObject hostedToolJson = OpenAiSdkSerialization.ToJsonObject(hostedTool);
        Assert.Equal("always", hostedToolJson["require_approval"]?.GetValue<string>());
    }

    /// <summary>Custom model-setting patches survive SDK transport serialization and reach the wire body.</summary>
    /// <intent>Prove that the public model-settings dictionary is not just retained in-memory but actually serialized into the outgoing Responses request.</intent>
    /// <scenario>LIB-OAI-MAP-002C</scenario>
    /// <behavior>Custom model-setting keys are serialized into the OpenAI Responses request body while explicit top-level transport fields remain controlled by the mapper.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_SerializesCustomModelSettingsOntoTheWire()
    {
        Agent<TestContext> triage = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            ModelSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["model"] = "wrong-model",
                ["instructions"] = "wrong instructions",
                ["previous_response_id"] = "wrong-response",
                ["metadata"] = new JsonObject { ["spoof"] = "bad" },
                ["stream"] = true,
                ["custom_string"] = "value",
                ["custom_bool"] = true,
                ["custom_int"] = 42,
                ["custom_long"] = 1234567890123L,
                ["custom_double"] = 12.5d,
                ["custom_null"] = null,
                ["custom_object"] = new JsonObject
                {
                    ["nested_text"] = "inner",
                    ["nested_flag"] = false,
                    ["nested_count"] = 7,
                    ["nested_fallback"] = 12345678901234567890m,
                },
                ["custom_array"] = new JsonArray("alpha", 2, null),
            },
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            5,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            "Need mail help",
            "resp-previous",
            null));

        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-key");

        OpenAiResponsesClient client = new(httpClient, "v1/responses");
        await client.CreateResponseAsync(new OpenAiResponsesRequest(plan.Options), CancellationToken.None);

        JsonObject body = JsonNode.Parse(handler.Body)?.AsObject()
            ?? throw new InvalidOperationException("The captured request body was not valid JSON.");

        Assert.Equal("gpt-5.4", body["model"]?.GetValue<string>());
        Assert.Equal("Handle mail", body["instructions"]?.GetValue<string>());
        Assert.Equal("resp-previous", body["previous_response_id"]?.GetValue<string>());
        Assert.False(body["stream"]?.GetValue<bool>() ?? true);

        JsonObject metadata = Assert.IsType<JsonObject>(body["metadata"]);
        Assert.Equal("session-1", metadata["session_key"]?.GetValue<string>());
        Assert.Equal("triage", metadata["agent_name"]?.GetValue<string>());
        Assert.Equal("5", metadata["turn_number"]?.GetValue<string>());
        Assert.DoesNotContain("spoof", metadata.ToJsonString());

        Assert.Equal("value", body["custom_string"]?.GetValue<string>());
        Assert.True(body["custom_bool"]?.GetValue<bool>() ?? false);
        Assert.Equal(42, body["custom_int"]?.GetValue<int>());
        Assert.Equal(1234567890123L, body["custom_long"]?.GetValue<long>());
        Assert.Equal(12.5d, body["custom_double"]?.GetValue<double>());
        Assert.True(body.ContainsKey("custom_null"));
        Assert.Null(body["custom_null"]);

        JsonObject customObject = Assert.IsType<JsonObject>(body["custom_object"]);
        Assert.Equal("inner", customObject["nested_text"]?.GetValue<string>());
        Assert.False(customObject["nested_flag"]?.GetValue<bool>() ?? true);
        Assert.Equal(7, customObject["nested_count"]?.GetValue<int>());
        Assert.Equal(12345678901234567890m, customObject["nested_fallback"]?.GetValue<decimal>());

        JsonArray customArray = Assert.IsType<JsonArray>(body["custom_array"]);
        Assert.Equal("alpha", customArray[0]?.GetValue<string>());
        Assert.Equal(2, customArray[1]?.GetValue<int>());
        Assert.Null(customArray[2]);
    }

    /// <summary>Reasoning items preserve the joined summary text, status, encrypted content, and omission policy.</summary>
    /// <intent>Protect reasoning-item fidelity when mapped into the OpenAI Responses SDK model.</intent>
    /// <scenario>LIB-OAI-MAP-002B</scenario>
    /// <behavior>Reasoning input preserves the readable summary text, keeps status and encrypted content, and omits the reasoning item ID when requested.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_PreservesReasoningSummaryStatusEncryptedContentAndIdPolicy()
    {
        Agent<TestContext> agent = new()
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle the delegated task.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-reasoning",
            2,
            [
                new AgentConversationItem(AgentItemTypes.Reasoning, "assistant", "delegate")
                {
                    Data = new JsonObject
                    {
                        ["type"] = "reasoning",
                        ["id"] = "rs_123",
                        ["status"] = "completed",
                        ["encrypted_content"] = "enc-abc",
                        ["summary"] = new JsonArray
                        {
                            "first line",
                            new JsonObject { ["text"] = "second line" },
                            new JsonObject { ["text"] = string.Empty },
                            42,
                        },
                    },
                },
            ],
            null,
            null,
            new AgentRunOptions<TestContext> { ReasoningItemIdPolicy = ReasoningItemIdPolicy.Omit }));

        ReasoningResponseItem reasoning = Assert.IsType<ReasoningResponseItem>(Assert.Single(plan.Options.InputItems));
        Assert.Null(reasoning.Id);
        Assert.Equal(ReasoningStatus.Completed, reasoning.Status);
        Assert.Equal("enc-abc", reasoning.EncryptedContent);
        Assert.Equal(string.Join(Environment.NewLine, ["first line", "second line"]), ReadReasoningSummary(reasoning));
    }

    /// <summary>Missing models fail fast before any request mapping work begins.</summary>
    /// <intent>Protect the request-mapper guard that rejects OpenAI runs without a concrete model id.</intent>
    /// <scenario>LIB-OAI-MAP-002D</scenario>
    /// <behavior>Agents with blank model identifiers throw an informative `InvalidOperationException` during request mapping.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task RequestMapper_RejectsAgentsWithoutModels()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "   ",
            Instructions = "Handle requests.",
        };

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
                agent,
                new TestContext("user-1", "tenant-1"),
                "session-missing-model",
                1,
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
                ],
                "hello",
                null,
                null)).AsTask());

        Assert.Contains("must specify a model", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Disabled handoffs are omitted, while enabled handoffs fall back to target-agent descriptions and default schemas.</summary>
    /// <intent>Protect handoff enablement checks and fallback handoff metadata when explicit overrides are not supplied.</intent>
    /// <scenario>LIB-OAI-MAP-002E</scenario>
    /// <behavior>Disabled handoffs do not produce tools, and enabled handoffs without explicit descriptions or schemas use the target agent fallback description and default object schema.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_SkipsDisabledHandoffsAndUsesFallbackDescriptions()
    {
        Agent<TestContext> mailAgent = new()
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            HandoffDescription = "Fallback mail handoff description.",
        };

        Agent<TestContext> calendarAgent = new()
        {
            Name = "calendar specialist",
            Model = "gpt-5.4",
            Instructions = "Handle calendars",
            HandoffDescription = "Fallback calendar handoff description.",
        };

        Agent<TestContext> triage = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Route work",
            Handoffs =
            [
                new AgentHandoff<TestContext>
                {
                    Name = "mail",
                    TargetAgent = mailAgent,
                },
                new AgentHandoff<TestContext>
                {
                    Name = "calendar",
                    TargetAgent = calendarAgent,
                    ToolNameOverride = "route_to_calendar",
                    IsEnabledAsync = static (_, _) => ValueTask.FromResult(false),
                },
            ],
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-handoffs",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
            ],
            "hello",
            null,
            null));

        FunctionTool handoffTool = Assert.Single(plan.Options.Tools.OfType<FunctionTool>());
        JsonObject handoffJson = OpenAiSdkSerialization.ToJsonObject(handoffTool);

        Assert.Equal("transfer_to_mail_specialist", handoffTool.FunctionName);
        Assert.Contains("transfer_to_mail_specialist", plan.HandoffMap.Keys);
        Assert.DoesNotContain("route_to_calendar", plan.HandoffMap.Keys);
        Assert.Contains("Fallback mail handoff description.", handoffJson.ToJsonString());
        Assert.Contains("\"type\":\"object\"", handoffJson.ToJsonString());
        Assert.Contains("\"properties\":{}", handoffJson.ToJsonString());
        Assert.Contains("\"additionalProperties\":true", handoffJson.ToJsonString());
    }

    /// <summary>Custom handoff-history transformers receive the correct source, target, and argument context after a handoff.</summary>
    /// <intent>Protect the request-mapper path that delegates post-handoff normalization to caller-provided transformers.</intent>
    /// <scenario>LIB-OAI-MAP-002F</scenario>
    /// <behavior>When post-handoff normalization is enabled, the transformer is invoked with the last handoff source agent and arguments for the current target agent.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_PassesCorrectContextToHandoffHistoryTransformer()
    {
        Agent<TestContext> delegateAgent = new()
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle delegated work.",
        };

        AgentHandoffHistoryTransformContext<TestContext>? captured = null;
        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            delegateAgent,
            new TestContext("user-1", "tenant-1"),
            "session-transform",
            2,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "help" },
                new AgentConversationItem(AgentItemTypes.ToolCall, "assistant", "triage") { Name = "lookup_customer", ToolCallId = "call-1", Data = new JsonObject { ["customer_id"] = "42" } },
                new AgentConversationItem(AgentItemTypes.HandoffRequested, "assistant", "triage") { Name = "mail", Text = "delegate", Data = new JsonObject { ["topic"] = "mail" } },
                new AgentConversationItem(AgentItemTypes.HandoffOccurred, "system", "delegate") { Name = "mail", Text = "delegate", Data = new JsonObject { ["topic"] = "mail" } },
                new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "delegate") { Text = "later message", Data = new JsonObject { ["topic"] = "wrong" } },
            ],
            null,
            "resp-1",
            new AgentRunOptions<TestContext>
            {
                HandoffHistoryMode = AgentHandoffHistoryMode.NormalizeModelInputAfterHandoff,
                HandoffHistoryTransformerAsync = (context, _) =>
                {
                    captured = context;
                    return ValueTask.FromResult<IReadOnlyList<AgentConversationItem>>(context.Conversation);
                },
            }));

        Assert.NotNull(captured);
        Assert.Equal("triage", captured!.CurrentAgentName);
        Assert.Equal("delegate", captured.TargetAgentName);
        Assert.Equal("mail", captured.Arguments?["topic"]?.GetValue<string>());
        Assert.Equal(5, plan.Options.InputItems.Count);
    }

    /// <summary>Conversation items preserve roles, fallback raw item fields, tool defaults, and reasoning normalization.</summary>
    /// <intent>Protect the per-item mapping branches that feed the OpenAI Responses input array.</intent>
    /// <scenario>LIB-OAI-MAP-002G</scenario>
    /// <behavior>Mapped input preserves message roles, raw fallback item fields, generated tool-call ids, serialized tool outputs, and reasoning defaults for blank ids and non-array summaries.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_MapsConversationItemsAcrossRolesFallbacksAndStatuses()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle requests.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-items",
            1,
            [
                new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "triage") { Text = "assistant reply" },
                new AgentConversationItem(AgentItemTypes.FinalOutput, "system", "triage") { Text = "system note" },
                new AgentConversationItem(AgentItemTypes.GuardrailTripwire, "developer", "triage") { Text = "developer note" },
                new AgentConversationItem(AgentItemTypes.ApprovalRejected, "user", "triage") { Text = "user note" },
                new AgentConversationItem(AgentItemTypes.ToolCall, "assistant", "triage") { Status = "completed" },
                new AgentConversationItem(AgentItemTypes.ToolOutput, "tool", "triage") { Data = new JsonObject { ["result"] = "ok" }, Status = "completed" },
                new AgentConversationItem(AgentItemTypes.Reasoning, "assistant", "triage")
                {
                    Data = new JsonObject
                    {
                        ["id"] = " ",
                        ["status"] = "completed",
                        ["encrypted_content"] = "enc-value",
                        ["summary"] = "not-an-array",
                    },
                },
                new AgentConversationItem("custom_item", "tool", "triage")
                {
                    Name = "raw_item",
                    Text = "payload",
                    ToolCallId = "call-raw",
                    Data = new JsonObject { ["value"] = 1 },
                },
            ],
            null,
            null,
            null));

        JsonObject assistantMessage = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[0]);
        JsonObject systemMessage = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[1]);
        JsonObject developerMessage = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[2]);
        JsonObject userMessage = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[3]);
        JsonObject toolCall = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[4]);
        JsonObject toolOutput = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[5]);
        ReasoningResponseItem reasoning = Assert.IsType<ReasoningResponseItem>(plan.Options.InputItems[6]);
        JsonObject rawItem = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[7]);

        Assert.Equal("assistant", assistantMessage["role"]?.GetValue<string>());
        Assert.Equal("output_text", assistantMessage["content"]?[0]?["type"]?.GetValue<string>());
        Assert.Equal("assistant reply", assistantMessage["content"]?[0]?["text"]?.GetValue<string>());
        Assert.Equal("system", systemMessage["role"]?.GetValue<string>());
        Assert.Equal("input_text", systemMessage["content"]?[0]?["type"]?.GetValue<string>());
        Assert.Equal("system note", systemMessage["content"]?[0]?["text"]?.GetValue<string>());
        Assert.Equal("developer", developerMessage["role"]?.GetValue<string>());
        Assert.Equal("user", userMessage["role"]?.GetValue<string>());

        Assert.Matches("^[0-9a-f]{32}$", toolCall["call_id"]?.GetValue<string>() ?? string.Empty);
        Assert.Equal(string.Empty, toolCall["name"]?.GetValue<string>());
        Assert.Equal("{}", toolCall["arguments"]?.GetValue<string>());
        Assert.Equal("completed", toolCall["status"]?.GetValue<string>());

        Assert.Matches("^[0-9a-f]{32}$", toolOutput["call_id"]?.GetValue<string>() ?? string.Empty);
        Assert.Equal("""{"result":"ok"}""", toolOutput["output"]?.GetValue<string>());
        Assert.Equal("completed", toolOutput["status"]?.GetValue<string>());

        Assert.Null(reasoning.Id);
        Assert.Equal(ReasoningStatus.Completed, reasoning.Status);
        Assert.Equal("enc-value", reasoning.EncryptedContent);
        Assert.Equal(string.Empty, ReadReasoningSummary(reasoning));
        Assert.Equal("reasoning", OpenAiSdkSerialization.ToJsonObject(reasoning)["type"]?.GetValue<string>());

        Assert.Equal("custom_item", rawItem["type"]?.GetValue<string>());
        Assert.Equal("tool", rawItem["role"]?.GetValue<string>());
        Assert.Equal("raw_item", rawItem["name"]?.GetValue<string>());
        Assert.Equal("payload", rawItem["content"]?.GetValue<string>());
        Assert.Equal("call-raw", rawItem["tool_call_id"]?.GetValue<string>());
        Assert.Equal(1, rawItem["data"]?["value"]?.GetValue<int>());
    }

    /// <summary>Hosted MCP factory resolution uses caller auth context and preserves approval policy details on the resolved tool.</summary>
    /// <intent>Protect hosted MCP resolution when connector-only tools are expanded through the factory path.</intent>
    /// <scenario>LIB-OAI-MAP-002H</scenario>
    /// <behavior>Hosted MCP factory resolution receives the mapped auth context, supports connector-only tools, and preserves approval policy and patch metadata from the resolved definition.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_UsesHostedMcpFactoryAuthContextAndConnectorFallbacks()
    {
        McpAuthContext? capturedContext = null;
        HostedMcpToolFactory factory = new(new DelegateMcpAuthResolver(context =>
        {
            capturedContext = context;
            return new McpAuthResult("factory-token", null);
        }));

        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle hosted MCP tools.",
            HostedMcpTools =
            [
                new HostedMcpToolDefinition("mail", null, "connector-1", null, true, null, "approval required", "Hosted mail connector"),
                new HostedMcpToolDefinition("calendar", null, "connector-2", null, false, null, null, "Hosted calendar connector"),
            ],
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper(
            factory,
            _ => new McpAuthContext { UserId = "user-1", TenantId = "tenant-1", SessionKey = "session-hosted" }).CreateAsync(new AgentTurnRequest<TestContext>(
                agent,
                new TestContext("user-1", "tenant-1"),
                "session-hosted",
                1,
                [
                    new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
                ],
                "hello",
                null,
                null));

        Assert.NotNull(capturedContext);
        Assert.Equal("user-1", capturedContext!.UserId);
        Assert.Equal("tenant-1", capturedContext.TenantId);
        Assert.Equal("session-hosted", capturedContext.SessionKey);

        McpTool approvalTool = Assert.Single(plan.Options.Tools.OfType<McpTool>(), tool => tool.ConnectorId == "connector-1");
        McpTool connectorTool = Assert.Single(plan.Options.Tools.OfType<McpTool>(), tool => tool.ConnectorId == "connector-2");
        JsonObject approvalToolJson = OpenAiSdkSerialization.ToJsonObject(approvalTool);
        JsonObject connectorToolJson = OpenAiSdkSerialization.ToJsonObject(connectorTool);

        Assert.Equal("always", approvalToolJson["require_approval"]?.GetValue<string>());
        Assert.Equal("approval required", approvalToolJson["approval_reason"]?.GetValue<string>());
        Assert.Equal("Bearer factory-token", approvalTool.AuthorizationToken);

        Assert.Equal("never", connectorToolJson["require_approval"]?.GetValue<string>());
        Assert.Equal("connector-2", connectorTool.ConnectorId);
        Assert.Null(connectorToolJson["approval_reason"]);
    }

    /// <summary>Explicit handoff schemas and fallback output-contract details survive request mapping.</summary>
    /// <intent>Protect the fallback branches that synthesize transfer descriptions and structured-output names while preserving caller-supplied handoff schemas.</intent>
    /// <scenario>LIB-OAI-MAP-002I</scenario>
    /// <behavior>Handoffs without descriptions fall back to a transfer message, explicit handoff schemas are preserved, and unnamed structured outputs use the default OpenAI schema name in strict mode.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_UsesTransferFallbackDescriptionAndStructuredOutputDefaults()
    {
        JsonObject handoffSchema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["ticket_id"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray("ticket_id"),
            ["additionalProperties"] = false,
        };

        Agent<TestContext> mailAgent = new()
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
        };

        Agent<TestContext> triage = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Route work",
            Handoffs =
            [
                new AgentHandoff<TestContext>
                {
                    Name = "mail",
                    TargetAgent = mailAgent,
                    InputSchema = handoffSchema,
                },
            ],
            OutputContract = new AgentOutputContract(ExampleOutputSchema, null, null),
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-structured-output",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "help" },
            ],
            "help",
            null,
            null));

        FunctionTool handoffTool = Assert.Single(plan.Options.Tools.OfType<FunctionTool>());
        JsonObject handoffJson = OpenAiSdkSerialization.ToJsonObject(handoffTool);
        JsonObject textFormatJson = OpenAiSdkSerialization.ToJsonObject(plan.Options.TextOptions!.TextFormat);
        string handoffToolText = handoffJson.ToJsonString();

        Assert.Contains("\"ticket_id\"", handoffToolText);
        Assert.Contains("\"additionalProperties\":false", handoffToolText);
        Assert.Contains("Transfer to mail specialist.", handoffToolText);
        Assert.Equal(ResponseTextFormatKind.JsonSchema, plan.Options.TextOptions.TextFormat.Kind);
        Assert.Equal("structured_output", textFormatJson["name"]?.GetValue<string>());
        Assert.Equal(ExampleOutputSchema.ToJsonString(), textFormatJson["schema"]?.ToJsonString());
        Assert.True(textFormatJson["strict"]?.GetValue<bool>() ?? false);
    }

    /// <summary>Conversation item mapping preserves explicit tool-call fields, empty message text fallbacks, and reasoning normalization details.</summary>
    /// <intent>Protect item-level fallback branches so generated defaults only apply when callers omit values.</intent>
    /// <scenario>LIB-OAI-MAP-002J</scenario>
    /// <behavior>Message items map null text to empty strings, tool calls and outputs preserve explicit identifiers and payloads, and reasoning items keep non-blank ids while ignoring whitespace-only summary entries.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_PreservesExplicitConversationItemFieldsAndWhitespaceReasoningRules()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle requests.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-explicit-items",
            1,
            [
                new AgentConversationItem(AgentItemTypes.MessageOutput, "assistant", "triage"),
                new AgentConversationItem(AgentItemTypes.FinalOutput, "system", "triage"),
                new AgentConversationItem(AgentItemTypes.ToolCall, "assistant", "triage")
                {
                    ToolCallId = "call-123",
                    Name = "lookup_customer",
                    Data = new JsonObject { ["customer_id"] = "42" },
                    Status = "completed",
                },
                new AgentConversationItem(AgentItemTypes.ToolOutput, "tool", "triage")
                {
                    ToolCallId = "call-123",
                    Text = "customer-42",
                    Data = new JsonObject { ["result"] = "ignored" },
                    Status = "completed",
                },
                new AgentConversationItem(AgentItemTypes.Reasoning, "assistant", "triage"),
                new AgentConversationItem(AgentItemTypes.Reasoning, "assistant", "triage")
                {
                    Data = new JsonObject
                    {
                        ["id"] = "rs_123",
                        ["status"] = "completed",
                        ["summary"] = new JsonArray(
                            "first line",
                            "   ",
                            new JsonObject { ["text"] = "second line" },
                            new JsonObject { ["text"] = "   " }),
                    },
                },
            ],
            null,
            null,
            null));

        JsonObject assistantMessage = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[0]);
        JsonObject systemMessage = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[1]);
        JsonObject toolCall = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[2]);
        JsonObject toolOutput = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[3]);
        ReasoningResponseItem defaultReasoning = Assert.IsType<ReasoningResponseItem>(plan.Options.InputItems[4]);
        ReasoningResponseItem explicitReasoning = Assert.IsType<ReasoningResponseItem>(plan.Options.InputItems[5]);

        Assert.Equal(string.Empty, assistantMessage["content"]?[0]?["text"]?.GetValue<string>());
        Assert.Equal(string.Empty, systemMessage["content"]?[0]?["text"]?.GetValue<string>());

        Assert.Equal("call-123", toolCall["call_id"]?.GetValue<string>());
        Assert.Equal("lookup_customer", toolCall["name"]?.GetValue<string>());
        Assert.Equal("""{"customer_id":"42"}""", toolCall["arguments"]?.GetValue<string>());
        Assert.Equal("completed", toolCall["status"]?.GetValue<string>());

        Assert.Equal("call-123", toolOutput["call_id"]?.GetValue<string>());
        Assert.Equal("customer-42", toolOutput["output"]?.GetValue<string>());
        Assert.Equal("completed", toolOutput["status"]?.GetValue<string>());

        Assert.Null(defaultReasoning.Id);
        Assert.Equal(string.Empty, ReadReasoningSummary(defaultReasoning));
        Assert.Equal("rs_123", explicitReasoning.Id);
        Assert.Equal(ReasoningStatus.Completed, explicitReasoning.Status);
        Assert.Equal(string.Join(Environment.NewLine, ["first line", "second line"]), ReadReasoningSummary(explicitReasoning));
        Assert.Equal("reasoning", OpenAiSdkSerialization.ToJsonObject(defaultReasoning)["type"]?.GetValue<string>());
    }

    /// <summary>Missing tool-call values generate compact OpenAI identifiers and empty tool-output text.</summary>
    /// <intent>Protect the fallback branches that synthesize OpenAI-compatible call ids and blank tool output when no payload is present.</intent>
    /// <scenario>LIB-OAI-MAP-002M</scenario>
    /// <behavior>Missing tool-call ids use 32-character lowercase hex values, and tool-output items with no text or data map to an empty string.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_GeneratesCompactIdsAndEmptyToolOutputsForMissingValues()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle requests.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-generated-ids",
            1,
            [
                new AgentConversationItem(AgentItemTypes.ToolCall, "assistant", "triage"),
                new AgentConversationItem(AgentItemTypes.ToolOutput, "tool", "triage"),
            ],
            null,
            null,
            null));

        JsonObject generatedToolCall = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[0]);
        JsonObject generatedToolOutput = OpenAiSdkSerialization.ToJsonObject(plan.Options.InputItems[1]);

        Assert.Matches("^[0-9a-f]{32}$", generatedToolCall["call_id"]?.GetValue<string>() ?? string.Empty);
        Assert.Equal(string.Empty, generatedToolCall["name"]?.GetValue<string>());
        Assert.Equal("{}", generatedToolCall["arguments"]?.GetValue<string>());
        Assert.Matches("^[0-9a-f]{32}$", generatedToolOutput["call_id"]?.GetValue<string>() ?? string.Empty);
        Assert.Equal(string.Empty, generatedToolOutput["output"]?.GetValue<string>());
    }

    /// <summary>Handoff normalization stays disabled unless the current agent actually received a handoff occurrence.</summary>
    /// <intent>Protect the negative normalization path so unrelated handoff events do not rewrite the model-visible conversation.</intent>
    /// <scenario>LIB-OAI-MAP-002K</scenario>
    /// <behavior>When the current agent does not appear on a handoff-occurred item, normalization does not run and the original conversation remains intact.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task RequestMapper_DoesNotNormalizeWithoutMatchingTargetHandoff()
    {
        bool transformerInvoked = false;

        Agent<TestContext> delegateAgent = new()
        {
            Name = "delegate",
            Model = "gpt-5.4",
            Instructions = "Handle delegated work.",
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            delegateAgent,
            new TestContext("user-1", "tenant-1"),
            "session-no-handoff",
            2,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "delegate") { Text = "help" },
                new AgentConversationItem(AgentItemTypes.ToolCall, "assistant", "delegate")
                {
                    ToolCallId = "call-keep",
                    Name = "lookup_customer",
                    Data = new JsonObject { ["customer_id"] = "42" },
                },
                new AgentConversationItem(AgentItemTypes.HandoffOccurred, "system", "other-agent")
                {
                    Name = "mail",
                    Text = "delegate",
                    Data = new JsonObject { ["topic"] = "mail" },
                },
            ],
            null,
            "resp-1",
            new AgentRunOptions<TestContext>
            {
                HandoffHistoryMode = AgentHandoffHistoryMode.NormalizeModelInputAfterHandoff,
                HandoffHistoryTransformerAsync = (_, _) =>
                {
                    transformerInvoked = true;
                    return ValueTask.FromResult<IReadOnlyList<AgentConversationItem>>([]);
                },
            }));

        Assert.False(transformerInvoked);
        FunctionCallResponseItem preservedToolCall = Assert.IsType<FunctionCallResponseItem>(Assert.Single(plan.Options.InputItems, item => item is FunctionCallResponseItem));
        Assert.Equal("call-keep", OpenAiSdkSerialization.ToJsonObject(preservedToolCall)["call_id"]?.GetValue<string>());
    }

    /// <summary>Hosted MCP mapping prefers server URLs when they are present and still keeps connector metadata.</summary>
    /// <intent>Protect the hosted-MCP branch that selects URL-backed tools instead of connector-only fallbacks.</intent>
    /// <scenario>LIB-OAI-MAP-002L</scenario>
    /// <behavior>When a hosted MCP definition supplies a server URL, the mapped tool keeps the URL path while also preserving connector metadata and description.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_PrefersHostedServerUrlsWhenAvailable()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle hosted MCP tools.",
            HostedMcpTools =
            [
                new HostedMcpToolDefinition("mail", new Uri("https://mail.example.test/mcp"), "connector-1", "Bearer token", false, null, null, "Hosted mail connector"),
            ],
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-server-url",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
            ],
            "hello",
            null,
            null));

        McpTool hostedTool = Assert.Single(plan.Options.Tools.OfType<McpTool>());

        Assert.Equal(new Uri("https://mail.example.test/mcp"), hostedTool.ServerUri);
        Assert.Equal("connector-1", hostedTool.ConnectorId);
        Assert.Equal("Bearer token", hostedTool.AuthorizationToken);
        Assert.Equal("Hosted mail connector", hostedTool.ServerDescription);
    }

    /// <summary>Very large finite doubles survive model-setting patch serialization.</summary>
    /// <intent>Protect the request-mapper numeric branch that uses the JsonPatch double overload for values outside decimal precision.</intent>
    /// <scenario>LIB-OAI-MAP-002N</scenario>
    /// <behavior>Finite doubles that cannot round-trip through decimal still reach the wire body as doubles instead of being dropped.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_SerializesLargeFiniteDoubleModelSettingsOntoTheWire()
    {
        Agent<TestContext> triage = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            ModelSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["custom_large_double"] = 1e100d,
            },
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-large-double",
            6,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            "Need mail help",
            null,
            null));

        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-key");

        OpenAiResponsesClient client = new(httpClient, "v1/responses");
        await client.CreateResponseAsync(new OpenAiResponsesRequest(plan.Options), CancellationToken.None);

        JsonObject body = JsonNode.Parse(handler.Body)?.AsObject()
            ?? throw new InvalidOperationException("The captured request body was not valid JSON.");

        Assert.Equal(1e100d, body["custom_large_double"]?.GetValue<double>());
    }

    /// <summary>Connectorless hosted MCP definitions still map through the empty-connector fallback path.</summary>
    /// <intent>Protect the hosted-MCP constructor fallback used when a definition only supplies a server label.</intent>
    /// <scenario>LIB-OAI-MAP-002O</scenario>
    /// <behavior>Hosted MCP definitions without a server URL or connector id produce a tool with an empty relative server URI and no connector id.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Negative)]
    public async Task RequestMapper_UsesEmptyHostedMcpFallbackWhenNoServerConfigurationIsProvided()
    {
        Agent<TestContext> agent = new()
        {
            Name = "triage",
            Model = "gpt-5.4",
            Instructions = "Handle hosted MCP tools.",
            HostedMcpTools =
            [
                new HostedMcpToolDefinition("mail"),
            ],
        };

        OpenAiResponsesTurnPlan<TestContext> plan = await new OpenAiResponsesRequestMapper().CreateAsync(new AgentTurnRequest<TestContext>(
            agent,
            new TestContext("user-1", "tenant-1"),
            "session-empty-hosted",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" },
            ],
            "hello",
            null,
            null));

        McpTool hostedTool = Assert.Single(plan.Options.Tools.OfType<McpTool>());

        Assert.True(hostedTool.ConnectorId.HasValue);
        Assert.Equal(string.Empty, hostedTool.ConnectorId.ToString());
        Assert.Null(hostedTool.ServerUri);
    }

    /// <summary>Unrecognized JsonValue primitives are written as raw JSON patch payloads.</summary>
    /// <intent>Protect the private raw-json fallback so uncommon JsonValue primitive types are not silently dropped.</intent>
    /// <scenario>LIB-OAI-MAP-002P</scenario>
    /// <behavior>When ApplyJsonPatchValue receives a JsonValue primitive that does not map to string, bool, or known numeric overloads, it writes the node's raw JSON payload into the patch.</behavior>
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public void RequestMapper_ApplyJsonPatchValue_WritesRawJsonForUnrecognizedJsonValuePrimitives()
    {
        JsonPatch patch = new(Array.Empty<byte>());
        MethodInfo method = typeof(OpenAiResponsesRequestMapper).GetMethod("ApplyJsonPatchValue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("OpenAiResponsesRequestMapper.ApplyJsonPatchValue was not found.");

        object?[] parameters =
        [
            patch,
            "$.custom_uri",
            JsonValue.Create(new Uri("https://example.test/a")),
        ];

        method.Invoke(null, parameters);

        JsonPatch updatedPatch = Assert.IsType<JsonPatch>(parameters[0]);
        string patchText = updatedPatch.ToString();
        Assert.Contains("/custom_uri", patchText, StringComparison.Ordinal);
        Assert.Contains("https://example.test/a", patchText, StringComparison.Ordinal);
    }

    private static string? ReadReasoningSummary(ReasoningResponseItem reasoning)
    {
        JsonObject json = OpenAiSdkSerialization.ToJsonObject(reasoning);

        if (json["summary"] is JsonValue summaryValue && summaryValue.TryGetValue<string>(out string? text))
        {
            return text;
        }

        if (json["summary"] is JsonArray summaryArray)
        {
            List<string> parts = [];
            foreach (JsonNode? entry in summaryArray)
            {
                switch (entry)
                {
                    case JsonValue value when value.TryGetValue<string>(out string? textValue) && !string.IsNullOrWhiteSpace(textValue):
                        parts.Add(textValue);
                        break;
                    case JsonObject obj when obj["text"] is JsonValue textNode && textNode.TryGetValue<string>(out string? itemText) && !string.IsNullOrWhiteSpace(itemText):
                        parts.Add(itemText);
                        break;
                }
            }

            return string.Join(Environment.NewLine, parts);
        }

        return reasoning.GetType().GetProperty("SummaryText")?.GetValue(reasoning)?.ToString()
            ?? reasoning.GetType().GetProperty("Summary")?.GetValue(reasoning)?.ToString();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"resp-1","output":[]}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class DelegateMcpAuthResolver : IUserScopedMcpAuthResolver
    {
        private readonly Func<McpAuthContext, McpAuthResult> handler;

        public DelegateMcpAuthResolver(Func<McpAuthContext, McpAuthResult> handler)
        {
            this.handler = handler;
        }

        public ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(handler(context));
    }

    private sealed record TestContext(string UserId, string TenantId);
}
