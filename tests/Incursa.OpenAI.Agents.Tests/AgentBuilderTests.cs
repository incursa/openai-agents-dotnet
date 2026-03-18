using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using Incursa.OpenAI.Agents.Mcp;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for agent composition helpers and request factory APIs.</summary>
public sealed class AgentBuilderTests
{
    /// <summary>AgentBuilder preserves the configured composition surface when building an agent.</summary>
    /// <intent>Protect the low-ceremony composition API used by consumer code.</intent>
    /// <scenario>LIB-COMP-BUILDER-001</scenario>
    /// <behavior>Builder output keeps model, instructions, tools, handoffs, MCP definitions, metadata, model settings, and output contracts.</behavior>
    [Trait("Category", "Smoke")]
    [Fact]
    public void AgentBuilder_CreatesConfiguredAgent()
    {
        Agent<TestContext> specialist = AgentBuilder
            .Create<TestContext>("mail specialist")
            .WithModel("gpt-5.4")
            .WithInstructions("Handle mail")
            .Build();

        Agent<TestContext> agent = AgentBuilder
            .Create<TestContext>("triage")
            .WithModel("gpt-5.4")
            .WithInstructions("Route work")
            .AddTool("lookup_customer", (_, _) => ValueTask.FromResult(AgentToolResult.FromText("ok")), "Lookup a customer")
            .AddHandoff("mail", specialist, "Delegate mail work.")
            .WithHostedMcpTool(new HostedMcpToolDefinition("mail")
            {
                ServerUrl = new Uri("https://mail.example.test/mcp"),
            })
            .WithStreamableMcpServer(new StreamableHttpMcpServerDefinition("mail", new Uri("https://mail.example.test/mcp")))
            .WithMetadata("team", "support")
            .WithModelSetting("temperature", 0.2)
            .WithOutputContract(AgentOutputContract.For<ExampleOutput>())
            .Build();

        Assert.Equal("triage", agent.Name);
        Assert.Equal("gpt-5.4", agent.Model);
        Assert.Single(agent.Tools);
        Assert.Single(agent.Handoffs);
        Assert.Single(agent.HostedMcpTools);
        Assert.Single(agent.StreamableMcpServers);
        Assert.Equal("support", agent.Metadata["team"]);
        Assert.Equal(0.2, agent.ModelSettings["temperature"]);
        Assert.Equal(typeof(ExampleOutput), agent.OutputContract?.ClrType);
    }

    /// <summary>AgentRunRequest helper methods preserve resume and continuation metadata.</summary>
    /// <intent>Keep request helper ergonomics aligned with the underlying runtime contracts.</intent>
    /// <scenario>LIB-COMP-REQUEST-001</scenario>
    /// <behavior>Helper APIs carry session, previous-response, and approval-resume data into the resulting request.</behavior>
    [Fact]
    public void AgentRunRequest_HelpersCreateResumeAndPreviousResponseRequests()
    {
        Agent<TestContext> agent = AgentBuilder.Create<TestContext>("triage").Build();
        AgentRunState<TestContext> state = new(
            "session-1",
            agent,
            [new AgentConversationItem(AgentItemTypes.UserInput, "user", "triage") { Text = "hello" }],
            1,
            "resp-1",
            [new AgentPendingApproval<TestContext>(agent, "send_mail", "call-1", new JsonObject())]);

        AgentRunRequest<TestContext> request = AgentRunRequest<TestContext>
            .FromUserInput(agent, "hello", new TestContext())
            .WithSession("session-1")
            .WithPreviousResponse("resp-2");

        AgentRunRequest<TestContext> resumedApproved = AgentRunRequest<TestContext>.ResumeApproved(state, new TestContext(), "call-1");
        AgentRunRequest<TestContext> resumedRejected = AgentRunRequest<TestContext>.ResumeRejected(state, new TestContext(), "call-1", "denied");

        Assert.Equal("session-1", request.SessionKey);
        Assert.Equal("resp-2", request.Options?.PreviousResponseId);
        Assert.True(Assert.Single(resumedApproved.ApprovalResponses!).Approved);
        Assert.False(Assert.Single(resumedRejected.ApprovalResponses!).Approved);
        Assert.Equal("denied", Assert.Single(resumedRejected.ApprovalResponses!).Reason);
    }

    private sealed record TestContext
    {
        public TestContext()
            : this("user-1")
        {
        }

        public TestContext(string userId)
        {
            UserId = userId;
        }

        public string UserId { get; init; }
    }

    private sealed record ExampleOutput
    {
        public ExampleOutput(string value)
        {
            Value = value;
        }

        public string Value { get; init; }
    }
}
