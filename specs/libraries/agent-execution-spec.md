# Agent Execution Specification

## Scope
This specification covers runtime execution behavior in `Incursa.OpenAI.Agents`:

- runner and turn execution contracts
- run request/options/result/state models
- approvals and approval resumption
- input/output/tool guardrails
- in-memory and file-backed session stores
- session recovery helpers and runtime observability

## Requirements
- `LIB-EXEC-API-001`: Public execution types are tracked by the public API baselines for `Incursa.OpenAI.Agents`.
- `LIB-EXEC-RUNNER-001`: Completed runs persist final output items into the conversation and result stream.
- `LIB-EXEC-TOOLS-001`: Tool calls execute in-order and append tool outputs back into the run items.
- `LIB-EXEC-HANDOFF-001`: Handoffs switch the active agent and emit handoff occurrence items.
- `LIB-EXEC-MAXTURNS-001`: The runner stops with `MaxTurnsExceeded` when configured turn limits are exhausted.
- `LIB-EXEC-STREAM-001`: Streaming runs emit user input, generated run items, and final output items as they become available.
- `LIB-EXEC-APPROVAL-001`: Approval-required tool calls pause execution without invoking the tool until an approval response is supplied.
- `LIB-EXEC-APPROVAL-002`: Rejected approvals use the configured tool error formatter when producing rejection items.
- `LIB-EXEC-GUARDRAIL-001`: Guardrail tripwires stop execution and surface the guardrail message on the result.
- `LIB-EXEC-OBS-001`: Runtime execution emits lifecycle observations for run start, turn start, turn completion, and successful completion events.
- `LIB-EXEC-SESSION-001`: Session stores preserve conversation state, response IDs, and versioning semantics across loads and saves.
- `LIB-EXEC-SESSION-002`: File-backed session persistence uses atomic replacement semantics and can clean up expired sessions.
