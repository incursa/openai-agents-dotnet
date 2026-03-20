---
id: SPIKE-0002
type: spike
status: draft
priority: medium
owner: null
created: 2026-03-19
updated: 2026-03-19
tags: []
related:
  specs: []
  adrs: []
  files:
    - /docs/20-architecture/tracing-and-exporters.md
  prs: []
  issues: []
  branches: []
title: Tracing and exporters design
---

# SPIKE-0002 - Tracing and exporters design

## Summary

Design the next tracing and exporters milestone so it can follow the audio release without reopening basic architecture questions.

## Research notes

## Acceptance criteria

- docs/20-architecture/tracing-and-exporters.md explains how tracing builds on existing observation seams.
- The design identifies the minimum OpenTelemetry or exporter surface to add later.
- The spike remains design-only and does not add runtime tracing code in this release.
