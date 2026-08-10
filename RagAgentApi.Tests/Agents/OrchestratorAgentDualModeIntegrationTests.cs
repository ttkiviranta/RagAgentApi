using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RagAgentApi.Agents;
using RagAgentApi.Models;
using RagAgentApi.Options;
using RagAgentApi.Services;
using RagAgentApi.Services.Orchestration;

namespace RagAgentApi.Tests.Agents;

/// <summary>
/// Integration tests for configuration-driven orchestration mode selection.
/// </summary>
public class OrchestratorAgentDualModeIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenModeIsPipeline_ShouldUsePipelineOrchestrator()
    {
        // Arrange
        var services = CreateServices(new Dictionary<string, string?>
        {
            ["Orchestration:Mode"] = "Pipeline"
        });

        var provider = services.BuildServiceProvider();
        var orchestratorAgent = provider.GetRequiredService<OrchestratorAgent>();
        var pipelineMock = provider.GetRequiredService<Mock<IPipelineOrchestrator>>();
        var llmMock = provider.GetRequiredService<Mock<ILlmOrchestrator>>();

        var context = new AgentContext
        {
            ThreadId = Guid.NewGuid().ToString(),
            State = new Dictionary<string, object> { ["url"] = "https://example.com" }
        };

        // Act
        var result = await orchestratorAgent.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("pipeline");
        pipelineMock.Verify(x => x.ExecuteAsync(context, It.IsAny<CancellationToken>()), Times.Once);
        llmMock.Verify(x => x.ExecuteAsync(It.IsAny<AgentContext>(), It.IsAny<IReadOnlyDictionary<string, AgentMetadata>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenModeIsLlm_ShouldUseLlmOrchestrator()
    {
        // Arrange
        var services = CreateServices(new Dictionary<string, string?>
        {
            ["Orchestration:Mode"] = "LLM"
        });

        var provider = services.BuildServiceProvider();
        var orchestratorAgent = provider.GetRequiredService<OrchestratorAgent>();
        var pipelineMock = provider.GetRequiredService<Mock<IPipelineOrchestrator>>();
        var llmMock = provider.GetRequiredService<Mock<ILlmOrchestrator>>();

        var context = new AgentContext
        {
            ThreadId = Guid.NewGuid().ToString(),
            State = new Dictionary<string, object> { ["url"] = "https://example.com" }
        };

        // Act
        var result = await orchestratorAgent.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("llm");
        llmMock.Verify(x => x.ExecuteAsync(context, It.IsAny<IReadOnlyDictionary<string, AgentMetadata>>(), It.IsAny<CancellationToken>()), Times.Once);
        pipelineMock.Verify(x => x.ExecuteAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ServiceCollection CreateServices(Dictionary<string, string?> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var pipelineMock = new Mock<IPipelineOrchestrator>();
        pipelineMock
            .Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.CreateSuccess("pipeline"));

        var llmMock = new Mock<ILlmOrchestrator>();
        llmMock
            .Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>(), It.IsAny<IReadOnlyDictionary<string, AgentMetadata>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.CreateSuccess("llm"));

        var metadata = AgentMetadataDefinitions.Create();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<OrchestrationOptions>(configuration.GetSection("Orchestration"));

        services.AddSingleton(pipelineMock);
        services.AddSingleton(llmMock);
        services.AddSingleton<IReadOnlyDictionary<string, AgentMetadata>>(metadata);
        services.AddSingleton<IPipelineOrchestrator>(sp => sp.GetRequiredService<Mock<IPipelineOrchestrator>>().Object);
        services.AddSingleton<ILlmOrchestrator>(sp => sp.GetRequiredService<Mock<ILlmOrchestrator>>().Object);

        services.AddLogging();
        services.AddSingleton<OrchestratorAgent>();

        return services;
    }
}
