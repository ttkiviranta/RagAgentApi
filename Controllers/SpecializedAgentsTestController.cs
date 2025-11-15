using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Agents;
using RagAgentApi.Services;
using RagAgentApi.Models;
using RagAgentApi.Models.Requests;

namespace RagAgentApi.Controllers;

/// <summary>
/// Test controller for specialized agents
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SpecializedAgentsTestController : ControllerBase
{
    private readonly GitHubApiAgent _githubAgent;
    private readonly YouTubeTranscriptAgent _youtubeAgent;
    private readonly ArxivScraperAgent _arxivAgent;
    private readonly NewsArticleScraperAgent _newsAgent;
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly ILogger<SpecializedAgentsTestController> _logger;

    public SpecializedAgentsTestController(
        GitHubApiAgent githubAgent,
        YouTubeTranscriptAgent youtubeAgent,
        ArxivScraperAgent arxivAgent,
        NewsArticleScraperAgent newsAgent,
        AgentOrchestrationService orchestrationService,
 ILogger<SpecializedAgentsTestController> logger)
    {
        _githubAgent = githubAgent;
        _youtubeAgent = youtubeAgent;
     _arxivAgent = arxivAgent;
        _newsAgent = newsAgent;
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Test GitHub API agent with various GitHub URLs
    /// </summary>
    [HttpPost("test-github")]
    public async Task<IActionResult> TestGitHub([FromBody] TestUrlRequest request)
    {
      try
        {
            var context = _orchestrationService.CreateContext();
 context.State["url"] = request.Url;

            var result = await _githubAgent.ExecuteAsync(context);

            return Ok(new
            {
        agent = "GitHubApiAgent",
      url = request.Url,
     success = result.Success,
 message = result.Message,
       data = result.Data,
     metadata = context.State.TryGetValue("github_metadata", out var metadata) ? metadata : null,
          content_preview = context.State.TryGetValue("raw_content", out var content) && content is string contentStr
      ? (contentStr.Length > 300 ? contentStr.Substring(0, 300) + "..." : contentStr)
        : null,
            timestamp = DateTimeOffset.UtcNow
   });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub agent test failed for URL: {Url}", request.Url);
    return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test YouTube transcript agent with YouTube URLs
    /// </summary>
    [HttpPost("test-youtube")]
    public async Task<IActionResult> TestYouTube([FromBody] TestUrlRequest request)
    {
      try
        {
     var context = _orchestrationService.CreateContext();
            context.State["url"] = request.Url;

            var result = await _youtubeAgent.ExecuteAsync(context);

            return Ok(new
         {
   agent = "YouTubeTranscriptAgent",
   url = request.Url,
   success = result.Success,
                message = result.Message,
        data = result.Data,
       metadata = context.State.TryGetValue("youtube_metadata", out var metadata) ? metadata : null,
        content_preview = context.State.TryGetValue("raw_content", out var content) && content is string contentStr
     ? (contentStr.Length > 300 ? contentStr.Substring(0, 300) + "..." : contentStr)
      : null,
           timestamp = DateTimeOffset.UtcNow
 });
        }
        catch (Exception ex)
        {
     _logger.LogError(ex, "YouTube agent test failed for URL: {Url}", request.Url);
     return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
  }
    }

    /// <summary>
    /// Test arXiv scraper agent with academic paper URLs
    /// </summary>
    [HttpPost("test-arxiv")]
    public async Task<IActionResult> TestArxiv([FromBody] TestUrlRequest request)
{
        try
        {
     var context = _orchestrationService.CreateContext();
       context.State["url"] = request.Url;

       var result = await _arxivAgent.ExecuteAsync(context);

  return Ok(new
            {
         agent = "ArxivScraperAgent",
     url = request.Url,
 success = result.Success,
   message = result.Message,
         data = result.Data,
    metadata = context.State.TryGetValue("arxiv_metadata", out var metadata) ? metadata : null,
  content_preview = context.State.TryGetValue("raw_content", out var content) && content is string contentStr
  ? (contentStr.Length > 300 ? contentStr.Substring(0, 300) + "..." : contentStr)
       : null,
   timestamp = DateTimeOffset.UtcNow
            });
   }
        catch (Exception ex)
        {
            _logger.LogError(ex, "arXiv agent test failed for URL: {Url}", request.Url);
            return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test news article agent with news URLs
    /// </summary>
    [HttpPost("test-news")]
    public async Task<IActionResult> TestNews([FromBody] TestUrlRequest request)
{
     try
        {
          var context = _orchestrationService.CreateContext();
  context.State["url"] = request.Url;

      var result = await _newsAgent.ExecuteAsync(context);

   return Ok(new
            {
     agent = "NewsArticleScraperAgent",
  url = request.Url,
 success = result.Success,
          message = result.Message,
 data = result.Data,
   metadata = context.State.TryGetValue("article_metadata", out var metadata) ? metadata : null,
    content_preview = context.State.TryGetValue("raw_content", out var content) && content is string contentStr
   ? (contentStr.Length > 300 ? contentStr.Substring(0, 300) + "..." : contentStr)
           : null,
   timestamp = DateTimeOffset.UtcNow
         });
        }
        catch (Exception ex)
      {
   _logger.LogError(ex, "News agent test failed for URL: {Url}", request.Url);
  return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test all specialized agents with a batch of URLs
    /// </summary>
    [HttpPost("test-all")]
    public async Task<IActionResult> TestAllAgents()
    {
        var testUrls = new[]
        {
      new { url = "https://github.com/microsoft/semantic-kernel", expected_agent = "GitHub" },
     new { url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", expected_agent = "YouTube" },
        new { url = "https://arxiv.org/abs/2301.00001", expected_agent = "arXiv" },
  new { url = "https://yle.fi/a/74-20000123", expected_agent = "News" }
        };

        var results = new List<object>();

        foreach (var testUrl in testUrls)
        {
            try
    {
    var context = _orchestrationService.CreateContext();
        context.State["url"] = testUrl.url;

      AgentResult result = testUrl.expected_agent switch
    {
           "GitHub" => await _githubAgent.ExecuteAsync(context),
   "YouTube" => await _youtubeAgent.ExecuteAsync(context),
     "arXiv" => await _arxivAgent.ExecuteAsync(context),
               "News" => await _newsAgent.ExecuteAsync(context),
  _ => throw new ArgumentException($"Unknown agent type: {testUrl.expected_agent}")
      };

    results.Add(new
          {
         url = testUrl.url,
   expected_agent = testUrl.expected_agent,
 success = result.Success,
          message = result.Message,
      processing_time_ms = result.Data?.GetValueOrDefault("extraction_time_ms", 0),
          content_length = context.State.TryGetValue("raw_content", out var content) && content is string contentStr
               ? contentStr.Length
       : 0
         });
      }
    catch (Exception ex)
    {
                results.Add(new
       {
        url = testUrl.url,
       expected_agent = testUrl.expected_agent,
    success = false,
                 error = ex.Message
                });
            }
  }

        return Ok(new
        {
         test_summary = new
     {
       total_tests = testUrls.Length,
     successful = results.Count(r => ((dynamic)r).success),
         failed = results.Count(r => !((dynamic)r).success)
     },
        results = results,
    timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Get agent capabilities and supported URL patterns
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult GetCapabilities()
    {
        var capabilities = new
    {
      specialized_agents = new[]
        {
       new
    {
        name = "GitHubApiAgent",
      description = "Extracts content from GitHub repositories, files, and documentation",
    supported_urls = new[]
          {
"https://github.com/owner/repo",
     "https://github.com/owner/repo/blob/branch/file.md",
      "https://github.com/owner/repo/tree/branch/folder"
     },
         status = "Placeholder - Ready for Octokit integration",
      features = new[]
            {
   "Repository metadata extraction",
               "README and documentation parsing",
  "Code structure analysis",
    "Issue and PR context extraction"
}
           },
     new
     {
            name = "YouTubeTranscriptAgent",
    description = "Extracts video metadata and transcripts from YouTube",
       supported_urls = new[]
        {
            "https://youtube.com/watch?v=VIDEO_ID",
  "https://youtu.be/VIDEO_ID",
               "https://youtube.com/playlist?list=PLAYLIST_ID"
                },
           status = "Placeholder - Ready for YoutubeExplode integration",
   features = new[]
          {
             "Video metadata extraction",
 "Transcript/caption processing",
        "Timestamp-aligned segments",
               "Multi-language support"
    }
      },
    new
          {
     name = "ArxivScraperAgent",
        description = "Downloads and processes academic papers from arXiv",
              supported_urls = new[]
       {
        "https://arxiv.org/abs/2301.00001",
         "https://arxiv.org/pdf/2301.00001.pdf"
 },
     status = "Placeholder - Ready for PDF processing integration",
             features = new[]
   {
        "Paper metadata from arXiv API",
      "PDF text extraction",
                  "Academic structure analysis",
           "Citation and reference parsing"
                 }
     },
       new
                {
   name = "NewsArticleScraperAgent",
   description = "Extracts content from news articles and blog posts",
       supported_urls = new[]
            {
       "Major news outlets (BBC, CNN, Reuters, etc.)",
            "Technology blogs (TechCrunch, Wired, etc.)",
 "Finnish news sites (YLE, HS, etc.)",
      "Blog platforms (Medium, WordPress, etc.)"
       },
    status = "Placeholder - Ready for HtmlAgilityPack integration",
     features = new[]
       {
       "Intelligent content extraction",
  "Author and publication date detection",
     "Site-specific optimization",
  "Multi-language support"
           }
   }
     },
    implementation_roadmap = new
            {
          phase_1 = "Basic URL recognition and metadata extraction (CURRENT - Placeholder)",
      phase_2 = "Core library integration (Octokit, YoutubeExplode, PDF libraries, HtmlAgilityPack)",
            phase_3 = "Advanced content processing and structure analysis",
                phase_4 = "AI-powered content enhancement and cross-referencing"
       },
  timestamp = DateTimeOffset.UtcNow
        };

      return Ok(capabilities);
    }
}