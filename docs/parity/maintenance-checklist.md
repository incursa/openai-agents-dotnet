---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/parity/maintenance-checklist.md
---

# Included Surface Maintenance Checklist

Use this checklist when upstream Python changes land in the included server-side runtime surface.

## Review Flow

1. Identify the upstream change set.
2. Check whether it touches an included doc or test family from [``docs/parity/manifest.md``](manifest.md).
3. Classify the change:
   - behavior change
   - public API/documentation change
   - new regression test only
   - excluded surface, no action
4. Update the corresponding .NET tests first or alongside the runtime change.
5. Update docs/sample code if the user-facing flow changed.

## Questions To Answer

- Did an included upstream doc page change meaningfully?
- Did an included upstream test add a new scenario we do not cover yet?
- Does the change affect runner behavior, approvals, guardrails, handoffs, sessions, MCP, or Responses streaming?
- Does the change affect DI-hosted usage, runtime observation, or persisted approval recovery?
- Does the change require a .NET-native ergonomic helper, or is it purely behavioral?
- Do the console sample or host sample need to be updated to keep the canonical flows current?

## Expected Outputs

- runtime change, if behavior changed
- test change mirroring the upstream scenario
- manifest status update if coverage changed
- README/sample updates if the public usage flow changed
