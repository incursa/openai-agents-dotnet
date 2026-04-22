# Upstream Sync Workflow

This folder hosts a PowerShell workflow that reviews upstream changes from the local `openai-agents-python` and `openai-agents-js` clones, triages the relevant deltas with Codex, applies the required .NET/spec/doc updates, validates them locally, and optionally pushes/opens a PR for the translated changes.

## Requirements

- PowerShell 7 (`pwsh`)
- `git`, `codex`, and `dotnet` must be on `PATH`
- `gh` must be on `PATH` when you want the workflow to push and/or create a PR
- A clean `Incusra.OpenAI.Agents` working tree (unless overridden with `-AllowDirty`)
- The upstream repositories must be cloned at:
  - `C:\src\openai\openai-agents-python`
  - `C:\src\openai\openai-agents-js`

## State files

- [``state.json``](state.json) (tracked): stores the configured upstream sources and the last applied SHA per source that this repository has translated into tracked repo changes.
- [``state.local.json``](state.local.json) (ignored): stores the last reviewed SHA per source, the last attempted SHA, the bootstrap watermark, and the most recent local run metadata.
- [``runs/``](runs/) (ignored): per-run packets containing the commit lists, changed files, diffs, triage JSON/Markdown, and captured Codex responses.

The first invocation bootstraps local review baselines by recording the current upstream heads in [``state.local.json``](state.local.json) and exiting without translating. That keeps the committed [``state.json``](state.json) honest: it advances only when the repo actually applies upstream behavior.

## Workflow stages

Each non-preview run executes these stages:

1. Fetch each selected upstream and diff from the last locally reviewed SHA.
2. Write a run packet under [``runs/``](runs/).
3. Invoke Codex phase 1 to create `triage.json` and `triage.md`.
4. If triage says the delta is in scope, invoke Codex phase 2 to apply the repo changes.
5. Validate with `dotnet build`, `dotnet test`, and `scripts/quality/validate-library-traceability.ps1` unless skipped.
6. Update [``state.json``](state.json) for the sources whose changes were actually applied, then commit/push/PR if enabled.

If triage concludes the delta is outside the included .NET parity surface, only the ignored local review state advances.

## Running

- One-shot mode:
  ```
  pwsh tools/upstream-sync/Invoke-UpstreamSync.ps1 -Once
  ```

- Review only one source:
  ```
  pwsh tools/upstream-sync/Invoke-UpstreamSync.ps1 -Once -Sources python
  ```

- Preview or analysis-only without mutating tracked repo state (`-AnalyzeOnly` is an alias):
  ```
  pwsh tools/upstream-sync/Invoke-UpstreamSync.ps1 -Once -PreviewOnly
  ```

- Loop mode (runs every 5 minutes by default):
  ```
  pwsh tools/upstream-sync/Invoke-UpstreamSync.ps1 -Loop -IntervalMinutes 5
  ```

- Force translation from a specific SHA for a single selected source:
  ```
  pwsh tools/upstream-sync/Invoke-UpstreamSync.ps1 -Once -Sources python -ForceFromSha <sha>
  ```

- Skip steps when desired:
  - `-SkipBuild`, `-SkipTests` disable the respective `dotnet` commands.
  - `-SkipTraceability` disables `scripts/quality/validate-library-traceability.ps1`.
  - `-SkipPush` avoids pushing the sync branch (also prevents PR creation).
  - `-SkipPr` stops PR creation after a push.
  - `-AllowDirty` lets the watcher run even if the repo already has uncommitted changes (use with care).

## Codex invocation

The workflow runs two Codex passes:

- Phase 1 (`prompts/TriagePhase.md`) produces only run-local triage artifacts.
- Phase 2 (`prompts/ApplyPhase.md`) performs the actual repo changes justified by the triage packet.

Both phases run via `codex exec --dangerously-bypass-approvals-and-sandbox` from the repository root, with the selected upstream clones added via repeated `--add-dir` arguments.

## Post-sync state

Successful syncs commit the translated files plus the updated [``state.json``](state.json), push a `sync/upstream-<timestamp>` branch, and call `gh pr create` with a title/body that references the upstream ranges and summaries.

Failed runs leave the branch, working tree, and ignored run packet intact for inspection. [``state.json``](state.json) is updated only when translation and validation succeed.
