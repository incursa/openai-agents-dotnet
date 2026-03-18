using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Controls whether reasoning item IDs are preserved when mapping model output.
/// </summary>

public enum ReasoningItemIdPolicy
{
    /// <summary>Keep existing reasoning IDs in mapped payloads.</summary>
    Preserve,

    /// <summary>Remove reasoning IDs from mapped payloads.</summary>
    Omit,
}

/// <summary>
/// Controls how conversation history is prepared after a handoff.
/// </summary>

public enum AgentHandoffHistoryMode
{
    /// <summary>Preserve full conversation history after handoff.</summary>
    PreserveFullHistory,

    /// <summary>Normalize model input after handoff to trim internal entries.</summary>
    NormalizeModelInputAfterHandoff,
}

/// <summary>
/// Describes the terminal or intermediate state of an agent run.
/// </summary>

public enum AgentRunStatus
{
    /// <summary>Run completed successfully.</summary>
    Completed,

    /// <summary>Tool execution requires approval before continuing.</summary>
    ApprovalRequired,

    /// <summary>Guardrail blocked execution.</summary>
    GuardrailTriggered,

    /// <summary>Turn limit was exceeded before completion.</summary>
    MaxTurnsExceeded,

    /// <summary>Run ended while cancelled.</summary>
    Cancelled,
}

/// <summary>
/// Defines canonical conversation item type names used by the runtime.
/// </summary>

public static class AgentItemTypes
{
    /// <summary>User provided text input item.</summary>
    public const string UserInput = "user_input";

    /// <summary>Assistant message output item.</summary>
    public const string MessageOutput = "message_output";

    /// <summary>Reasoning item emitted by the model.</summary>
    public const string Reasoning = "reasoning";

    /// <summary>Tool call request item.</summary>
    public const string ToolCall = "tool_call";

    /// <summary>Tool output item.</summary>
    public const string ToolOutput = "tool_output";

    /// <summary>Request to hand off to another agent.</summary>
    public const string HandoffRequested = "handoff_requested";

    /// <summary>Handoff completion item.</summary>
    public const string HandoffOccurred = "handoff_occurred";

    /// <summary>Approval requirement item.</summary>
    public const string ApprovalRequired = "approval_required";

    /// <summary>Tool approval rejection item.</summary>
    public const string ApprovalRejected = "approval_rejected";

    /// <summary>Guardrail tripwire triggered item.</summary>
    public const string GuardrailTripwire = "guardrail_tripwire";

    /// <summary>Final output item.</summary>
    public const string FinalOutput = "final_output";

    /// <summary>List MCP tools tool result.</summary>
    public const string McpListTools = "mcp_list_tools";
}

/// <summary>
/// Defines canonical stream event type names used by the runtime.
/// </summary>

public static class AgentStreamEventTypes
{
    /// <summary>Stream event for a run item payload.</summary>
    public const string RunItem = "run_item";

    /// <summary>Stream event for agent metadata updates.</summary>
    public const string AgentUpdated = "agent_updated";

    /// <summary>Stream event for raw model output.</summary>
    public const string RawModelEvent = "raw_model_event";
}

/// <summary>
/// Carries the data contract for AgentConversationItem.
/// </summary>

public sealed record AgentConversationItem
{
    /// <summary>Creates a run item from explicit type, role, and agent name.</summary>
    [JsonConstructor]
    public AgentConversationItem(string itemType, string role, string agentName)
        : this(itemType, role, agentName, null, null, null, null, null, null)
    {
    }

    /// <summary>Creates a conversation item with full metadata.</summary>
    public AgentConversationItem(
        string itemType,
        string role,
        string agentName,
        string? name,
        string? text,
        string? toolCallId,
        JsonNode? data,
        string? status,
        DateTimeOffset? timestampUtc)
    {
        ItemType = itemType;
        Role = role;
        AgentName = agentName;
        Name = name;
        Text = text;
        ToolCallId = toolCallId;
        Data = data;
        Status = status;
        TimestampUtc = timestampUtc;
    }

    /// <summary>Gets the item type identifier.</summary>
    public string ItemType { get; init; }

    /// <summary>Gets message role.</summary>
    public string Role { get; init; }

    /// <summary>Gets agent name that generated the item.</summary>
    public string AgentName { get; init; }

    /// <summary>Gets optional tool/function name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets message text payload.</summary>
    public string? Text { get; init; }

    /// <summary>Gets optional tool-call identifier.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets optional data payload.</summary>
    public JsonNode? Data { get; init; }

    /// <summary>Gets optional status for item-specific result.</summary>
    public string? Status { get; init; }

    /// <summary>Gets optional UTC timestamp.</summary>
    public DateTimeOffset? TimestampUtc { get; init; }

    /// <summary>Converts to a run item equivalent.</summary>
    public AgentRunItem ToRunItem()
        => new(ItemType, Role, AgentName, Name, Text, ToolCallId, Data, Status, TimestampUtc);
}

/// <summary>
/// Carries the data contract for AgentRunItem.
/// </summary>

public sealed record AgentRunItem
{
    /// <summary>Creates a run item from explicit type, role, and agent name.</summary>
    [JsonConstructor]
    public AgentRunItem(string itemType, string role, string agentName)
        : this(itemType, role, agentName, null, null, null, null, null, null)
    {
    }

    /// <summary>Creates a run item with full metadata.</summary>
    public AgentRunItem(
        string itemType,
        string role,
        string agentName,
        string? name,
        string? text,
        string? toolCallId,
        JsonNode? data,
        string? status,
        DateTimeOffset? timestampUtc)
    {
        ItemType = itemType;
        Role = role;
        AgentName = agentName;
        Name = name;
        Text = text;
        ToolCallId = toolCallId;
        Data = data;
        Status = status;
        TimestampUtc = timestampUtc;
    }

    /// <summary>Gets the item type identifier.</summary>
    public string ItemType { get; init; }

    /// <summary>Gets message role.</summary>
    public string Role { get; init; }

    /// <summary>Gets name of agent that created the item.</summary>
    public string AgentName { get; init; }

    /// <summary>Gets optional tool/function name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets optional text content.</summary>
    public string? Text { get; init; }

    /// <summary>Gets optional tool call id.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets optional JSON data payload.</summary>
    public JsonNode? Data { get; init; }

    /// <summary>Gets optional status value.</summary>
    public string? Status { get; init; }

    /// <summary>Gets optional UTC event timestamp.</summary>
    public DateTimeOffset? TimestampUtc { get; init; }

    /// <summary>Converts this run item to a conversation item.</summary>
    public AgentConversationItem ToConversationItem()
        => new(ItemType, Role, AgentName, Name, Text, ToolCallId, Data, Status, TimestampUtc);
}

/// <summary>
/// Carries the data contract for AgentFinalOutput.
/// </summary>

public sealed record AgentFinalOutput
{
    /// <summary>Creates an empty final output.</summary>
    public AgentFinalOutput()
        : this(null, null, null, null)
    {
    }

    /// <summary>Creates a final output from text.</summary>
    public AgentFinalOutput(string? text)
        : this(text, null, null, null)
    {
    }

    /// <summary>Creates a typed final output.</summary>
    public AgentFinalOutput(string? text, JsonNode? structuredValue, Type? outputType, string? responseId)
    {
        Text = text;
        StructuredValue = structuredValue;
        OutputType = outputType;
        ResponseId = responseId;
    }

    /// <summary>Gets plain-text final output.</summary>
    public string? Text { get; init; }

    /// <summary>Gets structured final output.</summary>
    public JsonNode? StructuredValue { get; init; }

    /// <summary>Gets expected output CLR type.</summary>
    public Type? OutputType { get; init; }

    /// <summary>Gets response identifier.</summary>
    public string? ResponseId { get; init; }
}

/// <summary>
/// Carries the context used by agent handoff history transform.
/// </summary>

public sealed record AgentHandoffHistoryTransformContext<TContext>
{
    /// <summary>Creates a context for handoff history transformation.</summary>
    public AgentHandoffHistoryTransformContext(
        string currentAgentName,
        string targetAgentName,
        TContext context,
        string sessionKey,
        JsonNode? arguments,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        CurrentAgentName = currentAgentName;
        TargetAgentName = targetAgentName;
        Context = context;
        SessionKey = sessionKey;
        Arguments = arguments;
        Conversation = conversation;
    }

    /// <summary>Gets source agent name.</summary>
    public string CurrentAgentName { get; init; }

    /// <summary>Gets target agent name.</summary>
    public string TargetAgentName { get; init; }

    /// <summary>Gets context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets handoff arguments.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets conversation to transform.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}

/// <summary>
/// Carries the context used by agent model input.
/// </summary>

public sealed record AgentModelInputFilterContext<TContext>
{
    /// <summary>Creates a model input filter context.</summary>
    public AgentModelInputFilterContext(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        int turnNumber,
        string? previousResponseId,
        IReadOnlyList<AgentConversationItem> conversation)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        TurnNumber = turnNumber;
        PreviousResponseId = previousResponseId;
        Conversation = conversation;
    }

    /// <summary>Gets current agent.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets user-provided context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets current turn number.</summary>
    public int TurnNumber { get; init; }

    /// <summary>Gets previous response identifier.</summary>
    public string? PreviousResponseId { get; init; }

    /// <summary>Gets conversation items passed to model.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }
}

/// <summary>
/// Configures agent run.
/// </summary>

public sealed record AgentRunOptions<TContext>
{
    /// <summary>Creates default run options.</summary>
    public AgentRunOptions()
        : this(null, null, AgentHandoffHistoryMode.PreserveFullHistory, null, null, ReasoningItemIdPolicy.Preserve, null, null)
    {
    }

    /// <summary>Creates fully initialized run options.</summary>
    public AgentRunOptions(
        string? previousResponseId,
        Func<AgentToolErrorContext, string?>? toolErrorFormatter,
        AgentHandoffHistoryMode handoffHistoryMode,
        Func<AgentHandoffHistoryTransformContext<TContext>, CancellationToken, ValueTask<IReadOnlyList<AgentConversationItem>>>? handoffHistoryTransformerAsync,
        Func<AgentModelInputFilterContext<TContext>, CancellationToken, ValueTask<IReadOnlyList<AgentConversationItem>>>? modelInputFilterAsync,
        ReasoningItemIdPolicy reasoningItemIdPolicy,
        IReadOnlyList<IInputGuardrail<TContext>>? inputGuardrails,
        IReadOnlyList<IOutputGuardrail<TContext>>? outputGuardrails)
    {
        PreviousResponseId = previousResponseId;
        ToolErrorFormatter = toolErrorFormatter;
        HandoffHistoryMode = handoffHistoryMode;
        HandoffHistoryTransformerAsync = handoffHistoryTransformerAsync;
        ModelInputFilterAsync = modelInputFilterAsync;
        ReasoningItemIdPolicy = reasoningItemIdPolicy;
        InputGuardrails = inputGuardrails;
        OutputGuardrails = outputGuardrails;
    }

    /// <summary>Gets previous response identifier for continuation.</summary>
    public string? PreviousResponseId { get; init; }

    /// <summary>Gets optional tool error formatter.</summary>
    public Func<AgentToolErrorContext, string?>? ToolErrorFormatter { get; init; }

    /// <summary>Gets handoff history handling mode.</summary>
    public AgentHandoffHistoryMode HandoffHistoryMode { get; init; }

    /// <summary>Gets handoff history transform callback.</summary>
    public Func<AgentHandoffHistoryTransformContext<TContext>, CancellationToken, ValueTask<IReadOnlyList<AgentConversationItem>>>? HandoffHistoryTransformerAsync { get; init; }

    /// <summary>Gets model input filter callback.</summary>
    public Func<AgentModelInputFilterContext<TContext>, CancellationToken, ValueTask<IReadOnlyList<AgentConversationItem>>>? ModelInputFilterAsync { get; init; }

    /// <summary>Gets reasoning item id policy.</summary>
    public ReasoningItemIdPolicy ReasoningItemIdPolicy { get; init; }

    /// <summary>Gets input guardrails to evaluate.</summary>
    public IReadOnlyList<IInputGuardrail<TContext>>? InputGuardrails { get; init; }

    /// <summary>Gets output guardrails to evaluate.</summary>
    public IReadOnlyList<IOutputGuardrail<TContext>>? OutputGuardrails { get; init; }
}

/// <summary>
/// Carries the context used by agent tool error.
/// </summary>

public sealed record AgentToolErrorContext
{
    /// <summary>Creates a tool error context.</summary>
    public AgentToolErrorContext(string kind, string toolType, string toolName, string callId, string defaultMessage)
    {
        Kind = kind;
        ToolType = toolType;
        ToolName = toolName;
        CallId = callId;
        DefaultMessage = defaultMessage;
    }

    /// <summary>Gets kind of tool error.</summary>
    public string Kind { get; init; }

    /// <summary>Gets tool type string.</summary>
    public string ToolType { get; init; }

    /// <summary>Gets tool name.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets tool call id.</summary>
    public string CallId { get; init; }

    /// <summary>Gets default formatter fallback message.</summary>
    public string DefaultMessage { get; init; }
}

/// <summary>
/// Represents the response from agent approval.
/// </summary>

public sealed record AgentApprovalResponse
{
    /// <summary>Creates an approval response.</summary>
    [JsonConstructor]
    public AgentApprovalResponse(string toolCallId, bool approved)
        : this(toolCallId, approved, null)
    {
    }

    /// <summary>Creates an approval response with reason.</summary>
    public AgentApprovalResponse(string toolCallId, bool approved, string? reason)
    {
        ToolCallId = toolCallId;
        Approved = approved;
        Reason = reason;
    }

    /// <summary>Gets the tool call id being responded to.</summary>
    public string ToolCallId { get; init; }

    /// <summary>Gets whether approval was granted.</summary>
    public bool Approved { get; init; }

    /// <summary>Gets optional reason.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Carries the data contract for AgentPendingApproval.
/// </summary>

public sealed record AgentPendingApproval<TContext>
{
    /// <summary>Creates a pending approval request for a default function tool call.</summary>
    public AgentPendingApproval(Agent<TContext> agent, string toolName, string toolCallId)
        : this(agent, toolName, toolCallId, null, null, "function")
    {
    }

    /// <summary>Creates a pending approval request with arguments.</summary>
    public AgentPendingApproval(Agent<TContext> agent, string toolName, string toolCallId, JsonNode? arguments)
        : this(agent, toolName, toolCallId, arguments, null, "function")
    {
    }

    /// <summary>Creates a pending approval request with arguments and reason.</summary>
    public AgentPendingApproval(Agent<TContext> agent, string toolName, string toolCallId, JsonNode? arguments, string? reason)
        : this(agent, toolName, toolCallId, arguments, reason, "function")
    {
    }

    /// <summary>Creates a pending approval request with explicit metadata.</summary>
    public AgentPendingApproval(
        Agent<TContext> agent,
        string toolName,
        string toolCallId,
        JsonNode? arguments,
        string? reason,
        string toolType)
    {
        Agent = agent;
        ToolName = toolName;
        ToolCallId = toolCallId;
        Arguments = arguments;
        Reason = reason;
        ToolType = toolType;
    }

    /// <summary>Gets the agent executing this call.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the tool name that requires approval.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets the tool call identifier.</summary>
    public string ToolCallId { get; init; }

    /// <summary>Gets arguments associated with the pending approval.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets optional reason for requesting approval.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the tool type string.</summary>
    public string ToolType { get; init; }
}

/// <summary>
/// Carries the data contract for AgentRunState.
/// </summary>

public sealed record AgentRunState<TContext>
{
    /// <summary>Creates run state with no previous response id and no pending approvals.</summary>
    public AgentRunState(string sessionKey, Agent<TContext> currentAgent, IReadOnlyList<AgentConversationItem> conversation, int turnsExecuted)
        : this(sessionKey, currentAgent, conversation, turnsExecuted, null, [])
    {
    }

    /// <summary>Creates run state with a prior response id and no pending approvals.</summary>
    public AgentRunState(string sessionKey, Agent<TContext> currentAgent, IReadOnlyList<AgentConversationItem> conversation, int turnsExecuted, string? lastResponseId)
        : this(sessionKey, currentAgent, conversation, turnsExecuted, lastResponseId, [])
    {
    }

    /// <summary>Creates run state with all fields set.</summary>
    public AgentRunState(
        string sessionKey,
        Agent<TContext> currentAgent,
        IReadOnlyList<AgentConversationItem> conversation,
        int turnsExecuted,
        string? lastResponseId,
        IReadOnlyList<AgentPendingApproval<TContext>> pendingApprovals)
    {
        SessionKey = sessionKey;
        CurrentAgent = currentAgent;
        Conversation = conversation;
        TurnsExecuted = turnsExecuted;
        LastResponseId = lastResponseId;
        PendingApprovals = pendingApprovals;
    }

    /// <summary>Gets the session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the active agent.</summary>
    public Agent<TContext> CurrentAgent { get; init; }

    /// <summary>Gets the conversation history at the point of resumption.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }

    /// <summary>Gets the number of turns already executed.</summary>
    public int TurnsExecuted { get; init; }

    /// <summary>Gets the last response id associated with this state.</summary>
    public string? LastResponseId { get; init; }

    /// <summary>Gets pending tool approvals blocking execution.</summary>
    public IReadOnlyList<AgentPendingApproval<TContext>> PendingApprovals { get; init; }
}

/// <summary>
/// Represents a request for agent approval.
/// </summary>

public sealed record AgentApprovalRequest
{
    /// <summary>Creates an approval request record.</summary>
    public AgentApprovalRequest(string agentName, string toolName, string toolCallId, string? reason, JsonNode? arguments)
    {
        AgentName = agentName;
        ToolName = toolName;
        ToolCallId = toolCallId;
        Reason = reason;
        Arguments = arguments;
    }

    /// <summary>Gets the agent name associated with the approval request.</summary>
    public string AgentName { get; init; }

    /// <summary>Gets the tool name requiring approval.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets the id for the tool call.</summary>
    public string ToolCallId { get; init; }

    /// <summary>Gets the approval reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the arguments for the requested tool call.</summary>
    public JsonNode? Arguments { get; init; }
}

/// <summary>
/// Represents a request for agent run.
/// </summary>

public sealed record AgentRunRequest<TContext>
{
    /// <summary>Creates a default run request from an agent and context.</summary>
    public AgentRunRequest(Agent<TContext> startingAgent, TContext context)
        : this(startingAgent, context, null, null, null, null, null, 8, null, null)
    {
    }

    /// <summary>Creates a run request with explicit run parameters.</summary>
    public AgentRunRequest(
        Agent<TContext> startingAgent,
        TContext context,
        string? userInput,
        IReadOnlyList<AgentConversationItem>? inputItems,
        AgentRunState<TContext>? resumeState,
        IReadOnlyList<AgentApprovalResponse>? approvalResponses,
        string? sessionKey,
        int maxTurns,
        AgentRunOptions<TContext>? options,
        long? expectedSessionVersion)
    {
        StartingAgent = startingAgent;
        Context = context;
        UserInput = userInput;
        InputItems = inputItems;
        ResumeState = resumeState;
        ApprovalResponses = approvalResponses;
        SessionKey = sessionKey;
        MaxTurns = maxTurns;
        Options = options;
        ExpectedSessionVersion = expectedSessionVersion;
    }

    /// <summary>Gets the starting agent.</summary>
    public Agent<TContext> StartingAgent { get; init; }

    /// <summary>Gets the run context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets optional direct user input.</summary>
    public string? UserInput { get; init; }

    /// <summary>Gets explicit input items.</summary>
    public IReadOnlyList<AgentConversationItem>? InputItems { get; init; }

    /// <summary>Gets resume state for continuation.</summary>
    public AgentRunState<TContext>? ResumeState { get; init; }

    /// <summary>Gets responses to pending tool approvals.</summary>
    public IReadOnlyList<AgentApprovalResponse>? ApprovalResponses { get; init; }

    /// <summary>Gets the session key.</summary>
    public string? SessionKey { get; init; }

    /// <summary>Gets maximum turns for this run.</summary>
    public int MaxTurns { get; init; }

    /// <summary>Gets run options.</summary>
    public AgentRunOptions<TContext>? Options { get; init; }

    /// <summary>Gets expected session version.</summary>
    public long? ExpectedSessionVersion { get; init; }

    /// <summary>Creates a request from user input.</summary>
    public static AgentRunRequest<TContext> FromUserInput(
        Agent<TContext> agent,
        string userInput,
        TContext context)
        => FromUserInput(agent, userInput, context, null, 8, null);

    public static AgentRunRequest<TContext> FromUserInput(
        Agent<TContext> agent,
        string userInput,
        TContext context,
        string? sessionKey)
        => FromUserInput(agent, userInput, context, sessionKey, 8, null);

    /// <summary>Creates a request from user input with max turn override.</summary>
    public static AgentRunRequest<TContext> FromUserInput(
        Agent<TContext> agent,
        string userInput,
        TContext context,
        string? sessionKey,
        int maxTurns)
        => FromUserInput(agent, userInput, context, sessionKey, maxTurns, null);

    public static AgentRunRequest<TContext> FromUserInput(
        Agent<TContext> agent,
        string userInput,
        TContext context,
        string? sessionKey,
        int maxTurns,
        AgentRunOptions<TContext>? options)
        => new(agent, context, userInput, null, null, null, sessionKey, maxTurns, options, null);

    /// <summary>Creates a request from stored run state.</summary>
    public static AgentRunRequest<TContext> FromState(
        AgentRunState<TContext> state,
        TContext context)
        => FromState(state, context, null, null, null);

    /// <summary>Creates a request from stored state and approval responses.</summary>
    public static AgentRunRequest<TContext> FromState(
        AgentRunState<TContext> state,
        TContext context,
        IReadOnlyList<AgentApprovalResponse>? approvalResponses)
        => FromState(state, context, approvalResponses, null, null);

    /// <summary>Creates a request from state with optional max turns.</summary>
    public static AgentRunRequest<TContext> FromState(
        AgentRunState<TContext> state,
        TContext context,
        IReadOnlyList<AgentApprovalResponse>? approvalResponses,
        int? maxTurns)
        => FromState(state, context, approvalResponses, maxTurns, null);

    /// <summary>Creates a request from state with options.</summary>
    public static AgentRunRequest<TContext> FromState(
        AgentRunState<TContext> state,
        TContext context,
        IReadOnlyList<AgentApprovalResponse>? approvalResponses,
        int? maxTurns,
        AgentRunOptions<TContext>? options)
        => new(state.CurrentAgent, context, null, null, state, approvalResponses, state.SessionKey, maxTurns ?? Math.Max(state.TurnsExecuted + 1, 8), options, null);

    public static AgentRunRequest<TContext> ResumeApproved(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId)
        => ResumeApproved(state, context, toolCallId, null, null);

    /// <summary>Creates a request to resume an approved tool call with max turns.</summary>
    public static AgentRunRequest<TContext> ResumeApproved(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId,
        int? maxTurns)
        => ResumeApproved(state, context, toolCallId, maxTurns, null);

    /// <summary>Creates a request to resume an approved tool call with options.</summary>
    public static AgentRunRequest<TContext> ResumeApproved(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId,
        int? maxTurns,
        AgentRunOptions<TContext>? options)
        => FromState(state, context, [new AgentApprovalResponse(toolCallId, true)], maxTurns, options);

    public static AgentRunRequest<TContext> ResumeRejected(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId)
        => ResumeRejected(state, context, toolCallId, null, null, null);

    /// <summary>Creates a request to resume a rejected tool call.</summary>
    public static AgentRunRequest<TContext> ResumeRejected(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId,
        string? reason)
        => ResumeRejected(state, context, toolCallId, reason, null, null);

    /// <summary>Creates a request to resume a rejected tool call with max turns.</summary>
    public static AgentRunRequest<TContext> ResumeRejected(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId,
        string? reason,
        int? maxTurns)
        => ResumeRejected(state, context, toolCallId, reason, maxTurns, null);

    /// <summary>Creates a request to resume a rejected tool call with options.</summary>
    public static AgentRunRequest<TContext> ResumeRejected(
        AgentRunState<TContext> state,
        TContext context,
        string toolCallId,
        string? reason,
        int? maxTurns,
        AgentRunOptions<TContext>? options)
        => FromState(state, context, [new AgentApprovalResponse(toolCallId, false, reason)], maxTurns, options);

    /// <summary>Returns a new run request with an updated session key.</summary>
    public AgentRunRequest<TContext> WithSession(string sessionKey)
        => this with { SessionKey = sessionKey };

    /// <summary>Returns a new run request with updated previous response id.</summary>
    public AgentRunRequest<TContext> WithPreviousResponse(string? previousResponseId)
    {
        AgentRunOptions<TContext> options = (Options ?? new AgentRunOptions<TContext>()) with
        {
            PreviousResponseId = previousResponseId,
        };

        return this with { Options = options };
    }

    /// <summary>Returns a new run request with expected session version.</summary>
    public AgentRunRequest<TContext> WithExpectedSessionVersion(long? expectedSessionVersion)
        => this with { ExpectedSessionVersion = expectedSessionVersion };
}

/// <summary>
/// Carries the data contract for AgentToolCall.
/// </summary>

public sealed record AgentToolCall<TContext>
{
    /// <summary>Creates a tool call for a default function.</summary>
    public AgentToolCall(string callId, string toolName)
        : this(callId, toolName, null, false, null, "function")
    {
    }

    /// <summary>Creates a tool call with arguments.</summary>
    public AgentToolCall(string callId, string toolName, JsonNode? arguments)
        : this(callId, toolName, arguments, false, null, "function")
    {
    }

    /// <summary>Creates a tool call with an approval requirement.</summary>
    public AgentToolCall(string callId, string toolName, JsonNode? arguments, bool requiresApproval)
        : this(callId, toolName, arguments, requiresApproval, null, "function")
    {
    }

    /// <summary>Creates a tool call with approval reason.</summary>
    public AgentToolCall(string callId, string toolName, JsonNode? arguments, bool requiresApproval, string? approvalReason)
        : this(callId, toolName, arguments, requiresApproval, approvalReason, "function")
    {
    }

    /// <summary>Creates a tool call with explicit metadata.</summary>
    public AgentToolCall(
        string callId,
        string toolName,
        JsonNode? arguments,
        bool requiresApproval,
        string? approvalReason,
        string toolType)
    {
        CallId = callId;
        ToolName = toolName;
        Arguments = arguments;
        RequiresApproval = requiresApproval;
        ApprovalReason = approvalReason;
        ToolType = toolType;
    }

    /// <summary>Gets the call identifier.</summary>
    public string CallId { get; init; }

    /// <summary>Gets the tool name.</summary>
    public string ToolName { get; init; }

    /// <summary>Gets arguments for the tool call.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets whether approval is required.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>Gets the approval reason, if any.</summary>
    public string? ApprovalReason { get; init; }

    /// <summary>Gets the tool type.</summary>
    public string ToolType { get; init; }
}

/// <summary>
/// Represents a request for agent handoff.
/// </summary>

public sealed record AgentHandoffRequest<TContext>
{
    /// <summary>Creates a handoff request with defaults.</summary>
    public AgentHandoffRequest(string handoffName, Agent<TContext> targetAgent)
        : this(handoffName, targetAgent, null, null)
    {
    }

    /// <summary>Creates a handoff request with arguments.</summary>
    public AgentHandoffRequest(string handoffName, Agent<TContext> targetAgent, JsonNode? arguments)
        : this(handoffName, targetAgent, arguments, null)
    {
    }

    /// <summary>Creates a handoff request with reason.</summary>
    public AgentHandoffRequest(string handoffName, Agent<TContext> targetAgent, JsonNode? arguments, string? reason)
    {
        HandoffName = handoffName;
        TargetAgent = targetAgent;
        Arguments = arguments;
        Reason = reason;
    }

    /// <summary>Gets the handoff name.</summary>
    public string HandoffName { get; init; }

    /// <summary>Gets the target agent.</summary>
    public Agent<TContext> TargetAgent { get; init; }

    /// <summary>Gets handoff arguments.</summary>
    public JsonNode? Arguments { get; init; }

    /// <summary>Gets handoff reason.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Represents a request for agent turn.
/// </summary>

public sealed record AgentTurnRequest<TContext>
{
    /// <summary>Creates a turn request with default options.</summary>
    public AgentTurnRequest(Agent<TContext> agent, TContext context, string sessionKey, int turnNumber, IReadOnlyList<AgentConversationItem> conversation)
        : this(agent, context, sessionKey, turnNumber, conversation, null, null, null)
    {
    }

    /// <summary>Creates a turn request with full parameters.</summary>
    public AgentTurnRequest(
        Agent<TContext> agent,
        TContext context,
        string sessionKey,
        int turnNumber,
        IReadOnlyList<AgentConversationItem> conversation,
        string? userInput,
        string? previousResponseId,
        AgentRunOptions<TContext>? options)
    {
        Agent = agent;
        Context = context;
        SessionKey = sessionKey;
        TurnNumber = turnNumber;
        Conversation = conversation;
        UserInput = userInput;
        PreviousResponseId = previousResponseId;
        Options = options;
    }

    /// <summary>Gets the request agent.</summary>
    public Agent<TContext> Agent { get; init; }

    /// <summary>Gets the request context.</summary>
    public TContext Context { get; init; }

    /// <summary>Gets the session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the turn number.</summary>
    public int TurnNumber { get; init; }

    /// <summary>Gets the conversation to execute.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }

    /// <summary>Gets optional user input.</summary>
    public string? UserInput { get; init; }

    /// <summary>Gets optional previous response id.</summary>
    public string? PreviousResponseId { get; init; }

    /// <summary>Gets run options for this turn.</summary>
    public AgentRunOptions<TContext>? Options { get; init; }
}

/// <summary>
/// Represents the response from agent turn.
/// </summary>

public sealed record AgentTurnResponse<TContext>
{
    /// <summary>Creates an empty turn response.</summary>
    public AgentTurnResponse()
        : this(null, [], [], [], null, null)
    {
    }

    /// <summary>Creates a turn response with explicit values.</summary>
    public AgentTurnResponse(
        AgentFinalOutput? finalOutput,
        IReadOnlyList<AgentToolCall<TContext>> toolCalls,
        IReadOnlyList<AgentHandoffRequest<TContext>> handoffs,
        IReadOnlyList<AgentRunItem>? items,
        string? responseId,
        Agent<TContext>? effectiveAgent)
    {
        FinalOutput = finalOutput;
        ToolCalls = toolCalls;
        Handoffs = handoffs;
        Items = items;
        ResponseId = responseId;
        EffectiveAgent = effectiveAgent;
    }

    /// <summary>Gets the final output.</summary>
    public AgentFinalOutput? FinalOutput { get; init; }

    /// <summary>Gets the tool calls.</summary>
    public IReadOnlyList<AgentToolCall<TContext>> ToolCalls { get; init; }

    /// <summary>Gets the handoff requests.</summary>
    public IReadOnlyList<AgentHandoffRequest<TContext>> Handoffs { get; init; }

    /// <summary>Gets emitted run items.</summary>
    public IReadOnlyList<AgentRunItem>? Items { get; init; }

    /// <summary>Gets response id.</summary>
    public string? ResponseId { get; init; }

    /// <summary>Gets effective agent for output.</summary>
    public Agent<TContext>? EffectiveAgent { get; init; }

    /// <summary>Gets an empty default response value.</summary>
    public static AgentTurnResponse<TContext> Empty => new(null, [], [], [], null, null);
}

/// <summary>
/// Represents the result of agent run.
/// </summary>

public sealed record AgentRunResult<TContext>
{
    /// <summary>Creates a run result with full execution values.</summary>
    public AgentRunResult(
        AgentRunStatus status,
        string sessionKey,
        Agent<TContext> finalAgent,
        IReadOnlyList<AgentRunItem> items,
        IReadOnlyList<AgentConversationItem> conversation,
        int turnsExecuted,
        AgentFinalOutput? finalOutput,
        AgentApprovalRequest? approvalRequest,
        string? guardrailMessage,
        string? responseId,
        AgentRunState<TContext>? state)
    {
        Status = status;
        SessionKey = sessionKey;
        FinalAgent = finalAgent;
        Items = items;
        Conversation = conversation;
        TurnsExecuted = turnsExecuted;
        FinalOutput = finalOutput;
        ApprovalRequest = approvalRequest;
        GuardrailMessage = guardrailMessage;
        ResponseId = responseId;
        State = state;
    }

    /// <summary>Gets run status.</summary>
    public AgentRunStatus Status { get; init; }

    /// <summary>Gets session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets final agent.</summary>
    public Agent<TContext> FinalAgent { get; init; }

    /// <summary>Gets all run items.</summary>
    public IReadOnlyList<AgentRunItem> Items { get; init; }

    /// <summary>Gets conversation at completion.</summary>
    public IReadOnlyList<AgentConversationItem> Conversation { get; init; }

    /// <summary>Gets number of turns executed.</summary>
    public int TurnsExecuted { get; init; }

    /// <summary>Gets final output.</summary>
    public AgentFinalOutput? FinalOutput { get; init; }

    /// <summary>Gets the active approval request when applicable.</summary>
    public AgentApprovalRequest? ApprovalRequest { get; init; }

    /// <summary>Gets guardrail message when triggered.</summary>
    public string? GuardrailMessage { get; init; }

    /// <summary>Gets response id.</summary>
    public string? ResponseId { get; init; }

    /// <summary>Gets saved state for resumability.</summary>
    public AgentRunState<TContext>? State { get; init; }

    /// <summary>Gets whether run is waiting on approval.</summary>
    public bool RequiresApproval => Status == AgentRunStatus.ApprovalRequired;

    /// <summary>Gets whether final output was produced.</summary>
    public bool HasFinalOutput => FinalOutput is not null;

    /// <summary>Gets whether execution was interrupted.</summary>
    public bool IsInterrupted => Status is AgentRunStatus.ApprovalRequired or AgentRunStatus.GuardrailTriggered or AgentRunStatus.MaxTurnsExceeded or AgentRunStatus.Cancelled;
}

/// <summary>
/// Represents an agent stream event.
/// </summary>

public sealed record AgentStreamEvent
{
    /// <summary>Creates a stream event with required values.</summary>
    public AgentStreamEvent(string eventType, string agentName)
        : this(eventType, agentName, null, null, null, null)
    {
    }

    /// <summary>Creates a stream event with all values.</summary>
    public AgentStreamEvent(
        string eventType,
        string agentName,
        AgentRunItem? item,
        JsonNode? data,
        string? message,
        DateTimeOffset? timestampUtc)
    {
        EventType = eventType;
        AgentName = agentName;
        Item = item;
        Data = data;
        Message = message;
        TimestampUtc = timestampUtc;
    }

    /// <summary>Gets the event type.</summary>
    public string EventType { get; init; }

    /// <summary>Gets the agent name.</summary>
    public string AgentName { get; init; }

    /// <summary>Gets the associated run item.</summary>
    public AgentRunItem? Item { get; init; }

    /// <summary>Gets event data.</summary>
    public JsonNode? Data { get; init; }

    /// <summary>Gets event message.</summary>
    public string? Message { get; init; }

    /// <summary>Gets event timestamp.</summary>
    public DateTimeOffset? TimestampUtc { get; init; }
}
