---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/parity/maintenance-checklist.md
---

# Included Surface Maintenance Checklist

Use this checklist when tracked upstream changes land in the included server-side runtime surface.

## Review Flow

1. Identify the upstream change set across the tracked source repos.
2. Treat `openai-agents-python` as the behavioral source of truth and `openai-agents-js` as a supporting signal.
3. Check whether it touches an included doc or test family from [``docs/parity/manifest.md``](manifest.md).
4. Classify the change:
   - behavior change
   - public API/documentation change
   - new regression test only
   - excluded surface, no action
5. Update the corresponding .NET tests first or alongside the runtime change.
6. Update docs/sample code if the user-facing flow changed.

## Questions To Answer

- Did an included upstream doc page change meaningfully?
- Did an included upstream test add a new scenario we do not cover yet?
- Did JavaScript clarify the intended behavior without contradicting Python?
- Does the change affect runner behavior, approvals, guardrails, handoffs, sessions, MCP, or Responses streaming?
- Does the change affect DI-hosted usage, runtime observation, or persisted approval recovery?
- Does the change require a .NET-native ergonomic helper, or is it purely behavioral?
- Do the console sample or host sample need to be updated to keep the canonical flows current?

## Expected Outputs

- runtime change, if behavior changed
- test change mirroring the upstream scenario
- manifest status update if coverage changed
- README/sample updates if the public usage flow changed
