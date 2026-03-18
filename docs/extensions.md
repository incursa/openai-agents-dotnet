---
workbench:
  type: guide
  workItems: []
  codeRefs:
    - src/Incursa.OpenAI.Agents.Extensions/ServiceCollectionExtensions.cs
    - samples/Incursa.OpenAI.Agents.ConsoleSample/Program.cs
    - samples/Incursa.OpenAI.Agents.HostSample/Program.cs
    - tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs
  pathHistory: []
  path: /docs/extensions.md
---

# Extensions and hosting

The upstream Python SDK does not include a .NET DI package, so this area documents the
runtime integration surface implemented specifically for this repository.

## Concept

`Incursa.OpenAI.Agents.Extensions` standardizes startup for host-based applications:

- Runner and session registration in DI.
- Optional file-backed sessions.
- OpenAI Responses + MCP wiring in one hosted step.

## API surface

- `AddIncursaAgents(IServiceCollection)` registers the baseline runtime services.
- `AddIncursaAgents(IServiceCollection, Action<AgentRuntimeOptions>?)` accepts runtime-level configuration.
- `AddFileAgentSessions(IServiceCollection, string directoryPath, Action<AgentSessionRetentionOptions>?)` configures durable, file-backed state.
- `AddOpenAiResponses(IServiceCollection, Action<OpenAiResponsesOptions>?)` registers responses + runner dependencies.

## Option surface

- `AgentSessionRetentionOptions` controls retention and cleanup:
  - `MaxConversationItems`
  - `MaxTurns`
  - `CompactionMode`
  - `AbsoluteLifetime`
  - `SlidingExpiration`
  - cleanup behavior on load/save
- `OpenAiResponsesOptions` controls transport and retry behavior:
  - `ApiKey`
  - `BaseAddress`
  - `ResponsesPath`
  - `McpRetryCount`
  - `McpRetryDelay`
  - `EnableMcpLoggingObserver`

## Minimal host sample

```csharp
using System;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;
using Incursa.OpenAI.Agents.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

var services = new ServiceCollection();
services.AddIncursaAgents(options =>
{
    options.EnableLoggingObserver = true;
});
services.AddFileAgentSessions("sessions", options =>
{
    options.MaxConversationItems = 50;
    options.MaxTurns = 10;
    options.SlidingExpiration = TimeSpan.FromHours(1);
});
services.AddOpenAiResponses(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    options.McpRetryCount = 3;
    options.McpRetryDelay = TimeSpan.FromMilliseconds(250);
});
services.AddHttpClient("incursa-agents-mcp");
services.AddSingleton<IUserScopedMcpAuthResolver, DemoAuthResolver>();

using var provider = services.BuildServiceProvider();
var runner = provider.GetRequiredService<OpenAiResponsesRunner>();

class DemoAuthResolver : IUserScopedMcpAuthResolver
{
    public ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new McpAuthResult($"demo-token-for-{context.UserId}"));
}
```

## Recommended references

- `samples/Incursa.OpenAI.Agents.HostSample/Program.cs` for a complete hosted workflow.
- `samples/Incursa.OpenAI.Agents.ConsoleSample/Program.cs` for direct construction.
- `tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs` for service registration guarantees.

## Next steps

- Move from console samples to host startup once your application wiring is stable.
- Keep extension setup in composition root and avoid constructing runner dependencies manually in feature code.
- Revisit retention and retry settings as traffic and workload increase.
