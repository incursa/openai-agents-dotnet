---
workbench:
  type: adr
  workItems:
    - TASK-0002
  codeRefs: []
  path: /docs/40-decisions/adr-standalone-audio-api-surface.md
  pathHistory: []
---

# Standalone audio API surface

## Status

Accepted

## Context

The repository already exposes a standalone `Responses` adapter and keeps its public contracts repo-owned. Adding audio support inside the `Responses` surface would mix unrelated concerns and make the agent-runtime layer responsible for lower-level audio operations.

## Decision

Add a separate standalone audio API beside `Responses`.

- The core package exposes repo-owned audio request and response contracts.
- The implementation uses the upstream `OpenAI.Audio.AudioClient` internally.
- The extensions package adds `AddOpenAiAudio(...)` separately from `AddOpenAiResponses(...)`.

## Consequences

- Audio support stays additive and easier to version independently.
- The public API remains stable even if the upstream SDK changes its audio types.
- Consumers can use audio without taking on the agent-runtime abstractions.
