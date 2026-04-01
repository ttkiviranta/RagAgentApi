using FluentAssertions;
using RagAgentApi.Models;

namespace RagAgentApi.Tests.Models;

/// <summary>
/// Unit tests for AgentContext model
/// </summary>
public class AgentContextTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var context = new AgentContext();

        // Assert
        context.ThreadId.Should().NotBeNullOrEmpty();
        context.State.Should().NotBeNull();
        context.State.Should().BeEmpty();
        context.Messages.Should().NotBeNull();
        context.Messages.Should().BeEmpty();
        context.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        context.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_ThreadIdShouldBeValidGuid()
    {
        // Act
        var context = new AgentContext();

        // Assert
        Guid.TryParse(context.ThreadId, out _).Should().BeTrue();
    }

    #endregion

    #region State Tests

    [Fact]
    public void State_ShouldAcceptVariousTypes()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        context.State["string"] = "test";
        context.State["int"] = 42;
        context.State["bool"] = true;
        context.State["list"] = new List<string> { "a", "b" };
        context.State["dict"] = new Dictionary<string, int> { { "key", 1 } };

        // Assert
        context.State["string"].Should().Be("test");
        context.State["int"].Should().Be(42);
        context.State["bool"].Should().Be(true);
        context.State["list"].Should().BeOfType<List<string>>();
        context.State["dict"].Should().BeOfType<Dictionary<string, int>>();
    }

    [Fact]
    public void State_ShouldBeModifiable()
    {
        // Arrange
        var context = new AgentContext();
        context.State["key"] = "initial";

        // Act
        context.State["key"] = "modified";

        // Assert
        context.State["key"].Should().Be("modified");
    }

    [Fact]
    public void State_ShouldSupportRemoval()
    {
        // Arrange
        var context = new AgentContext();
        context.State["key"] = "value";

        // Act
        context.State.Remove("key");

        // Assert
        context.State.Should().NotContainKey("key");
    }

    #endregion

    #region Messages Tests

    [Fact]
    public void Messages_ShouldAcceptNewMessages()
    {
        // Arrange
        var context = new AgentContext();
        var message = new AgentMessage
        {
            From = "Agent1",
            To = "Agent2",
            Content = "Test message"
        };

        // Act
        context.Messages.Add(message);

        // Assert
        context.Messages.Should().HaveCount(1);
        context.Messages[0].From.Should().Be("Agent1");
    }

    [Fact]
    public void Messages_ShouldMaintainOrder()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        context.Messages.Add(new AgentMessage { Content = "First" });
        context.Messages.Add(new AgentMessage { Content = "Second" });
        context.Messages.Add(new AgentMessage { Content = "Third" });

        // Assert
        context.Messages[0].Content.Should().Be("First");
        context.Messages[1].Content.Should().Be("Second");
        context.Messages[2].Content.Should().Be("Third");
    }

    #endregion

    #region Timestamp Tests

    [Fact]
    public void UpdatedAt_ShouldBeSettable()
    {
        // Arrange
        var context = new AgentContext();
        var newTime = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        context.UpdatedAt = newTime;

        // Assert
        context.UpdatedAt.Should().Be(newTime);
    }

    [Fact]
    public void CreatedAt_ShouldRemainConstant()
    {
        // Arrange
        var context = new AgentContext();
        var createdAt = context.CreatedAt;
        
        // Act - simulate some operations
        context.State["key"] = "value";
        context.Messages.Add(new AgentMessage { Content = "test" });
        context.UpdatedAt = DateTimeOffset.UtcNow;

        // Assert
        context.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region ThreadId Tests

    [Fact]
    public void ThreadId_ShouldBeSettable()
    {
        // Arrange
        var context = new AgentContext();
        var customId = "custom-thread-id";

        // Act
        context.ThreadId = customId;

        // Assert
        context.ThreadId.Should().Be(customId);
    }

    [Fact]
    public void ThreadId_MultipleContextsShouldHaveUniqueIds()
    {
        // Arrange & Act
        var context1 = new AgentContext();
        var context2 = new AgentContext();
        var context3 = new AgentContext();

        // Assert
        context1.ThreadId.Should().NotBe(context2.ThreadId);
        context2.ThreadId.Should().NotBe(context3.ThreadId);
        context1.ThreadId.Should().NotBe(context3.ThreadId);
    }

    #endregion
}
