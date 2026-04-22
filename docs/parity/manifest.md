---
workbench:
  type: doc
  workItems:
    - TASK-0008
  codeRefs: []
  pathHistory: []
  path: /docs/parity/manifest.md
---

# Included Parity Manifest

This repo does not aim for full Python SDK parity. It tracks the included server-side runtime surfaces closely enough that upstream changes in those areas can be translated deliberately.

## Included .NET Surface

- `Incursa.OpenAI.Agents`
  - `Agent<TContext>`, tools, handoffs, approvals, guardrails, sessions, runner loop, MCP abstractions, and OpenAI Responses integration
- `Incursa.OpenAI.Agents.Extensions`
  - DI registration, options, logging-backed observation, and host integration helpers

## Excluded Surface

- realtime voice
- browser/computer-use
- shell/apply-patch tools
- evals
- tracing/exporters
- hosted UI/chat widgets
- non-OpenAI providers

## Included Additions

- file audio transcription and text-to-speech

## Upstream Docs To Mirror

- `docs/running_agents.md`
- `docs/handoffs.md`
- `docs/guardrails.md`
- `docs/mcp.md`
- `docs/streaming.md`

## Upstream Test Families To Mirror

### Runner and run state

- `tests/test_agent_runner.py`
- `tests/test_agent_runner_sync.py`
- `tests/test_agent_runner_streamed.py`
- `tests/test_max_turns.py`
- `tests/test_run_config.py`
- `tests/test_run_impl_resume_paths.py`
- `tests/test_run_state.py`
- `tests/test_run_step_execution.py`
- `tests/test_run_step_processing.py`

### Handoffs and guardrails

- `tests/test_handoff_history_duplication.py`
- `tests/test_handoff_prompt.py`
- `tests/test_handoff_tool.py`
- `tests/test_guardrails.py`
- `tests/test_tool_guardrails.py`
- `tests/test_stream_input_guardrail_timing.py`

### OpenAI Responses and streaming

- `tests/test_openai_responses.py`
- `tests/test_openai_responses_converter.py`
- `tests/test_responses.py`
- `tests/test_stream_events.py`
- `tests/test_streaming_tool_call_arguments.py`

### MCP

- `tests/mcp/test_runner_calls_mcp.py`
- `tests/mcp/test_streamable_http_client_factory.py`
- `tests/mcp/test_mcp_approval.py`
- `tests/mcp/test_tool_filtering.py`
- `tests/mcp/test_caching.py`
- `tests/mcp/test_server_errors.py`

## Current Mapping

- Runner, approvals, guardrails, and sessions:
  - status: `covered`
  - [``src/Incursa.OpenAI.Agents/Core/*``](../../src/Incursa.OpenAI.Agents/Core/)
  - [``tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/AgentRunnerTests.cs)
  - [``tests/Incursa.OpenAI.Agents.Tests/ApprovalAndGuardrailTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/ApprovalAndGuardrailTests.cs)
  - [``tests/Incursa.OpenAI.Agents.Tests/SessionStoreTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/SessionStoreTests.cs)
  - [``tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/AgentBuilderTests.cs)
- MCP:
  - status: `covered`
  - `src/Incursa.OpenAI.Agents/Mcp/Mcp.cs`
  - [``tests/Incursa.OpenAI.Agents.Tests/McpTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/McpTests.cs)
- OpenAI Responses:
  - status: `covered`
  - [``src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesClient.cs``](../../src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesClient.cs)
  - [``src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesMapping.cs``](../../src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesMapping.cs)
  - [``src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesRunner.cs``](../../src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesRunner.cs)
  - [``src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesStreaming.cs``](../../src/Incursa.OpenAI.Agents/OpenAI/OpenAiResponsesStreaming.cs)
  - [``tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/OpenAiResponsesTests.cs)
- Sample application:
  - status: `covered`
  - [``samples/Incursa.OpenAI.Agents.ConsoleSample/Program.cs``](../../samples/Incursa.OpenAI.Agents.ConsoleSample/Program.cs)
  - [``samples/Incursa.OpenAI.Agents.HostSample/Program.cs``](../../samples/Incursa.OpenAI.Agents.HostSample/Program.cs)
- Host integration and observability:
  - status: `covered`
  - [``src/Incursa.OpenAI.Agents.Extensions/*``](../../src/Incursa.OpenAI.Agents.Extensions/)
  - [``tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/DependencyInjectionTests.cs)
  - [``tests/Incursa.OpenAI.Agents.Tests/RuntimeObservabilityTests.cs``](../../tests/Incursa.OpenAI.Agents.Tests/RuntimeObservabilityTests.cs)

## Translation Rule

For the included surface, prefer:

- behavioral parity with the upstream tests and docs
- `openai-agents-python` as the primary source of truth, with `openai-agents-js` used only as a supporting signal
- idiomatic .NET public APIs over Python-shaped APIs
- preserving persisted session history and applying normalization at model-input time when possible
