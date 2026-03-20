---
id: TASK-0003
type: task
status: ready
priority: high
owner: null
created: 2026-03-19
updated: 2026-03-19
tags: []
related:
  specs: []
  adrs: []
  files:
    - /docs/20-architecture/audio-apis-v1.md
    - src/Incursa.OpenAI.Agents/OpenAI/
  prs: []
  issues: []
  branches: []
title: OpenAI audio client foundation and contracts
---

# TASK-0003 - OpenAI audio client foundation and contracts

## Summary

Add the repo-owned standalone audio client contracts and the core OpenAI audio adapter beside the existing Responses integration.

## Acceptance criteria

- Core audio contracts exist for transcription and speech generation.
- The implementation builds on OpenAI.Audio.AudioClient rather than custom multipart HTTP transport.
- The public API stays additive and independent from the upstream SDK types.
