---
workbench:
  type: guide
  workItems:
    - SPIKE-0002
  codeRefs: []
  path: /docs/20-architecture/tracing-and-exporters.md
  pathHistory: []
---

# Tracing and exporters design

## Notes

## Summary

Tracing/exporters are the next milestone after the audio release. The implementation should build on the existing runtime observation seams rather than introducing a parallel observability stack.

## Current Building Blocks

- `IAgentRuntimeObserver`
- `IAgentRuntimeObservationSink`
- `IMcpClientObserver`
- `IMcpObservationSink`

## Design Direction

- Add tracing in the extensions package first.
- Prefer OpenTelemetry-compatible spans and exporter hooks over bespoke tracing contracts.
- Preserve the current observation callbacks so existing consumers do not break.

## Non-goals For The Audio Release

- no tracing runtime changes in the shipped audio release
- no exporter package in this milestone
- no change to the current observation event contracts unless a follow-on tracing design requires an additive extension
