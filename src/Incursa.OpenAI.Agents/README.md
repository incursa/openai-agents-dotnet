# Incursa.OpenAI.Agents

Core server-side agent runtime.

Included in this package:
- [`Agent<TContext>`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/Agent.cs) with tools, handoffs, hosted MCP, and streamable MCP server definitions
- [`AgentRunner`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/AgentRunner.cs) with max-turn enforcement, session persistence, approvals, guardrails, and streaming-shaped events
- [`OpenAiResponsesClient`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesClient.cs), request/response mapping, streaming helpers, and [`OpenAiResponsesRunner`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesRunner.cs)
- [`OpenAiAudioClient`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/OpenAI/OpenAiAudioClient.cs) with repo-owned request/response contracts for file transcription and text-to-speech
- explicit JSON-schema-based structured output contracts via [`AgentOutputContract`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/AgentOutputContract.cs)
- resumable [`AgentRunState<TContext>`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/Conversation.cs) for approval workflows
- per-user MCP auth abstractions through [`McpAuthContext`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Mcp/McpAuthContext.cs) and [`IUserScopedMcpAuthResolver`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Mcp/IUserScopedMcpAuthResolver.cs)
- typed [`AgentRunOptions<TContext>`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/Conversation.cs) for previous-response continuation, handoff input normalization, model input filtering, reasoning item ID policy, and run-level guardrails
- [`IAgentSessionStore`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/IAgentSessionStore.cs) as the session persistence seam, [`FileAgentSessionStore`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/FileAgentSessionStore.cs) as the built-in durable implementation, and [`IVersionedAgentSessionStore`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/IVersionedAgentSessionStore.cs)/[`AgentSessionStoreOptions`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/AgentSessionStoreOptions.cs) for optimistic concurrency and retention
- [`AgentSessionRecovery`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/SessionRecovery.cs) helpers for persisted approval resume flows
- [`IAgentRuntimeObserver`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/IAgentRuntimeObserver.cs) and runtime observation events for host-level logging/metrics adapters
- [`AgentBuilder<TContext>`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/AgentBuilder.cs) and [`AgentRunRequest<TContext>`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents/Core/Conversation.cs) helper methods for lower-ceremony app code

Not included:
- UI or ASP.NET surfaces
- realtime voice, browser/computer-use, evals, or tracing exporters
