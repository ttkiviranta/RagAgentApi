using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RagAgentApi.Agents;
using RagAgentApi.Services;

namespace RagAgentApi.Tests.Services;

/// <summary>
/// Unit tests for AgentFactory
/// </summary>
public class AgentFactoryTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<AgentFactory>> _loggerMock;

    public AgentFactoryTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<AgentFactory>>();
    }

    [Fact]
    public void CreateAgent_WithValidAgentName_ShouldReturnAgent()
    {
        // Arrange
        var mockAgent = CreateMockAgent<ChunkerAgent>();
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(ChunkerAgent)))
            .Returns(mockAgent);

        var factory = new AgentFactory(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var agent = factory.CreateAgent("ChunkerAgent");

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<ChunkerAgent>();
    }

    [Fact]
    public void CreateAgent_WithCaseInsensitiveName_ShouldReturnAgent()
    {
        // Arrange
        var mockAgent = CreateMockAgent<ChunkerAgent>();
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(ChunkerAgent)))
            .Returns(mockAgent);

        var factory = new AgentFactory(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var agent = factory.CreateAgent("chunkeragent");

        // Assert
        agent.Should().NotBeNull();
    }

    [Fact]
    public void CreateAgent_WithUnknownAgentName_ShouldThrowArgumentException()
    {
        // Arrange
        var factory = new AgentFactory(_serviceProviderMock.Object, _loggerMock.Object);

        // Act
        var act = () => factory.CreateAgent("UnknownAgent");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown agent type*");
    }

    [Theory]
    [InlineData("ScraperAgent")]
    [InlineData("ChunkerAgent")]
    [InlineData("EmbeddingAgent")]
    [InlineData("QueryAgent")]
    [InlineData("PostgresStorageAgent")]
    [InlineData("PostgresQueryAgent")]
    [InlineData("GitHubApiAgent")]
    public void CreateAgent_ShouldSupportAllRegisteredAgents(string agentName)
    {
        // Arrange
        var factory = new AgentFactory(_serviceProviderMock.Object, _loggerMock.Object);

        // Act & Assert - Just verify no exception for unknown type
        // The actual creation would fail because mock doesn't return agents
        try
        {
            factory.CreateAgent(agentName);
        }
        catch (InvalidOperationException)
        {
            // Expected when service provider doesn't return the agent
        }
        catch (ArgumentException ex)
        {
            // Should not throw for registered agents
            Assert.Fail($"Agent {agentName} should be registered but got: {ex.Message}");
        }
    }

    private T CreateMockAgent<T>() where T : BaseRagAgent
    {
        var loggerMock = new Mock<ILogger<T>>();
        var configurationMock = new Mock<IConfiguration>();
        var errorLogServiceMock = new Mock<IErrorLogService>();

        // Use Activator to create instance with required dependencies
        if (typeof(T) == typeof(ChunkerAgent))
        {
            var chunkerLogger = new Mock<ILogger<ChunkerAgent>>();
            return (T)(object)new ChunkerAgent(
                configurationMock.Object, 
                chunkerLogger.Object, 
                errorLogServiceMock.Object);
        }

        throw new NotSupportedException($"Mock creation not supported for {typeof(T).Name}");
    }
}
