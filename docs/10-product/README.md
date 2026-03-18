---
workbench:
  type: spec
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/10-product/README.md
---

# Product

## Core concept

This repository focuses on the same primitives shown in the upstream Agents SDK but only for .NET server-side execution:

- agents with instructions and tool access
- handoffs for multi-agent workflows
- guardrails for defensive execution
- approvals for sensitive operations
- session-backed conversation state
- MCP tool calling for streamable HTTP tool servers

## Operational stance

`Incursa.OpenAI.Agents` is intentionally conservative in scope:

- OpenAI Responses is the primary runtime integration.
- Per-user MCP context is handled through `IUserScopedMcpAuthResolver`.
- Runtime behavior is built to be deterministic and hostable in DI containers.
- Durable local session state is provided through `FileAgentSessionStore` and retention options, while alternate backends can plug in through `IAgentSessionStore`.

## Mapping from upstream

The following upstream areas were intentionally adapted, while non-core or ecosystem features are deferred:

### Included

- Agents as orchestration primitives
- Function-like tools and tool filtering
- Handoffs and delegation flows
- Guardrails
- Human-in-the-loop approvals
- Streaming events and resumed sessions

### Deferred

- realtime/voice
- hosted UI and browser/computer-use
- multi-provider abstractions
- advanced tracing/evals/distillation tooling

Parity intent and maintenance expectations for these included areas are tracked in:

- `docs/parity/manifest.md`
- `docs/parity/maintenance-checklist.md`
