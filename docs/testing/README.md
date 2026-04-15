---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/testing/README.md
---

# Test Documentation

This folder describes the XML doc schema for the xUnit test suite and hosts generated test inventory artifacts.

Workbench quality integration uses:

- Authored intent contract: [``docs/30-contracts/test-gate.contract.yaml``](../30-contracts/test-gate.contract.yaml)
- Smoke lane: [``scripts/quality/run-smoke-tests.ps1``](../../scripts/quality/run-smoke-tests.ps1)
- Blocking lane: [``scripts/quality/run-blocking-tests.ps1``](../../scripts/quality/run-blocking-tests.ps1)
- Observational lane: [``scripts/quality/run-observational-tests.ps1``](../../scripts/quality/run-observational-tests.ps1)
- Advisory evidence runner: [``scripts/quality/run-advisory-quality-tests.ps1``](../../scripts/quality/run-advisory-quality-tests.ps1)
- Mutation lane: [``scripts/quality/run-library-mutation.ps1``](../../scripts/quality/run-library-mutation.ps1)
- Full quality workflow: [``scripts/quality/run-quality-evidence.ps1``](../../scripts/quality/run-quality-evidence.ps1) - runs advisory tests, mutation evidence, fuzz corpus checks, and Workbench normalization in one pass.
- Normalized Workbench outputs: `artifacts/quality/testing/`
- Benchmarks: [``benchmarks/README.md``](../../benchmarks/README.md)
- Fuzz harnesses: [``fuzz/README.md``](../../fuzz/README.md), [``fuzz/corpus/README.md``](../../fuzz/corpus/README.md)
- Operating model: [``docs/testing-operating-model.md``](../testing-operating-model.md)
- Known issues: [``docs/testing-known-issues.md``](../testing-known-issues.md)
- Requirement-home tests: [``tests/Incursa.OpenAI.Agents.Tests/RequirementHomes/``](../../tests/Incursa.OpenAI.Agents.Tests/RequirementHomes/)

- Schema: [``docs/testing/test-doc-schema.md``](test-doc-schema.md)
- Generated docs: [``docs/testing/generated/README.md``](generated/README.md)

## How to document tests
1. Add XML doc tags to each `[Fact]` or `[Theory]` method: `summary`, `intent`, `scenario`, and `behavior`.
2. Use `Trait("Category", "...")` tags when the test should participate in smoke, integration, or known-issue lanes.
3. Use `CoverageTypeAttribute` to classify evidence as `Positive`, `Negative`, `Edge`, `Fuzz`, or `Benchmark` when a test maps to a specific requirement or perf/robustness lane.
4. Prefer one-scenario requirement-home files under `tests/Incursa.OpenAI.Agents.Tests/RequirementHomes/<Area>/` for direct `LIB-*` traceability.
5. Refresh generated docs locally with `dotnet tool run incursa-testdocs -- generate --repoRoot . --outDir docs/testing/generated`.

Optional: [`Incursa.TestDocs.Analyzers`](https://www.nuget.org/packages/Incursa.TestDocs.Analyzers) warns when required tags are missing or still placeholder text.
