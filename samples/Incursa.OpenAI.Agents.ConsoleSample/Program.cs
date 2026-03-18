using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;

var sessionDirectory = Path.Combine(global::System.AppContext.BaseDirectory, "sessions");
var sessionStore = new FileAgentSessionStore(
    sessionDirectory,
    new AgentSessionStoreOptions
    {
        MaxConversationItems = 50,
        MaxTurns = 10,
    });

Agent<AppContext> mailAgent = AgentBuilder
    .Create<AppContext>("mail specialist")
    .WithModel("gpt-5.4")
    .WithInstructions("Handle mailbox tasks and use the available MCP tools.")
    .WithStreamableMcpServer(new StreamableHttpMcpServerDefinition("mail", new Uri("https://mail.example.test/mcp"))
    {
        ApprovalRequired = true,
        Description = "Governed per-user mailbox MCP server.",
        CacheToolsList = true,
    })
    .Build();

Agent<AppContext> triageAgent = AgentBuilder
    .Create<AppContext>("triage")
    .WithModel("gpt-5.4")
    .WithInstructions("Route mailbox tasks to the appropriate specialist.")
    .AddHandoff("mail", mailAgent, "Delegate mailbox work.")
    .Build();

var runner = new OpenAiResponsesRunner(
    new SampleResponsesClient(),
    sessionStore,
    new ApprovalRequiredForSensitiveToolsService(),
    new DemoUserScopedMcpAuthResolver(),
    new HttpClient(new DemoMcpHandler()),
    null,
    mcpClientOptions: new McpClientOptions
    {
        RetryCount = 1,
        Observer = new ConsoleMcpObserver(),
    },
    observer: null);

var context = new AppContext(new AppUser("user-1", "tenant-1", "mailbox-1", "conn-1"));
AgentRunRequest<AppContext> request = AgentRunRequest<AppContext>
    .FromUserInput(triageAgent, "Check my inbox for unread messages.", context)
    .WithSession("sample-session");

Console.WriteLine($"Session store: {sessionDirectory}");

AgentRunResult<AppContext> first = await runner.RunAsync(request, CreateAuthContext);
Console.WriteLine($"First status: {first.Status}");
Console.WriteLine($"Current agent: {first.FinalAgent.Name}");

if (first.Status == AgentRunStatus.ApprovalRequired && first.State is not null && first.ApprovalRequest is not null)
{
    Console.WriteLine($"Approval needed for tool: {first.ApprovalRequest.ToolName}");

    AgentRunResult<AppContext> resumed = await runner.RunAsync(
        AgentRunRequest<AppContext>.ResumeApproved(first.State, context, first.ApprovalRequest.ToolCallId),
        CreateAuthContext);

    Console.WriteLine($"Resumed status: {resumed.Status}");
    Console.WriteLine($"Final output: {resumed.FinalOutput?.Text}");
}

static McpAuthContext CreateAuthContext(AgentTurnRequest<AppContext> request)
{
    AppUser user = request.Context.User;
    return new McpAuthContext
    {
        UserId = user.Id,
        TenantId = user.TenantId,
        MailboxId = user.MailboxId,
        ConnectionId = user.ConnectionId,
        SessionKey = request.SessionKey,
        AgentName = request.Agent.Name,
    };
}

internal sealed record AppUser
{
    public AppUser(string id, string tenantId, string mailboxId, string connectionId)
    {
        Id = id;
        TenantId = tenantId;
        MailboxId = mailboxId;
        ConnectionId = connectionId;
    }

    public string Id { get; init; }

    public string TenantId { get; init; }

    public string MailboxId { get; init; }

    public string ConnectionId { get; init; }
}

internal sealed record AppContext
{
    public AppContext(AppUser user)
    {
        User = user;
    }

    public AppUser User { get; init; }
}

internal sealed class DemoUserScopedMcpAuthResolver : IUserScopedMcpAuthResolver
{
    public ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(new McpAuthResult(
            $"demo-token-for-{context.UserId}",
            new Dictionary<string, string>
            {
                ["X-Tenant-Id"] = context.TenantId ?? string.Empty,
                ["X-Mailbox-Id"] = context.MailboxId ?? string.Empty,
                ["X-Connection-Id"] = context.ConnectionId ?? string.Empty,
            }));
}

internal sealed class ApprovalRequiredForSensitiveToolsService : IAgentApprovalService
{
    public ValueTask<ApprovalDecision> EvaluateAsync<TContext>(AgentApprovalContext<TContext> context, CancellationToken cancellationToken)
        => ValueTask.FromResult(context.Tool.RequiresApproval
            ? ApprovalDecision.Require("Mailbox access requires approval.")
            : ApprovalDecision.Allow());
}

internal sealed class ConsoleMcpObserver : IMcpClientObserver
{
    public ValueTask ObserveAsync(McpClientObservation observation, CancellationToken cancellationToken)
    {
        Console.WriteLine($"MCP {observation.Method} attempt {observation.Attempt}: {observation.Outcome} ({observation.ServerLabel})");
        return ValueTask.CompletedTask;
    }
}

internal sealed class DemoMcpHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        JsonObject? json = JsonNode.Parse(body)?.AsObject();
        var method = json?["method"]?.GetValue<string>();

        var payload = method switch
        {
            "tools/list" => """{"jsonrpc":"2.0","id":"1","result":{"tools":[{"name":"list_messages","description":"List unread messages","inputSchema":{"type":"object","properties":{"folder":{"type":"string"}}}}]}}""",
            "tools/call" => """{"jsonrpc":"2.0","id":"1","result":{"content":[{"text":"2 unread messages in inbox"}]}}""",
            _ => """{"jsonrpc":"2.0","id":"1","error":{"code":-32601,"message":"method not found"}}""",
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed class SampleResponsesClient : IOpenAiResponsesClient
{
    private int turn;

    public Task<OpenAiResponsesResponse> CreateResponseAsync(OpenAiResponsesRequest request, CancellationToken cancellationToken)
    {
        turn++;
        return Task.FromResult(turn switch
        {
            1 => new OpenAiResponsesResponse("resp-1", new JsonObject
            {
                ["id"] = "resp-1",
                ["output"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "function_call",
                        ["id"] = "handoff_1",
                        ["call_id"] = "handoff_1",
                        ["name"] = "transfer_to_mail_specialist",
                        ["arguments"] = "{}",
                    },
                },
            }),
            2 => new OpenAiResponsesResponse("resp-2", new JsonObject
            {
                ["id"] = "resp-2",
                ["output"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "function_call",
                        ["id"] = "tool_1",
                        ["call_id"] = "tool_1",
                        ["name"] = "mcp_mail__list_messages",
                        ["arguments"] = """{"folder":"inbox"}""",
                    },
                },
            }),
            _ => new OpenAiResponsesResponse("resp-3", new JsonObject
            {
                ["id"] = "resp-3",
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
                                ["text"] = "You have 2 unread messages in your inbox.",
                            },
                        },
                    },
                },
            }),
        });
    }

    public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(OpenAiResponsesRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
