namespace RagAgentApi.Models;

public class AgentMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public string[] Inputs { get; set; } = Array.Empty<string>();
    public string[] Outputs { get; set; } = Array.Empty<string>();
}
