---
id: TASK-0009
type: task
status: ready
priority: medium
owner: null
created: 2026-04-22
updated: 2026-04-22
tags: []
related:
  specs: []
  adrs: []
  files:
    - /tools/upstream-sync/Invoke-UpstreamSync.ps1
    - /tools/upstream-sync/state.json
    - /docs/50-runbooks/upstream-sync.md
    - /docs/parity/manifest.md
    - /docs/parity/maintenance-checklist.md
  prs: []
  issues: []
  branches: []
title: Upstream sync workflow
---

# TASK-0009 - Upstream sync workflow

## Summary

Standardize how this repository reviews upstream changes, reconciles Python and JavaScript inputs, updates parity-facing documentation when needed, and translates included behavior into the .NET port.

## Acceptance criteria

- `tools/upstream-sync` supports Python-first multi-source review and translation.
- The workflow distinguishes local review watermarks from tracked applied state.
- A runbook documents the operator flow, source priority, and verification expectations.
- The parity maintenance docs explain how JavaScript is consulted without replacing Python as the source of truth.
