---
id: TASK-0005
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
title: Text-to-speech implementation
---

# TASK-0005 - Text-to-speech implementation

## Summary

Implement text-to-speech generation on the new core audio surface with binary output suitable for host applications.

## Acceptance criteria

- Speech generation accepts repo-owned request settings for model, voice, format, and optional instructions.
- Speech generation returns audio bytes and output format metadata.
- The feature is server-side and does not add device or UI helpers.
