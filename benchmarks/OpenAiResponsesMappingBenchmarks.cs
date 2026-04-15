using BenchmarkDotNet.Attributes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents.Benchmarks;

/// <summary>Benchmarks the representative OpenAI Responses request and response mapping paths.</summary>
[MemoryDiagnoser]
public class OpenAiResponsesMappingBenchmarks
{
    private readonly OpenAiResponsesRequestMapper requestMapper = new();
    private readonly OpenAiResponsesResponseMapper responseMapper = new();
    private AgentTurnRequest<TestContext> request = default!;
    private OpenAiResponsesTurnPlan<TestContext> plan = default!;
    private OpenAiResponsesResponse response = default!;

    [GlobalSetup]
    public void Setup()
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
            OutputContract = new AgentOutputContract(ExampleOutputSchema, null, typeof(BenchOutput)),
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

        request = new AgentTurnRequest<TestContext>(
            triage,
            new TestContext("user-1", "tenant-1"),
            "session-1",
            1,
            [
                new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "Need mail help" },
            ],
            "Need mail help",
            "resp-previous",
            null);

        plan = requestMapper.CreateAsync(request).AsTask().GetAwaiter().GetResult();
        response = new OpenAiResponsesResponse("resp-bench", new JsonObject
        {
            ["id"] = "resp-bench",
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
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = "fc_1",
                    ["call_id"] = "call_1",
                    ["name"] = "lookup_customer",
                    ["arguments"] = """{"customer_id":"42"}""",
                },
            },
        });
    }

    [Benchmark]
    public async Task CreateTurnPlanAsync()
        => await requestMapper.CreateAsync(request).AsTask().ConfigureAwait(false);

    [Benchmark]
    public void MapTurnResponse()
        => _ = responseMapper.Map(response, plan);

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

    private sealed record BenchOutput(string Value);

    private sealed record TestContext(string UserId, string TenantId);
}
