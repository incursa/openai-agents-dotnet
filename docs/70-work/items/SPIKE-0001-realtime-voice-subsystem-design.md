---
id: SPIKE-0001
type: spike
status: draft
priority: medium
owner: null
created: 2026-03-19
updated: 2026-03-19
tags: []
related:
  specs: []
  adrs:
    - /docs/40-decisions/adr-realtime-voice-separate-subsystem.md
  files:
    - /docs/20-architecture/realtime-voice-subsystem.md
  prs: []
  issues: []
  branches: []
title: Realtime voice subsystem design
---

# SPIKE-0001 - Realtime voice subsystem design

## Summary

Design the future server-side realtime voice subsystem without implementing it in this release.

## Research notes

## Acceptance criteria

- docs/20-architecture/realtime-voice-subsystem.md defines subsystem boundaries and session model.
- The design keeps microphone/device capture out of repo scope.
- The design identifies the minimum future public surface needed for realtime voice.
