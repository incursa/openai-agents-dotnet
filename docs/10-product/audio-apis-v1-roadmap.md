---
workbench:
  type: spec
  workItems:
    - TASK-0001
  codeRefs: []
  path: /docs/10-product/audio-apis-v1-roadmap.md
  pathHistory: []
---

# Audio APIs v1 roadmap

## Summary

`Incursa.OpenAI.Agents` adds a server-side audio surface in v1.1.0 that covers file transcription and text-to-speech beside the existing OpenAI Responses integration. The shipped scope is intentionally additive and excludes realtime voice, microphone capture, browser/device helpers, translation, and streamed transcription events.

## Goals

- Add a repo-owned standalone audio API in the core package.
- Support file transcription with optional word and segment timestamps.
- Support text-to-speech with binary output that host applications can store or stream.
- Add DI registration in the extensions package that matches the existing Responses ergonomics.
- Update repo scope docs and samples so the new audio boundary is explicit.

## Non-goals

- Realtime voice sessions
- Microphone or browser/device capture helpers
- Translation
- Streaming transcription events
- Tracing/exporters implementation in this release

## Requirements

- The release remains additive and targets a minor version by default.
- The public audio contracts stay repo-owned and do not expose upstream SDK types directly.
- The implementation builds on `OpenAI.Audio.AudioClient`.
- The audio release keeps the repo server-side only.
- Realtime voice is captured in roadmap and design artifacts but is not implemented in the shipped surface.

## Success Criteria

- Consumers can transcribe an audio stream by supplying `Stream`, `fileName`, and a repo-owned transcription request.
- Consumers can generate speech audio bytes by supplying a repo-owned speech request.
- DI resolves `IOpenAiAudioClient` through `AddOpenAiAudio(...)`.
- README, package README, product docs, and parity docs describe file audio support while keeping realtime voice excluded.

## Milestones

1. Roadmap and architecture docs
2. Core audio contracts and adapter
3. Audio DI registration and tests
4. Samples and scope docs
5. Realtime voice design follow-on
6. Tracing/exporters design follow-on
