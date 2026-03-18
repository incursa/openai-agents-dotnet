# Agent Composition Specification

## Scope
This specification covers construction-time agent APIs in `Incursa.OpenAI.Agents`:

- `Agent<TContext>`
- `AgentInstructions<TContext>`
- `AgentTool<TContext>` and related tool contracts
- `AgentHandoff<TContext>` and related handoff contracts
- `AgentBuilder<TContext>`
- output contracts, metadata, and model settings

## Requirements
- `LIB-COMP-API-001`: Public composition types are tracked by the public API baselines for `Incursa.OpenAI.Agents`.
- `LIB-COMP-BUILDER-001`: `AgentBuilder<TContext>` preserves configured model, instructions, tools, handoffs, MCP definitions, metadata, model settings, and output contracts when building an agent.
- `LIB-COMP-HANDOFF-001`: Handoff definitions preserve the target agent identity and descriptive metadata needed for runtime routing.
- `LIB-COMP-TOOLS-001`: Tool definitions preserve schemas, names, descriptions, and result shape metadata required for runtime execution.
- `LIB-COMP-REQUEST-001`: `AgentRunRequest<TContext>` helper methods preserve session and previous-response continuation metadata for follow-up execution.
