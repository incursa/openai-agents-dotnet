---
id: TASK-0004
type: task
status: draft
priority: high
owner: null
created: 2026-03-19
updated: 2026-03-19
tags: []
related:
  specs: []
  adrs: []
  files:
    - src/Incursa.OpenAI.Agents/OpenAI/
  prs: []
  issues: []
  branches: []
title: Audio transcription implementation
---

# TASK-0004 - Audio transcription implementation

## Summary

Implement file transcription with optional timestamp-rich responses using the new core audio surface.

## Acceptance criteria

- Transcription accepts Stream plus file name.
- Transcription returns text reliably.
- Word and segment timestamps are preserved when requested and returned by the upstream API.
