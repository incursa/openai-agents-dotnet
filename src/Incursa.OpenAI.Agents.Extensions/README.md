# Incursa.OpenAI.Agents.Extensions

Host integration package for the core runtime and OpenAI Responses adapter.

Included in this package:
- `AddIncursaAgents()` for the core runtime, default approval service, and runtime observation wiring
- `AddAgentSessionStore()` and `AddVersionedAgentSessionStore()` for swapping in custom session backends
- `AddFileAgentSessions()` for file-backed versioned session persistence and retention settings
- provider-specific adapters like `Incursa.OpenAI.Agents.Storage.Azure` and `Incursa.OpenAI.Agents.Storage.S3` for production-backed sessions
- `AddOpenAiResponses()` for `HttpClient` setup, `OpenAiResponsesRunner`, and MCP observation/retry configuration
- `AddOpenAiAudio()` for `HttpClient` setup and `IOpenAiAudioClient` resolution
- `AgentOutputContractFactory.ForJsonSchema(...)` convenience helpers for generating explicit output schemas from CLR types via `Microsoft.Extensions.AI.Abstractions`
- logging-backed observation sinks for `IAgentRuntimeObserver` and `IMcpClientObserver`

This package is additive. The core runtime and OpenAI adapter still work without DI or `Microsoft.Extensions.*`.
