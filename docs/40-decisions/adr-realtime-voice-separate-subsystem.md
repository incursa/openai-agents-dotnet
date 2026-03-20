---
workbench:
  type: adr
  workItems:
    - TASK-0002
    - SPIKE-0001
  codeRefs: []
  path: /docs/40-decisions/adr-realtime-voice-separate-subsystem.md
  pathHistory: []
---

# Realtime voice as separate subsystem

## Status

Accepted

## Context

Realtime voice requires session transport, partial event streams, turn/VAD behavior, and audio chunk ingress. The current repository is explicitly server-side and excludes UI/device capture surfaces, so realtime voice is not equivalent to adding another file-audio method.

## Decision

Do not ship realtime voice in the first audio release.

- Ship file transcription and text-to-speech first.
- Track realtime voice with dedicated design artifacts and backlog items.
- Keep microphone/device capture out of repo scope even when realtime is implemented later.

## Consequences

- The first audio release stays smaller and easier to verify.
- The realtime design can evolve independently without forcing breaking changes into the file-audio API.
- The repository’s server-side scope remains coherent.
