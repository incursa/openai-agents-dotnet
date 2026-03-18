# Extensions Integration Specification

## Scope
This specification covers `Incursa.OpenAI.Agents.Extensions`:

- dependency-injection registration helpers
- runtime, session, and OpenAI options types
- runtime and MCP observability sink composition

## Requirements
- `LIB-EXT-API-001`: Public extension types are tracked by the public API baselines for `Incursa.OpenAI.Agents.Extensions`.
- `LIB-EXT-DI-001`: Service registration helpers wire runnable defaults for agents, file-backed sessions, and OpenAI Responses execution using the configured options.
- `LIB-EXT-OBS-001`: Composite runtime and MCP observers fan out observations to each registered sink and respect option-based logging toggles.
