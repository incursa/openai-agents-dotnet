#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;
using OpenAI.Responses;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Request mapping preserves the OpenAI Responses turn shape.</summary>
public sealed class REQ_LIB_OAI_MAP_001
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

    /// <summary>Request mapping keeps the model, continuation, tools, handoffs, hosted MCP tools, and structured output contract.</summary>
    /// <intent>Protect the core OpenAI Responses request-mapping contract used by turn execution.</intent>
    /// <scenario>LIB-OAI-MAP-001</scenario>
    /// <behavior>Mapped requests preserve model selection, previous-response continuation, tool payloads, handoff routing, hosted MCP metadata, and structured-output schema details.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    [CoverageType(RequirementCoverageType.Positive)]
    public async Task RequestMapper_MapsToolsHandoffsAndStructuredOutput()
    {
        Agent<TestContext> mailAgent = new()
        {
            Name = "mail specialist",
            Model = "gpt-5.4",
            Instructions = "Handle mail",
            HandoffDescription = "Handles mailbox work.",
        };

        Agent<TestContext> triage = new()
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
            OutputContract = new AgentOutputContract(ExampleOutputSchema, null, typeof(ExampleOutput)),
            ModelSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["text"] = new
                {
                    verbosity = "medium",
                },
            },
            HostedMcpTools =
            [
                new HostedMcpToolDefinition("mail", new Uri("https://mail.example.test/mcp"), "connector-1", null, true, null, null, "Hosted mail connector"),
            ],
        };

        OpenAiResponsesRequestMapper mapper = new();
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

        Assert.Equal("gpt-5.4", plan.Options.Model);
        Assert.Equal("resp-previous", plan.Options.PreviousResponseId);
        Assert.Equal(3, plan.Options.Tools.Count);
        Assert.NotNull(plan.Options.TextOptions?.TextFormat);
        Assert.Equal(ResponseTextFormatKind.JsonSchema, plan.Options.TextOptions!.TextFormat.Kind);
        Assert.Equal(ExampleOutputSchema.ToJsonString(), OpenAiSdkSerialization.ToJsonObject(plan.Options.TextOptions.TextFormat)["schema"]?.ToJsonString());
        Assert.Equal("ExampleOutput", OpenAiSdkSerialization.ToJsonObject(plan.Options.TextOptions.TextFormat)["name"]?.GetValue<string>());
        Assert.Single(plan.HandoffMap);
        Assert.Contains(plan.Options.Tools, item => item is FunctionTool function && function.FunctionName == "lookup_customer");
        Assert.Contains(plan.Options.Tools, item => item is FunctionTool function && function.FunctionName == "transfer_to_mail_specialist");
        Assert.Contains(plan.Options.Tools, item => item is McpTool);
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
}
