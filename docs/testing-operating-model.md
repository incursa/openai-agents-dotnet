---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/testing-operating-model.md
---

# Testing Operating Model

The repo uses four quality lanes:

- `smoke`: fast curated checks tagged with `Category=Smoke`
- `blocking`: required non-Docker validation over the maintained solution
- `observational`: visible `Category=KnownIssue` regressions that do not block the lane
- `advisory`: blocking tests plus package coverage, then Workbench normalization
- `integration`: Docker-backed checks tagged with `Category=Integration` and `RequiresDocker=true`

Local commands:

```powershell
pwsh -File scripts/quality/run-smoke-tests.ps1
pwsh -File scripts/quality/run-blocking-tests.ps1
pwsh -File scripts/quality/run-observational-tests.ps1
pwsh -File scripts/quality/run-quality-evidence.ps1
dotnet test tests/Incursa.OpenAI.Agents.Storage.Azure.IntegrationTests/Incursa.OpenAI.Agents.Storage.Azure.IntegrationTests.csproj
dotnet test tests/Incursa.OpenAI.Agents.Storage.S3.IntegrationTests/Incursa.OpenAI.Agents.Storage.S3.IntegrationTests.csproj
pwsh -File scripts/testing/prepull-test-images.ps1
```

The Azure Blob and S3 suites are Docker-backed by design. When Docker is unavailable, treat those runs as environment-blocked rather than product regressions.

Traceability is governed by:

- [``specs/libraries/*.md``](../specs/libraries/library-conformance-matrix.md)
- [``specs/libraries/library-conformance-matrix.md``](../specs/libraries/library-conformance-matrix.md)
- [``scripts/quality/validate-library-traceability.ps1``](../scripts/quality/validate-library-traceability.ps1)
- [``src/*/PublicAPI.Shipped.txt``](../src/)

The Microsoft public API analyzer baselines and the authored `LIB-*` scenarios are both treated as quality evidence: a public API row is only considered covered when it maps to baseline files and automated tests.
