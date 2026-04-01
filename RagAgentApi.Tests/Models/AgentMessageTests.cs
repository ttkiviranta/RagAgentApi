using FluentAssertions;
using RagAgentApi.Models;

namespace RagAgentApi.Tests.Models;

/// <summary>
/// Unit tests for AgentMessage model
/// </summary>
public class AgentMessageTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var message = new AgentMessage();

        // Assert
        message.From.Should().BeEmpty();
        message.To.Should().BeEmpty();
        message.Content.Should().BeEmpty();
        message.Data.Should().BeNull();
        message.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Property Tests

    [Fact]
    public void From_ShouldBeSettable()
    {
        // Arrange
        var message = new AgentMessage();

        // Act
        message.From = "ChunkerAgent";

        // Assert
        message.From.Should().Be("ChunkerAgent");
    }

    [Fact]
    public void To_ShouldBeSettable()
    {
        // Arrange
        var message = new AgentMessage();

        // Act
        message.To = "EmbeddingAgent";

        // Assert
        message.To.Should().Be("EmbeddingAgent");
    }

    [Fact]
    public void Content_ShouldBeSettable()
    {
        // Arrange
        var message = new AgentMessage();
        var content = "This is a test message with some content.";

        // Act
        message.Content = content;

        // Assert
        message.Content.Should().Be(content);
    }

    [Fact]
    public void Data_ShouldAcceptDictionary()
    {
        // Arrange
        var message = new AgentMessage();
        var data = new Dictionary<string, object>
        {
            { "chunk_count", 5 },
            { "status", "completed" },
            { "metadata", new { source = "web" } }
        };

        // Act
        message.Data = data;

        // Assert
        message.Data.Should().NotBeNull();
        message.Data!["chunk_count"].Should().Be(5);
        message.Data["status"].Should().Be("completed");
    }

    [Fact]
    public void Timestamp_ShouldBeSettable()
    {
        // Arrange
        var message = new AgentMessage();
        var customTime = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        message.Timestamp = customTime;

        // Assert
        message.Timestamp.Should().Be(customTime);
    }

    #endregion

    #region Complete Message Tests

    [Fact]
    public void Message_ShouldSupportFullInitialization()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var data = new Dictionary<string, object> { { "key", "value" } };

        // Act
        var message = new AgentMessage
        {
            From = "ScraperAgent",
            To = "ChunkerAgent",
            Content = "Scraped 1500 characters from URL",
            Data = data,
            Timestamp = timestamp
        };

        // Assert
        message.From.Should().Be("ScraperAgent");
        message.To.Should().Be("ChunkerAgent");
        message.Content.Should().Contain("1500");
        message.Data.Should().ContainKey("key");
        message.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void MultipleMessages_ShouldHaveUniqueTimestamps()
    {
        // Arrange & Act (with small delay)
        var message1 = new AgentMessage { Content = "First" };
        Thread.Sleep(10);
        var message2 = new AgentMessage { Content = "Second" };

        // Assert
        message2.Timestamp.Should().BeOnOrAfter(message1.Timestamp);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Content_ShouldHandleLongContent()
    {
        // Arrange
        var message = new AgentMessage();
        var longContent = new string('x', 10000);

        // Act
        message.Content = longContent;

        // Assert
        message.Content.Should().HaveLength(10000);
    }

    [Fact]
    public void Data_ShouldHandleComplexNestedStructure()
    {
        // Arrange
        var message = new AgentMessage
        {
            Data = new Dictionary<string, object>
            {
                { "level1", new Dictionary<string, object>
                    {
                        { "level2", new Dictionary<string, object>
                            {
                                { "level3", "deep value" }
                            }
                        }
                    }
                }
            }
        };

        // Assert
        message.Data.Should().ContainKey("level1");
    }

    [Fact]
    public void From_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var message = new AgentMessage();

        // Act
        message.From = "Agent-1_Special.Name";

        // Assert
        message.From.Should().Be("Agent-1_Special.Name");
    }

    #endregion
}
