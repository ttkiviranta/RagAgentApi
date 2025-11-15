namespace RagAgentApi.Models.Requests;

/// <summary>
/// Request for testing URL processing
/// </summary>
public class TestUrlRequest
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Request for creating agents
/// </summary>
public class CreateAgentRequest
{
    public string AgentName { get; set; } = string.Empty;
}

/// <summary>
/// Request for adding agent mappings
/// </summary>
public class AddMappingRequest
{
    public string AgentTypeName { get; set; } = string.Empty;
 public string Pattern { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request for testing agent types
/// </summary>
public class TestAgentTypeRequest
{
    public string AgentTypeName { get; set; } = string.Empty;
}