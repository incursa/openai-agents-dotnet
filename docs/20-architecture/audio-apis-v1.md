---
workbench:
  type: guide
  workItems:
    - TASK-0002
    - TASK-0003
  codeRefs: []
  path: /docs/20-architecture/audio-apis-v1.md
  pathHistory: []
---

# Audio APIs v1 architecture

## Notes

## Boundary

The audio surface lives beside `Responses` rather than inside it. `Responses` remains the agent-runtime integration, while audio is a lower-level server-side client for:

- file transcription
- text-to-speech

This keeps realtime voice, device capture, and session transport concerns out of the shipped v1 audio release.

## Public Surface

Core types:

- `IOpenAiAudioClient`
- `OpenAiAudioClient`
- `OpenAiAudioTranscriptionRequest`
- `OpenAiAudioTranscriptionResponse`
- `OpenAiTranscribedWord`
- `OpenAiTranscribedSegment`
- `OpenAiSpeechGenerationRequest`
- `OpenAiSpeechGenerationResponse`

Extensions types:

- `OpenAiAudioOptions`
- `AddOpenAiAudio(IServiceCollection, Action<OpenAiAudioOptions>?)`

## Data Flow

1. The caller provides an audio stream plus file name and a repo-owned transcription request, or a repo-owned speech request.
2. `OpenAiAudioClient` creates a model-scoped upstream `OpenAI.Audio.AudioClient` through an internal factory.
3. The upstream SDK performs the request using the configured `HttpClient`, auth header, and base address.
4. The adapter normalizes the upstream result into repo-owned response types.

## Design Rules

- Keep all caller-facing types SDK-independent.
- Keep the API server-oriented: streams in, bytes out, no file-path helpers or device abstractions.
- Use an internal SDK factory seam so tests can verify option mapping without network calls.
- Preserve timestamp and duration metadata when the upstream SDK returns it.

## Follow-on Design

Realtime voice is a separate subsystem because it needs long-lived session transport, partial event streams, turn/VAD behavior, and output-audio streaming. That work is captured in separate design artifacts and does not change the v1 audio API.
