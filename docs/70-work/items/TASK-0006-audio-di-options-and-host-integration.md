---
id: TASK-0006
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
    - src/Incursa.OpenAI.Agents.Extensions/
  prs: []
  issues: []
  branches: []
title: Audio DI options and host integration
---

# TASK-0006 - Audio DI options and host integration

## Summary

Add DI registration and options for the audio surface so host applications can configure it the same way as the existing Responses integration.

## Acceptance criteria

- OpenAiAudioOptions configures API key, base address, and named client behavior.
- AddOpenAiAudio registers the audio client and resolves successfully from DI.
- Audio DI stays additive and does not break existing AddOpenAiResponses usage.
