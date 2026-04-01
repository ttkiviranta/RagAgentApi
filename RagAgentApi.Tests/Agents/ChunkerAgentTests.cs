using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RagAgentApi.Agents;
using RagAgentApi.Models;
using RagAgentApi.Services;

namespace RagAgentApi.Tests.Agents;

/// <summary>
/// Unit tests for ChunkerAgent
/// </summary>
public class ChunkerAgentTests
{
    private readonly Mock<ILogger<ChunkerAgent>> _loggerMock;
    private readonly Mock<IErrorLogService> _errorLogServiceMock;
    private readonly IConfiguration _configuration;

    public ChunkerAgentTests()
    {
        _loggerMock = new Mock<ILogger<ChunkerAgent>>();
        _errorLogServiceMock = new Mock<IErrorLogService>();

        var configData = new Dictionary<string, string?>
        {
            { "RagSettings:DefaultChunkSize", "1000" },
            { "RagSettings:DefaultChunkOverlap", "200" },
            { "RagSettings:MinChunkSize", "100" },
            { "RagSettings:MaxChunkSize", "5000" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private ChunkerAgent CreateAgent()
    {
        return new ChunkerAgent(_configuration, _loggerMock.Object, _errorLogServiceMock.Object);
    }

    private AgentContext CreateContextWithContent(string content)
    {
        var context = new AgentContext { ThreadId = Guid.NewGuid().ToString() };
        context.State["raw_content"] = content;
        return context;
    }

    #region Name Tests

    [Fact]
    public void Name_ShouldReturnChunkerAgent()
    {
        // Arrange
        var agent = CreateAgent();

        // Assert
        agent.Name.Should().Be("ChunkerAgent");
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithValidContent_ShouldReturnSuccess()
    {
        // Arrange
        var agent = CreateAgent();
        var content = GenerateLongContent(500);
        var context = CreateContextWithContent(content);

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidContent_ShouldCreateChunks()
    {
        // Arrange
        var agent = CreateAgent();
        var content = GenerateLongContent(2500);
        var context = CreateContextWithContent(content);

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        context.State.Should().ContainKey("chunks");
        var chunks = context.State["chunks"] as List<string>;
        chunks.Should().NotBeNull();
        chunks!.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutContent_ShouldReturnFailure()
    {
        // Arrange
        var agent = CreateAgent();
        var context = new AgentContext { ThreadId = Guid.NewGuid().ToString() };

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithShortContent_ShouldCreateSingleChunk()
    {
        // Arrange
        var agent = CreateAgent();
        var content = "This is a short content.";
        var context = CreateContextWithContent(content);

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        var chunks = context.State["chunks"] as List<string>;
        chunks.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreChunkParameters()
    {
        // Arrange
        var agent = CreateAgent();
        var context = CreateContextWithContent("Test content.");

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        context.State.Should().ContainKey("chunk_size");
        context.State.Should().ContainKey("chunk_overlap");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomChunkSize_ShouldUseProvidedSize()
    {
        // Arrange
        var agent = CreateAgent();
        var context = CreateContextWithContent(GenerateLongContent(3000));
        context.State["chunk_size"] = 500;
        context.State["chunk_overlap"] = 100;

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        context.State["chunk_size"].Should().Be(500);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddMessageToContext()
    {
        // Arrange
        var agent = CreateAgent();
        var context = CreateContextWithContent("Test content for message.");

        // Act
        await agent.ExecuteAsync(context);

        // Assert
        context.Messages.Should().NotBeEmpty();
        var lastMessage = context.Messages.Last();
        lastMessage.From.Should().Be("ChunkerAgent");
        lastMessage.To.Should().Be("EmbeddingAgent");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyContent_ShouldReturnFailure()
    {
        // Arrange
        var agent = CreateAgent();
        var context = CreateContextWithContent("");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ResultShouldContainChunkCount()
    {
        // Arrange
        var agent = CreateAgent();
        var context = CreateContextWithContent(GenerateLongContent(2000));

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Data.Should().ContainKey("chunk_count");
        ((int)result.Data!["chunk_count"]).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidChunkOverlap_ShouldReturnFailure()
    {
        // Arrange
        var agent = CreateAgent();
        var context = CreateContextWithContent("Test content");
        context.State["chunk_size"] = 500;
        context.State["chunk_overlap"] = 300; // Greater than half of chunk_size

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid");
    }

    #endregion

    #region Helper Methods

    private string GenerateLongContent(int approximateLength)
    {
        var sentences = new[]
        {
            "Machine learning is a subset of artificial intelligence.",
            "Deep learning uses neural networks with many layers.",
            "Natural language processing enables computers to understand text.",
            "Computer vision allows machines to interpret visual information.",
            "Reinforcement learning involves agents learning from rewards.",
            "Supervised learning uses labeled data for training.",
            "Unsupervised learning finds patterns in unlabeled data.",
            "Transfer learning applies knowledge from one task to another."
        };

        var result = new System.Text.StringBuilder();
        var random = new Random(42); // Fixed seed for reproducibility

        while (result.Length < approximateLength)
        {
            result.Append(sentences[random.Next(sentences.Length)]);
            result.Append(" ");
        }

        return result.ToString().Trim();
    }

    #endregion
}
