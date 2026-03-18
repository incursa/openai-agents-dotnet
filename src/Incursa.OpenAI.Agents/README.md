# Incursa.OpenAI.Agents

Core server-side agent runtime.

Included in this package:
- `Agent<TContext>` with tools, handoffs, hosted MCP, and streamable MCP server definitions
- `AgentRunner` with max-turn enforcement, session persistence, approvals, guardrails, and streaming-shaped events
- `OpenAiResponsesClient`, request/response mapping, streaming helpers, and `OpenAiResponsesRunner`
- resumable `AgentRunState<TContext>` for approval workflows
- per-user MCP auth abstractions through `McpAuthContext` and `IUserScopedMcpAuthResolver`
- typed `AgentRunOptions<TContext>` for previous-response continuation, handoff input normalization, model input filtering, reasoning item ID policy, and run-level guardrails
- `IAgentSessionStore` as the session persistence seam, `FileAgentSessionStore` as the built-in durable implementation, and `IVersionedAgentSessionStore`/`AgentSessionStoreOptions` for optimistic concurrency and retention
- `AgentSessionRecovery` helpers for persisted approval resume flows
- `IAgentRuntimeObserver` and runtime observation events for host-level logging/metrics adapters
- `AgentBuilder<TContext>` and `AgentRunRequest<TContext>` helper methods for lower-ceremony app code

Not included:
- UI or ASP.NET surfaces
- voice, realtime, browser/computer-use, evals, or tracing exporters
