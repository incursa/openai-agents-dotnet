# Library Conformance Matrix

## Scope
Traceability matrix for cross-library interface quality scenarios in:

- `specs/libraries/library-interface-quality-spec.md`
- `specs/libraries/agent-composition-spec.md`
- `specs/libraries/agent-execution-spec.md`
- `specs/libraries/extensions-integration-spec.md`
- `specs/libraries/mcp-spec.md`
- `specs/libraries/openai-responses-spec.md`

Status values:

- `Covered`: scenario is mapped to one or more automated tests or automation artifacts.
- `Missing`: no mapped test exists yet.
- `Deferred`: intentionally deferred and tracked for later ratcheting.

| Scenario ID | Library | Area | Status | Mapped Test(s) / Artifact(s) |
| --- | --- | --- | --- | --- |
| LIB-GOV-SPEC-001 | All | Governance | Covered | `specs/libraries/library-interface-quality-spec.md`, `specs/libraries/agent-composition-spec.md`, `specs/libraries/agent-execution-spec.md`, `specs/libraries/extensions-integration-spec.md`, `specs/libraries/mcp-spec.md`, `specs/libraries/openai-responses-spec.md` |
| LIB-GOV-MATRIX-001 | All | Governance | Covered | `scripts/quality/validate-library-traceability.ps1`, `specs/libraries/library-conformance-matrix.md` |
| LIB-GOV-API-001 | All | Governance | Covered | `src/Incursa.OpenAI.Agents/PublicAPI.Shipped.txt`, `src/Incursa.OpenAI.Agents/PublicAPI.Unshipped.txt`, `src/Incursa.OpenAI.Agents.Extensions/PublicAPI.Shipped.txt`, `src/Incursa.OpenAI.Agents.Extensions/PublicAPI.Unshipped.txt` |
| LIB-GOV-API-002 | All | Governance | Covered | `scripts/quality/validate-library-traceability.ps1`, `tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-GOV-TESTDOCS-001 | All | Governance | Covered | `.github/workflows/testdocs.yml`, `docs/testing/README.md`, `tests/Incursa.OpenAI.Agents.Tests/Incursa.OpenAI.Agents.Tests.csproj` |
| LIB-GOV-COV-001 | All | Governance | Covered | `scripts/quality/run-library-coverage.ps1` |
| LIB-GOV-MUT-001 | All | Governance | Covered | `scripts/quality/run-library-mutation.ps1`, `scripts/quality/stryker/agents.stryker-config.json`, `scripts/quality/stryker/extensions.stryker-config.json`, `scripts/quality/stryker/openai.stryker-config.json` |
| LIB-GOV-WORKBENCH-001 | All | Governance | Covered | `scripts/quality/run-quality-evidence.ps1`, `docs/30-contracts/test-gate.contract.yaml`, `.github/workflows/workbench-quality.yml` |
| LIB-COMP-API-001 | Agents | PublicApi | Covered | `src/Incursa.OpenAI.Agents/PublicAPI.Shipped.txt`, `tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs` |
| LIB-COMP-BUILDER-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs` |
| LIB-COMP-HANDOFF-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-COMP-TOOLS-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-COMP-REQUEST-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs` |
| LIB-EXEC-API-001 | Agents | PublicApi | Covered | `src/Incursa.OpenAI.Agents/PublicAPI.Shipped.txt`, `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/ApprovalAndGuardrailTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/SessionStoreTests.cs` |
| LIB-EXEC-RUNNER-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-EXEC-TOOLS-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-EXEC-HANDOFF-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-EXEC-MAXTURNS-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-EXEC-STREAM-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs` |
| LIB-EXEC-APPROVAL-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/ApprovalAndGuardrailTests.cs` |
| LIB-EXEC-APPROVAL-002 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/ApprovalAndGuardrailTests.cs` |
| LIB-EXEC-GUARDRAIL-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/ApprovalAndGuardrailTests.cs` |
| LIB-EXEC-OBS-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/RuntimeObservabilityTests.cs` |
| LIB-EXEC-SESSION-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/SessionStoreTests.cs` |
| LIB-EXEC-SESSION-002 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/SessionStoreTests.cs` |
| LIB-EXT-API-001 | Extensions | PublicApi | Covered | `src/Incursa.OpenAI.Agents.Extensions/PublicAPI.Shipped.txt`, `tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs`, `tests/Incursa.OpenAI.Agents.Tests/RuntimeObservabilityTests.cs` |
| LIB-EXT-DI-001 | Extensions | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs` |
| LIB-EXT-OBS-001 | Extensions | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/RuntimeObservabilityTests.cs` |
| LIB-MCP-API-001 | Agents | PublicApi | Covered | `src/Incursa.OpenAI.Agents/PublicAPI.Shipped.txt`, `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-AUTH-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-META-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-FILTER-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-CACHE-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-ERROR-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-RETRY-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-MCP-AUTHFAIL-001 | Agents | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/McpTests.cs` |
| LIB-OAI-API-001 | OpenAI | PublicApi | Covered | `src/Incursa.OpenAI.Agents/PublicAPI.Shipped.txt`, `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-OAI-MAP-001 | OpenAI | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-OAI-MCP-001 | OpenAI | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-OAI-HANDOFF-001 | OpenAI | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-OAI-FILTER-001 | OpenAI | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-OAI-REASONING-001 | OpenAI | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
| LIB-OAI-STREAM-001 | OpenAI | Behavior | Covered | `tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs` |
