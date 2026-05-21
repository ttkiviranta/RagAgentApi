namespace RagAgentApi.Services;

/// <summary>
/// Service for loading and managing prompt versions.
/// Prompts are stored in Prompts/ folder with versioning scheme: AgentName_v1.txt
/// </summary>
public interface IPromptService
{
    string GetPrompt(string agentName, int version = 1);
    (string content, int version) GetPromptWithVersion(string agentName);
    Dictionary<string, (string content, int version)> GetAllPrompts();
}

public class PromptService : IPromptService
{
    private readonly ILogger<PromptService> _logger;
    private readonly Dictionary<string, (string content, int version)> _prompts = new();
    private readonly string _promptsDirectory;

    public PromptService(ILogger<PromptService> logger)
    {
        _logger = logger;
        _promptsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Prompts");
        LoadAllPrompts();
    }

    private void LoadAllPrompts()
    {
        if (!Directory.Exists(_promptsDirectory))
        {
            _logger.LogWarning("[PromptService] Prompts directory not found at {Path}", _promptsDirectory);
            return;
        }

        var promptFiles = Directory.GetFiles(_promptsDirectory, "*.txt");
        foreach (var file in promptFiles)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('_');

                if (parts.Length >= 2 && parts[^1].StartsWith("v"))
                {
                    var agentName = string.Join("_", parts[..^1]);
                    var version = int.Parse(parts[^1].Substring(1));
                    var content = File.ReadAllText(file);

                    _prompts[agentName] = (content, version);
                    _logger.LogInformation("[PromptService] Loaded prompt: {AgentName} v{Version}", agentName, version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PromptService] Failed to load prompt from {File}", file);
            }
        }

        _logger.LogInformation("[PromptService] Loaded {Count} prompts", _prompts.Count);
    }

    public string GetPrompt(string agentName, int version = 1)
    {
        if (_prompts.TryGetValue(agentName, out var prompt))
        {
            return prompt.content;
        }

        _logger.LogWarning("[PromptService] Prompt not found for agent: {AgentName} v{Version}", agentName, version);
        return string.Empty;
    }

    public (string content, int version) GetPromptWithVersion(string agentName)
    {
        if (_prompts.TryGetValue(agentName, out var prompt))
        {
            return prompt;
        }

        return (string.Empty, 0);
    }

    public Dictionary<string, (string content, int version)> GetAllPrompts() => _prompts;
}
