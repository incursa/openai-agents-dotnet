---
workbench:
  type: guide
  workItems:
    - SPIKE-0001
  codeRefs: []
  path: /docs/20-architecture/realtime-voice-subsystem.md
  pathHistory: []
---

# Realtime voice subsystem design

## Notes

## Summary

Realtime voice is a future server-side subsystem, not an extension of the file audio API. It needs a separate session-oriented design for audio ingress, event streaming, and turn handling.

## Fixed Decisions

- The repo will stay server-side only.
- Realtime support will not include microphone, browser, or device capture APIs.
- The subsystem will accept caller-supplied encoded audio chunks or streams.
- The subsystem will expose partial transcript and output-audio events as part of a session model.

## Expected Surface

- session creation and disposal
- audio input append/send operations
- partial and final transcription events
- output audio events
- turn/VAD configuration
- failure and reconnect behavior

## Relationship To Audio v1

The shipped audio v1 surface covers file transcription and text-to-speech only. Realtime is intentionally separated so the v1 API does not inherit transport and state-management complexity.
