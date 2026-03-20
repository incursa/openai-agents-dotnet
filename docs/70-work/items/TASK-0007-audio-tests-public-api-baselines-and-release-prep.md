---
id: TASK-0007
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
    - tests/Incursa.OpenAI.Agents.Tests/
  prs: []
  issues: []
  branches: []
title: Audio tests public API baselines and release prep
---

# TASK-0007 - Audio tests public API baselines and release prep

## Summary

Add focused tests, public API baselines, and release hardening for the new audio surface.

## Acceptance criteria

- Unit tests cover request mapping, response mapping, error wrapping, and DI registration.
- PublicAPI.Unshipped.txt captures all new public types during development.
- The release is ready to promote the new public surface to shipped baselines.
