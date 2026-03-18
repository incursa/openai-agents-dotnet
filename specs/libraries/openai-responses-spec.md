# OpenAI Responses Specification

## Scope
This specification covers the OpenAI Responses surface in `Incursa.OpenAI.Agents`:

- request/response mapping
- structured-output schema generation
- OpenAI Responses client
- turn executor and runner behavior
- streaming event helpers and streamed tool-call reconstruction

## Requirements
- `LIB-OAI-API-001`: Public OpenAI Responses types are tracked by the public API baselines for `Incursa.OpenAI.Agents`.
- `LIB-OAI-MAP-001`: Request mapping includes model selection, previous-response continuation, tools, handoffs, hosted MCP tools, and structured-output contracts.
- `LIB-OAI-MCP-001`: Local streamable MCP servers are resolved into OpenAI tool payloads before turn execution.
- `LIB-OAI-HANDOFF-001`: Handoff normalization options remove pre-handoff tool-call items from model input when configured.
- `LIB-OAI-FILTER-001`: Run-level model input filters are applied during request mapping.
- `LIB-OAI-REASONING-001`: Reasoning item ID policy can omit reasoning IDs from mapped input.
- `LIB-OAI-STREAM-001`: Streaming execution reconstructs completed function-call arguments for emitted tool-call run items and final tool-call results.
