# Incursa.OpenAI.Agents

[![CI](https://github.com/incursa/openai-agents-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/incursa/openai-agents-dotnet/actions/workflows/ci.yml)
[![Quality](https://github.com/incursa/openai-agents-dotnet/actions/workflows/library-fast-quality.yml/badge.svg)](https://github.com/incursa/openai-agents-dotnet/actions/workflows/library-fast-quality.yml)
[![Docs](https://github.com/incursa/openai-agents-dotnet/actions/workflows/testdocs.yml/badge.svg)](https://github.com/incursa/openai-agents-dotnet/actions/workflows/testdocs.yml)
[![Nightly issue sync](https://github.com/incursa/openai-agents-dotnet/actions/workflows/workbench-nightly-sync.yml/badge.svg)](https://github.com/incursa/openai-agents-dotnet/actions/workflows/workbench-nightly-sync.yml)
[![NuGet Core](https://img.shields.io/nuget/v/Incursa.OpenAI.Agents.svg)](https://www.nuget.org/packages/Incursa.OpenAI.Agents/)
[![NuGet Extensions](https://img.shields.io/nuget/v/Incursa.OpenAI.Agents.Extensions.svg)](https://www.nuget.org/packages/Incursa.OpenAI.Agents.Extensions/)
[![License](https://img.shields.io/github/license/incursa/openai-agents-dotnet)](LICENSE)

`Incursa.OpenAI.Agents` is a .NET implementation of server-side orchestration patterns inspired by the OpenAI Agents SDK.

Upstream source of truth: [openai/openai-agents-python](https://github.com/openai/openai-agents-python).

## Scope and positioning

- Includes: server-side agent orchestration, handoffs, approvals, guardrails, sessions, and MCP-driven tool execution using OpenAI Responses.
- Includes: both direct runner usage and host-oriented Dependency Injection wiring.
- Includes: standalone OpenAI audio APIs for file transcription and text-to-speech.
- Excludes: realtime voice, browser/computer-use tools, tracing/eval/exporter suite, and multi-provider model abstractions.
- Includes explicit quality and operational controls in this repository via Workbench and tested quality workflows.

## 10-minute mini guide

1. Restore and build the repository.

```bash
dotnet restore
dotnet build
```

2. Configure credentials for local execution.

```bash
export OPENAI_API_KEY=sk-... # Linux/macOS
# OR
$Env:OPENAI_API_KEY = "sk-..." # PowerShell
```

3. Run the hosted sample (recommended for DI/observability patterns).

```bash
dotnet run --project samples/Incursa.OpenAI.Agents.HostSample/Incursa.OpenAI.Agents.HostSample.csproj
```

4. Validate parity and quality workflow gates (quick path).

```bash
dotnet test
dotnet tool restore
dotnet tool run workbench sync -- --issues --import-issues --docs --nav
```

## Repository at a glance

- Core runtime: [``src/Incursa.OpenAI.Agents``](src/Incursa.OpenAI.Agents)
- DI/hosting layer: [``src/Incursa.OpenAI.Agents.Extensions``](src/Incursa.OpenAI.Agents.Extensions)
- Console sample: [``samples/Incursa.OpenAI.Agents.ConsoleSample``](samples/Incursa.OpenAI.Agents.ConsoleSample)
- Hosted sample: [``samples/Incursa.OpenAI.Agents.HostSample``](samples/Incursa.OpenAI.Agents.HostSample)
- Tests: [``tests/Incursa.OpenAI.Agents.Tests``](tests/Incursa.OpenAI.Agents.Tests)
- Docs: [``docs/``](docs/)

## Requirements

- .NET SDK `10.0.200+` (managed by [``global.json``](global.json))
- OpenAI API key (for live calls)
- Network access to OpenAI-compatible endpoint (default: responses API)
- MCP target services reachable from host runtime when MCP tools are enabled

## Installation

If consuming published packages:

```bash
dotnet add package Incursa.OpenAI.Agents
dotnet add package Incursa.OpenAI.Agents.Extensions
dotnet add package Incursa.OpenAI.Agents.Storage.Azure
dotnet add package Incursa.OpenAI.Agents.Storage.S3
```

If working in this repository:

```bash
dotnet restore
dotnet build
```

Or pass the key directly in configuration:

```csharp
services.AddOpenAiResponses(options =>
{
    options.ApiKey = "sk-...";
});
```

## Configuration

### Environment variables

- `OPENAI_API_KEY` (production calls)

Linux/macOS:

```bash
export OPENAI_API_KEY=sk-...
```

Windows PowerShell:

```powershell
$Env:OPENAI_API_KEY = "sk-..."
```

## Development quickstart

### 1) Validate baseline

```bash
dotnet restore
dotnet build
dotnet test
```

## NuGet release flow

- Current version policy uses `1.0.12` as the first public release version.
- Shipped API surface is the gate for semver intent:
  - `PublicAPI.Shipped.txt` additive changes require at least a minor version bump.
  - `PublicAPI.Shipped.txt` removals/compatibility changes require a major version bump.
  - `PublicAPI.Unshipped.txt` changes are allowed to track work-in-progress API additions and are not treated as release-breaking by the policy gate.
- Publishing uses the GitHub workflow [``.github/workflows/publish-nuget-packages.yml``](.github/workflows/publish-nuget-packages.yml).
  - Tag with `vX.Y.Z` (for example `v1.0.12`) and push, or run workflow dispatch with `version` set to `X.Y.Z`.
  - The workflow validates policy using [``scripts/release/validate-public-api-versioning.ps1``](scripts/release/validate-public-api-versioning.ps1) before packing and pushing both packages to nuget.org.
- Standard CI uses [``.github/workflows/ci.yml``](.github/workflows/ci.yml) for push and pull request validation, including the blocking tests and full quality evidence lane, without publishing.

### 2) Run samples

Minimal direct pattern:

```bash
dotnet run --project samples/Incursa.OpenAI.Agents.ConsoleSample/Incursa.OpenAI.Agents.ConsoleSample.csproj
```

Hosted DI pattern (recommended for services):

```bash
dotnet run --project samples/Incursa.OpenAI.Agents.HostSample/Incursa.OpenAI.Agents.HostSample.csproj
```

## Example usage

```csharp
using System.Collections.Generic;
using System.Threading;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Extensions;
using Incursa.OpenAI.Agents.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddIncursaAgents();
services.AddFileAgentSessions("sessions");
services.AddOpenAiResponses(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    options.EnableMcpLoggingObserver = true;
});
services.AddSingleton<IUserScopedMcpAuthResolver, DemoAuthResolver>();

using var provider = services.BuildServiceProvider();
var runner = provider.GetRequiredService<OpenAiResponsesRunner>();

var specialist = AgentBuilder
    .Create<AppContext>("mail specialist")
    .WithModel("gpt-5.4")
    .WithInstructions("Handle mailbox tasks and use available MCP tools.")
    .WithStreamableMcpServer(new StreamableHttpMcpServerDefinition("mail", new Uri("https://mail.example.test/mcp")))
    .Build();

var triage = AgentBuilder
    .Create<AppContext>("triage")
    .WithModel("gpt-5.4")
    .WithInstructions("Route mailbox tasks to the specialist agent.")
    .AddHandoff("mail", specialist, "Delegate mailbox work.")
    .Build();

var request = AgentRunRequest<AppContext>.FromUserInput(
    triage,
    "Check my inbox for unread messages.",
    new AppContext(new AppUser("user-1", "tenant-1", "mailbox-1", "conn-1")),
    "session-123");

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

record AppUser(string Id, string TenantId, string MailboxId, string ConnectionId);
record AppContext(AppUser User);

sealed class DemoAuthResolver : IUserScopedMcpAuthResolver
{
    public ValueTask<McpAuthResult> ResolveAsync(McpAuthContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new McpAuthResult(
            $"demo-token-for-{context.UserId}",
            new Dictionary<string, string>
            {
                ["X-Tenant-Id"] = context.TenantId ?? string.Empty,
                ["X-Mailbox-Id"] = context.MailboxId ?? string.Empty,
                ["X-Connection-Id"] = context.ConnectionId ?? string.Empty,
            }));
}
```

## Quality and operations

- **Quality lanes**: `dotnet test`, plus scripted quality workflows in [``scripts/quality/``](scripts/quality/).
- **Workbench sync evidence**: [``scripts/quality/run-quality-evidence.ps1``](scripts/quality/run-quality-evidence.ps1) and [``.github/workflows/workbench-quality.yml``](.github/workflows/workbench-quality.yml) for advisory tests, mutation evidence, and fuzz corpus checks.
- **Benchmarks**: [``benchmarks/README.md``](benchmarks/README.md).
- **Fuzz harnesses**: [``fuzz/README.md``](fuzz/README.md), [``fuzz/corpus/README.md``](fuzz/corpus/README.md), [``scripts/quality/run-fuzz-corpus.ps1``](scripts/quality/run-fuzz-corpus.ps1).
- **Nightly issue sync**: [``.github/workflows/workbench-nightly-sync.yml``](.github/workflows/workbench-nightly-sync.yml) (dry-run validation + artifact output).
- **Scope parity tracking**: [``docs/parity/manifest.md``](docs/parity/manifest.md).
- **Maintenance checklist**: [``docs/parity/maintenance-checklist.md``](docs/parity/maintenance-checklist.md).
- **Upstream sync workflow**: [``tools/upstream-sync/README.md``](tools/upstream-sync/README.md).
- **Repository boundaries and quality contracts**: [``docs/quality/repo-scope-boundary.md``](docs/quality/repo-scope-boundary.md), [``docs/30-contracts/test-gate.contract.yaml``](docs/30-contracts/test-gate.contract.yaml).

### Direct verification commands

```bash
dotnet test
dotnet tool restore
dotnet tool run workbench sync -- --items --issues --import-issues --docs --nav
dotnet tool run workbench quality sync -- --contract docs/30-contracts/test-gate.contract.yaml --results artifacts/test-results/advisory --coverage artifacts/coverage/advisory --out-dir artifacts/quality/testing
dotnet tool run workbench quality show -- --kind report --path artifacts/quality/testing/quality-report.json
```

## Documentation

- [Product scope](docs/10-product/README.md)
- [Quickstart](docs/quickstart.md)
- [Extensions and hosting](docs/extensions.md)
- [Parity manifest](docs/parity/manifest.md)
- [Upstream maintenance checklist](docs/parity/maintenance-checklist.md)
- [Testing documentation](docs/testing/README.md)
- [Quality contracts](docs/30-contracts/README.md)

## Contributing

When changing behavior mirrored from upstream:

1. Review the equivalent Python SDK change.
2. Update the .NET implementation and relevant tests.
3. Update docs/examples if behavior or API changed.
4. Run the quality/sync workflows before opening PR.
5. When editing Markdown, link repo-local references with relative links and keep code-styled labels inside the link text; use absolute URLs only for package-facing or external docs.

## License

See [``LICENSE``](LICENSE) in the repository root.
