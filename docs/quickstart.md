---
workbench:
  type: guide
  workItems: []
  codeRefs:
    - samples/Incursa.OpenAI.Agents.ConsoleSample/Program.cs
    - samples/Incursa.OpenAI.Agents.HostSample/Program.cs
  pathHistory: []
  path: /docs/quickstart.md
---

# Quickstart

`Incursa.OpenAI.Agents` is a .NET runtime-first SDK for building agent-driven workflows.
This guide mirrors the upstream SDK shape while staying pragmatic to .NET hosting patterns.

## Concept

The .NET flavor keeps core orchestration concepts intact:

- Agents carry instructions and tool access.
- Handoffs enable delegation between agents.
- Approvals gate sensitive tool execution.
- Session state can persist across turns.
- MCP exposes external tools through streamable HTTP.

## Before you begin

- .NET SDK from `global.json`
- OpenAI API key
- A terminal with this repo checked out

You can either pass the key directly through `AddOpenAiResponses(options => options.ApiKey = "...")`
or set it for your shell:

```bash
export OPENAI_API_KEY=sk-...
```

## Quickstart flow

### Build

```bash
dotnet restore
dotnet build
```

### Run the console sample

```bash
dotnet run --project samples/Incursa.OpenAI.Agents.ConsoleSample/Incursa.OpenAI.Agents.ConsoleSample.csproj
```

This sample demonstrates:

- agent and handoff setup
- approvals
- streamable MCP invocation
- file-backed session persistence
- Azure Blob or S3 session persistence are available through separate provider packages for production deployments

### Run the host sample

```bash
dotnet run --project samples/Incursa.OpenAI.Agents.HostSample/Incursa.OpenAI.Agents.HostSample.csproj
```

This sample demonstrates:

- dependency injection setup
- hosted session store
- request resume after reload
- MCP retry and observation hooks

## Minimal direct API example

```csharp
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;

var request = AgentRunRequest<AppContext>.FromUserInput(
    AgentBuilder
        .Create<AppContext>("triage")
        .WithModel("gpt-5.4")
        .WithInstructions("Route mailbox tasks to the specialist.")
        .Build(),
    "Check my inbox for unread messages.",
    new AppContext(new AppUser("user-1", "tenant-1", "mailbox-1", "conn-1")),
    sessionKey: "demo-session");

var runner = new OpenAiResponsesRunner(new SampleResponsesClient());
var result = await runner.RunAsync(request, turn =>
{
    var user = turn.Context.User;
    return new McpAuthContext(
        UserId: user.Id,
        TenantId: user.TenantId,
        MailboxId: user.MailboxId,
        ConnectionId: user.ConnectionId,
        SessionKey: turn.SessionKey,
        AgentName: turn.Agent.Name);
});

Console.WriteLine(result.FinalOutput?.Text);

sealed record AppUser(string Id, string TenantId, string MailboxId, string ConnectionId);
sealed record AppContext(AppUser User);

sealed class SampleResponsesClient : IOpenAiResponsesClient
{
    public Task<OpenAiResponsesResponse> CreateResponseAsync(
        OpenAiResponsesRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new OpenAiResponsesResponse("resp-1", new JsonObject()));

    public async IAsyncEnumerable<OpenAiResponsesStreamEvent> StreamResponseAsync(
        OpenAiResponsesRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
```

## Next steps

- Continue with the full DI flow in [Extensions and hosting](extensions.md).
- Review included surface boundaries in [docs/parity/manifest.md](parity/manifest.md).
- Validate changes through existing quality workflows and generated test docs.
