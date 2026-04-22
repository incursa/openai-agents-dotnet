You are phase 1 of the upstream sync workflow for this repository.

Read these inputs first:
- `{{RUN_SUMMARY_PATH}}`
- `AGENTS.md`
- `docs/parity/manifest.md`
- `docs/parity/maintenance-checklist.md`
- `docs/50-runbooks/upstream-sync.md`
- `docs/70-work/items/TASK-0009-upstream-sync-workflow.md`
- `tools/upstream-sync/CODEX_TRANSLATION_NOTES.md`

Workflow contract:
1. The upstream Python repo remains the behavioral source of truth.
2. The upstream JavaScript repo is a supporting signal only. Use it to clarify behavior, docs, or tests, but do not let it override Python when they differ.
3. This phase is analysis-only. Do not modify tracked repository files outside `tools/upstream-sync/runs/{{RUN_ID}}`.
4. Treat excluded-surface changes as reviewed no-ops.

Write both of these files:
- `{{TRIAGE_JSON_PATH}}`
- `{{TRIAGE_MD_PATH}}`

The JSON file must be valid UTF-8 JSON with this shape:
```json
{
  "action": "apply",
  "summary": "Short sentence describing the required work.",
  "includedSources": ["python"],
  "excludedSources": ["js"],
  "needsCodeUpdate": true,
  "needsTestUpdate": true,
  "needsSpecUpdate": false,
  "needsDocUpdate": false
}
```

Rules for the JSON contract:
- `action` must be either `apply` or `no-op`.
- `summary` must be a single concise sentence.
- `includedSources` must list only source keys that require tracked repo changes.
- `excludedSources` must list reviewed source keys that do not require tracked repo changes.
- `needs*` flags must reflect the actual work needed in the apply phase.
- When `action` is `no-op`, `includedSources` must be empty.

The Markdown file should explain:
1. What changed upstream by source.
2. Which changes are inside or outside the included .NET parity surface.
3. Which repo areas need updates, if any.
4. Any conflicts where Python and JavaScript differed, with Python taking precedence.
