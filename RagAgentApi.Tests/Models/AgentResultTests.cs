using FluentAssertions;
using RagAgentApi.Models;

namespace RagAgentApi.Tests.Models;

/// <summary>
/// Unit tests for AgentResult model
/// </summary>
public class AgentResultTests
{
    #region CreateSuccess Tests

    [Fact]
    public void CreateSuccess_ShouldSetSuccessToTrue()
    {
        // Act
        var result = AgentResult.CreateSuccess("Test message");

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void CreateSuccess_ShouldSetMessage()
    {
        // Arrange
        var message = "Operation completed successfully";

        // Act
        var result = AgentResult.CreateSuccess(message);

        // Assert
        result.Message.Should().Be(message);
    }

    [Fact]
    public void CreateSuccess_WithData_ShouldSetData()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            { "count", 42 },
            { "name", "test" }
        };

        // Act
        var result = AgentResult.CreateSuccess("Success", data);

        // Assert
        result.Data.Should().NotBeNull();
        result.Data!["count"].Should().Be(42);
        result.Data["name"].Should().Be("test");
    }

    [Fact]
    public void CreateSuccess_WithoutData_ShouldCreateEmptyDataDictionary()
    {
        // Act
        var result = AgentResult.CreateSuccess("Success");

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public void CreateSuccess_ErrorsShouldBeEmpty()
    {
        // Act
        var result = AgentResult.CreateSuccess("Success");

        // Assert
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region CreateFailure Tests

    [Fact]
    public void CreateFailure_ShouldSetSuccessToFalse()
    {
        // Act
        var result = AgentResult.CreateFailure("Error occurred");

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void CreateFailure_ShouldSetMessage()
    {
        // Arrange
        var message = "Operation failed";

        // Act
        var result = AgentResult.CreateFailure(message);

        // Assert
        result.Message.Should().Be(message);
    }

    [Fact]
    public void CreateFailure_WithErrors_ShouldSetErrors()
    {
        // Arrange
        var errors = new List<string>
        {
            "Error 1",
            "Error 2",
            "Error 3"
        };

        // Act
        var result = AgentResult.CreateFailure("Failed", errors);

        // Assert
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Error 1");
        result.Errors.Should().Contain("Error 2");
        result.Errors.Should().Contain("Error 3");
    }

    [Fact]
    public void CreateFailure_WithoutErrors_ShouldCreateEmptyErrorsList()
    {
        // Act
        var result = AgentResult.CreateFailure("Failed");

        // Assert
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CreateFailure_DataShouldBeNull()
    {
        // Act
        var result = AgentResult.CreateFailure("Failed");

        // Assert
        result.Data.Should().BeNull();
    }

    #endregion

    #region Default Constructor Tests

    [Fact]
    public void DefaultConstructor_ShouldInitializeWithDefaults()
    {
        // Act
        var result = new AgentResult();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().BeEmpty();
        result.Data.Should().BeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var result = new AgentResult();

        // Act
        result.Success = true;
        result.Message = "Test";
        result.Data = new Dictionary<string, object> { { "key", "value" } };
        result.Errors = new List<string> { "error" };

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Test");
        result.Data!["key"].Should().Be("value");
        result.Errors.Should().Contain("error");
    }

    #endregion
}
