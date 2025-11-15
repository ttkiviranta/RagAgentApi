using RagAgentApi.Models;
using System.Text.Json;

namespace RagAgentApi.Agents;

/// <summary>
/// Specialized agent for GitHub repositories and files
/// Uses GitHub API (Octokit) to fetch repository content, README files, and metadata
/// </summary>
public class GitHubApiAgent : BaseRagAgent
{
    private readonly HttpClient _httpClient;

    public GitHubApiAgent(HttpClient httpClient, ILogger<GitHubApiAgent> logger) : base(logger)
    {
    _httpClient = httpClient;
    }

    public override string Name => "GitHubApiAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
        // Validate URL is GitHub
    if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
       {
          return AgentResult.CreateFailure("URL not found in context state");
        }

        if (!IsGitHubUrl(url))
      {
     return AgentResult.CreateFailure($"URL {url} is not a GitHub URL");
     }

            _logger.LogInformation("[GitHubApiAgent] Processing GitHub URL: {Url}", url);

            // TODO: Implement GitHub API integration
            // - Parse GitHub URL (repo/file/tree)
          // - Use Octokit library for GitHub API calls
    // - Extract repository metadata (stars, language, description)
     // - Fetch README content and documentation
 // - Handle rate limiting
            // - Support private repositories with authentication

            // For now, return a placeholder indicating the URL type
 var gitHubInfo = ParseGitHubUrl(url);
   
    var placeholderContent = GeneratePlaceholderContent(url, gitHubInfo);
            
            // Store extracted content in context
     context.State["raw_content"] = placeholderContent;
        context.State["source_url"] = url;
            context.State["content_type"] = "github";
  context.State["github_metadata"] = JsonDocument.Parse(JsonSerializer.Serialize(gitHubInfo));

      stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

     _logger.LogInformation("[GitHubApiAgent] Processed GitHub URL: {Url} ({Type})", 
                url, gitHubInfo.Type);

          AddMessage(context, "ChunkerAgent", 
 $"GitHub content extracted successfully (Type: {gitHubInfo.Type})",
 new Dictionary<string, object>
  {
    { "url", url },
     { "github_type", gitHubInfo.Type },
        { "owner", gitHubInfo.Owner },
              { "repository", gitHubInfo.Repository },
           { "content_length", placeholderContent.Length }
        });

      return AgentResult.CreateSuccess(
     "GitHub content extracted successfully (PLACEHOLDER)",
       new Dictionary<string, object>
       {
            { "url", url },
              { "github_type", gitHubInfo.Type },
          { "owner", gitHubInfo.Owner },
        { "repository", gitHubInfo.Repository },
   { "content_length", placeholderContent.Length },
  { "extraction_time_ms", stopwatch.ElapsedMilliseconds }
                });
        }
        catch (Exception ex)
        {
      stopwatch.Stop();
      LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
      return HandleException(ex, context.ThreadId, "GitHub API processing");
        }
    }

    private static bool IsGitHubUrl(string url)
    {
        return url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase);
    }

    private static GitHubUrlInfo ParseGitHubUrl(string url)
    {
      // Parse GitHub URL patterns:
        // https://github.com/owner/repo
        // https://github.com/owner/repo/blob/branch/file.md
        // https://github.com/owner/repo/tree/branch/folder
        
     var uri = new Uri(url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        
        if (segments.Length < 2)
        {
 return new GitHubUrlInfo { Type = "invalid", Owner = "", Repository = "" };
     }

        var info = new GitHubUrlInfo
        {
    Owner = segments[0],
      Repository = segments[1]
  };

        if (segments.Length == 2)
        {
  info.Type = "repository";
        }
        else if (segments.Length >= 4 && segments[2] == "blob")
   {
      info.Type = "file";
     info.Branch = segments[3];
       info.Path = string.Join("/", segments.Skip(4));
     }
        else if (segments.Length >= 4 && segments[2] == "tree")
        {
            info.Type = "tree";
        info.Branch = segments[3];
     info.Path = string.Join("/", segments.Skip(4));
        }
        else
        {
          info.Type = "repository";
   }

        return info;
  }

    private static string GeneratePlaceholderContent(string url, GitHubUrlInfo gitHubInfo)
    {
        return $@"# GitHub Repository: {gitHubInfo.Owner}/{gitHubInfo.Repository}

**URL**: {url}
**Type**: {gitHubInfo.Type}
**Owner**: {gitHubInfo.Owner}
**Repository**: {gitHubInfo.Repository}
{(string.IsNullOrEmpty(gitHubInfo.Branch) ? "" : $"**Branch**: {gitHubInfo.Branch}")}
{(string.IsNullOrEmpty(gitHubInfo.Path) ? "" : $"**Path**: {gitHubInfo.Path}")}

## TODO: GitHub API Integration

This is a placeholder for GitHub API integration. When implemented, this agent will:

1. **Repository Analysis**: Extract repository metadata including stars, forks, language, topics, and description
2. **README Extraction**: Fetch and parse README files in various formats (markdown, rst, txt)
3. **Documentation Scraping**: Extract documentation from docs/ folders, wiki pages, and GitHub Pages
4. **Code Analysis**: Analyze code structure, dependencies, and key files
5. **Issue & PR Context**: Extract relevant issues and pull request discussions
6. **Release Notes**: Fetch release notes and changelog information

## Implementation Plan

- Install Octokit NuGet package for GitHub API
- Add GitHub authentication support (PAT tokens)
- Implement rate limiting and retry logic
- Add support for private repositories
- Cache frequently accessed repositories
- Extract code snippets and documentation intelligently

This placeholder content allows the RAG system to understand that this URL points to a GitHub repository
and provides basic metadata for processing.";
    }

    private class GitHubUrlInfo
    {
        public string Type { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Repository { get; set; } = "";
        public string Branch { get; set; } = "";
   public string Path { get; set; } = "";
    }
}