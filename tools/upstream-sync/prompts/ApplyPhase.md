You are phase 2 of the upstream sync workflow for this repository.

Read these inputs first:
- `{{RUN_SUMMARY_PATH}}`
- `{{TRIAGE_JSON_PATH}}`
- `{{TRIAGE_MD_PATH}}`
- `AGENTS.md`
- `docs/parity/manifest.md`
- `docs/parity/maintenance-checklist.md`
- `docs/50-runbooks/upstream-sync.md`
- `docs/70-work/items/TASK-0009-upstream-sync-workflow.md`
- `tools/upstream-sync/CODEX_TRANSLATION_NOTES.md`

Workflow contract:
1. The upstream Python repo remains the behavioral source of truth.
2. The upstream JavaScript repo is a supporting signal only. Use it to clarify behavior, docs, or tests, but do not let it override Python when they differ.
3. Implement only the repo changes justified by the triage artifact.
4. Update specs, docs, samples, tests, and .NET code only when the upstream delta requires it.
5. Preserve the existing .NET architecture and keep the changes minimal and reviewable.
6. Do not modify files under `tools/upstream-sync/runs/{{RUN_ID}}` except to read the triage packet.
7. Do not edit `tools/upstream-sync/state.json` or `tools/upstream-sync/state.local.json` directly in this phase. The orchestrator updates those after validation succeeds.

Execution goals:
1. Apply the required translation work in this repository.
2. Mirror new or changed upstream behavior for the included surface only.
3. Add or adjust tests only when they are needed to secure the translated behavior.
4. Update repo-owned specifications or documentation when the upstream change alters the documented or required behavior in this .NET port.

If the triage packet says the action is `no-op`, leave the tracked repository files unchanged and exit cleanly.
