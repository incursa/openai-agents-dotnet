---
workbench:
  type: runbook
  workItems:
    - TASK-0009
  codeRefs:
    - /tools/upstream-sync/Invoke-UpstreamSync.ps1
    - /tools/upstream-sync/state.json
    - /tools/upstream-sync/README.md
  pathHistory: []
  path: /docs/50-runbooks/upstream-sync.md
---

# Upstream sync

Use this runbook when you need to review and translate new upstream changes from the local `openai-agents-python` and `openai-agents-js` clones into this .NET repository.

## Source priority

1. `openai-agents-python` is the behavioral source of truth for included parity.
2. `openai-agents-js` is a supporting signal only.
3. When the two differ, resolve the translation in favor of Python unless this repo intentionally documents a .NET-owned divergence.

## Inputs

- Workflow script: [``tools/upstream-sync/Invoke-UpstreamSync.ps1``](../../tools/upstream-sync/Invoke-UpstreamSync.ps1)
- Workflow notes: [``tools/upstream-sync/CODEX_TRANSLATION_NOTES.md``](../../tools/upstream-sync/CODEX_TRANSLATION_NOTES.md)
- Parity scope: [``docs/parity/manifest.md``](../parity/manifest.md)
- Maintenance checklist: [``docs/parity/maintenance-checklist.md``](../parity/maintenance-checklist.md)
- Tracking state: [``tools/upstream-sync/state.json``](../../tools/upstream-sync/state.json)
- Current work item: [``docs/70-work/items/TASK-0009-upstream-sync-workflow.md``](../70-work/items/TASK-0009-upstream-sync-workflow.md)

## Operator flow

1. Refresh the repo and ensure the working tree is clean unless you intentionally opt into `-AllowDirty`.
   Use `pwsh`; the workflow is not intended to run under Windows PowerShell.
2. Run an analysis-first pass:
   - `pwsh tools/upstream-sync/Invoke-UpstreamSync.ps1 -Once -PreviewOnly`
   - `-AnalyzeOnly` is an alias when you want the intent to read as a named stage.
3. Inspect the run packet under `tools/upstream-sync/runs/<run-id>/`.
4. If triage says `no-op`, stop. The ignored local review watermark is enough.
5. If triage says `apply`, rerun without `-PreviewOnly` to execute the apply and verification phases.
6. Review the resulting branch, commit, and optional PR body against the upstream ranges before merging.

## State interpretation

- [``state.json``](../../tools/upstream-sync/state.json) records the last upstream SHA actually applied to tracked repo files per source.
- `state.local.json` records the last reviewed SHA per source. It is intentionally ignored so no-op reviews and local bootstrap watermarks do not create repo churn.
- The first run bootstraps local review baselines and exits. Re-run the workflow to process only future upstream deltas.

## Verification

Unless explicitly skipped, the apply phase runs:

- `dotnet build`
- `dotnet test`
- [``scripts/quality/validate-library-traceability.ps1``](../../scripts/quality/validate-library-traceability.ps1)

Use skip flags only when you are intentionally narrowing the loop and you plan to run the missing checks before finalizing the change.
