# Incursa.OpenAI.Agents.Extensions

Host integration package for the core runtime and OpenAI Responses adapter.

Included in this package:
- `AddIncursaAgents()` for the core runtime, default approval service, and runtime observation wiring
- `AddFileAgentSessions()` for file-backed versioned session persistence and retention settings
- `AddOpenAiResponses()` for `HttpClient` setup, `OpenAiResponsesRunner`, and MCP observation/retry configuration
- logging-backed observation sinks for `IAgentRuntimeObserver` and `IMcpClientObserver`

This package is additive. The core runtime and OpenAI adapter still work without DI or `Microsoft.Extensions.*`.
