using Microsoft.EntityFrameworkCore;
using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;
using System.Text.Json;

namespace RagAgentApi.Services;

/// <summary>
/// Service for seeding initial agent types and URL mappings
/// </summary>
public class DatabaseSeedService
{
    private readonly RagDbContext _context;
    private readonly ILogger<DatabaseSeedService> _logger;

    public DatabaseSeedService(RagDbContext context, ILogger<DatabaseSeedService> logger)
    {
        _context = context;
   _logger = logger;
    }

    /// <summary>
 /// Seed initial agent types and URL mappings
    /// </summary>
    public async Task SeedAgentTypesAsync()
    {
        try
    {
            _logger.LogInformation("Starting agent types seeding...");

      // Check if already seeded
    if (await _context.AgentTypes.AnyAsync())
 {
            _logger.LogInformation("Agent types already exist, skipping seeding");
  return;
            }

    var agentTypes = new List<AgentType>();
            var urlMappings = new List<UrlAgentMapping>();

     // 1. Default Agent
            var defaultAgent = new AgentType
          {
                Name = "default_agent",
      Description = "Default general-purpose web scraping agent for any URL",
          AgentPipeline = JsonDocument.Parse(@"[""ScraperAgent"", ""ChunkerAgent"", ""EmbeddingAgent"", ""PostgresStorageAgent""]"),
      Capabilities = JsonDocument.Parse(@"{
       ""content_types"": [""html"", ""text"", ""pdf""],
       ""features"": [""web_scraping"", ""text_chunking"", ""embedding_generation"", ""storage""],
            ""limitations"": [""basic_html_parsing"", ""no_javascript_execution""]
    }"),
                IsActive = true,
         CreatedAt = DateTime.UtcNow
      };
    agentTypes.Add(defaultAgent);

            // 2. GitHub Agent
            var githubAgent = new AgentType
            {
          Name = "github_specialist",
     Description = "Specialized agent for GitHub repositories, files, and documentation",
                AgentPipeline = JsonDocument.Parse(@"[""GitHubApiAgent"", ""ChunkerAgent"", ""EmbeddingAgent"", ""PostgresStorageAgent""]"),
    Capabilities = JsonDocument.Parse(@"{
""content_types"": [""markdown"", ""code"", ""documentation"", ""issues"", ""pull_requests""],
     ""features"": [""github_api_integration"", ""repository_analysis"", ""code_structure_parsing"", ""readme_extraction""],
    ""supported_formats"": [""md"", ""rst"", ""txt"", ""various_code_languages""],
        ""metadata"": [""stars"", ""forks"", ""contributors"", ""languages"", ""topics""]
          }"),
      IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
     agentTypes.Add(githubAgent);

      // 3. YouTube Agent
            var youtubeAgent = new AgentType
            {
    Name = "youtube_specialist", 
       Description = "Specialized agent for YouTube videos and transcripts",
    AgentPipeline = JsonDocument.Parse(@"[""YouTubeTranscriptAgent"", ""ChunkerAgent"", ""EmbeddingAgent"", ""PostgresStorageAgent""]"),
  Capabilities = JsonDocument.Parse(@"{
            ""content_types"": [""video_transcript"", ""video_metadata"", ""captions""],
    ""features"": [""transcript_extraction"", ""timestamp_preservation"", ""multi_language_support"", ""video_metadata""],
           ""supported_languages"": [""auto_generated"", ""manual_captions"", ""multiple_languages""],
       ""metadata"": [""duration"", ""view_count"", ""channel_info"", ""publication_date""]
             }"),
          IsActive = true,
   CreatedAt = DateTime.UtcNow
      };
         agentTypes.Add(youtubeAgent);

    // 4. arXiv Agent
        var arxivAgent = new AgentType
            {
    Name = "arxiv_specialist",
          Description = "Specialized agent for academic papers from arXiv",
     AgentPipeline = JsonDocument.Parse(@"[""ArxivScraperAgent"", ""ChunkerAgent"", ""EmbeddingAgent"", ""PostgresStorageAgent""]"),
      Capabilities = JsonDocument.Parse(@"{
        ""content_types"": [""academic_paper"", ""pdf"", ""latex"", ""scientific_text""],
       ""features"": [""pdf_processing"", ""academic_structure_parsing"", ""citation_extraction"", ""math_formula_handling""],
       ""metadata"": [""authors"", ""abstract"", ""categories"", ""submission_date"", ""doi"", ""citations""],
   ""special_processing"": [""mathematical_equations"", ""figures_and_tables"", ""bibliography""]
        }"),
     IsActive = true,
             CreatedAt = DateTime.UtcNow
            };
            agentTypes.Add(arxivAgent);

    // 5. News Article Agent  
            var newsAgent = new AgentType
    {
        Name = "news_specialist",
   Description = "Specialized agent for news articles and blog posts",
                AgentPipeline = JsonDocument.Parse(@"[""NewsArticleScraperAgent"", ""ChunkerAgent"", ""EmbeddingAgent"", ""PostgresStorageAgent""]"),
    Capabilities = JsonDocument.Parse(@"{
      ""content_types"": [""news_article"", ""blog_post"", ""editorial"", ""press_release""],
 ""features"": [""readability_extraction"", ""author_detection"", ""publication_date_parsing"", ""category_detection""],
      ""supported_sites"": [""major_news_outlets"", ""technology_blogs"", ""regional_news"", ""blog_platforms""],
               ""metadata"": [""author"", ""publication_date"", ""category"", ""tags"", ""social_shares""]
         }"),
       IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
 agentTypes.Add(newsAgent);

            // Save agent types first
     _context.AgentTypes.AddRange(agentTypes);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {Count} agent types", agentTypes.Count);

     // Create URL mappings
      // Default agent (lowest priority, catches everything)
        urlMappings.Add(new UrlAgentMapping
   {
                AgentTypeId = defaultAgent.Id,
             Pattern = ".*",
      Priority = 1,
        IsActive = true,
             CreatedAt = DateTime.UtcNow
        });

      // GitHub patterns (highest priority)
            urlMappings.Add(new UrlAgentMapping
          {
                AgentTypeId = githubAgent.Id,
                Pattern = @"^https://github\.com/[\w\-\.]+/[\w\-\.]+",
      Priority = 10,
         IsActive = true,
       CreatedAt = DateTime.UtcNow
         });

            // YouTube patterns
    urlMappings.AddRange(new[]
  {
       new UrlAgentMapping
    {
      AgentTypeId = youtubeAgent.Id,
           Pattern = @"^https://(www\.)?youtube\.com/watch\?v=[\w\-]+",
   Priority = 9,
             IsActive = true,
       CreatedAt = DateTime.UtcNow
       },
    new UrlAgentMapping
             {
     AgentTypeId = youtubeAgent.Id,
   Pattern = @"^https://youtu\.be/[\w\-]+",
           Priority = 9,
   IsActive = true,
 CreatedAt = DateTime.UtcNow
              },
    new UrlAgentMapping
     {
          AgentTypeId = youtubeAgent.Id,
   Pattern = @"^https://(www\.)?youtube\.com/playlist\?list=[\w\-]+",
   Priority = 9,
       IsActive = true,
                 CreatedAt = DateTime.UtcNow
   }
        });

            // arXiv patterns
  urlMappings.AddRange(new[]
            {
                new UrlAgentMapping
           {
        AgentTypeId = arxivAgent.Id,
        Pattern = @"^https://arxiv\.org/abs/\d{4}\.\d{4,5}",
       Priority = 8,
         IsActive = true,
          CreatedAt = DateTime.UtcNow
 },
     new UrlAgentMapping
      {
    AgentTypeId = arxivAgent.Id,
   Pattern = @"^https://arxiv\.org/pdf/\d{4}\.\d{4,5}",
        Priority = 8,
    IsActive = true,
       CreatedAt = DateTime.UtcNow
                }
            });

      // News/Blog patterns
            urlMappings.AddRange(new[]
 {
   new UrlAgentMapping
        {
  AgentTypeId = newsAgent.Id,
          Pattern = @"^https://(www\.)?(yle|hs|iltalehti|mtv|is)\.fi/",
          Priority = 7,
  IsActive = true,
        CreatedAt = DateTime.UtcNow
       },
          new UrlAgentMapping
           {
            AgentTypeId = newsAgent.Id,
   Pattern = @"^https://(www\.)?(bbc\.com|cnn\.com|reuters\.com|techcrunch\.com|wired\.com)/",
    Priority = 7,
         IsActive = true,
          CreatedAt = DateTime.UtcNow
     },
        new UrlAgentMapping
             {
    AgentTypeId = newsAgent.Id,
       Pattern = @".*/blog/.*",
       Priority = 5,
IsActive = true,
         CreatedAt = DateTime.UtcNow
  },
    new UrlAgentMapping
     {
       AgentTypeId = newsAgent.Id,
          Pattern = @".*/news/.*",
            Priority = 5,
    IsActive = true,
          CreatedAt = DateTime.UtcNow
}
         });

      // Save URL mappings
   _context.UrlAgentMappings.AddRange(urlMappings);
            await _context.SaveChangesAsync();

  _logger.LogInformation("Created {Count} URL mappings", urlMappings.Count);
         _logger.LogInformation("Agent types seeding completed successfully");
      }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to seed agent types");
   throw;
        }
    }

    /// <summary>
    /// Get seeding status
    /// </summary>
    public async Task<SeedingStatus> GetSeedingStatusAsync()
    {
        return new SeedingStatus
        {
   AgentTypesCount = await _context.AgentTypes.CountAsync(),
      UrlMappingsCount = await _context.UrlAgentMappings.CountAsync(),
            IsSeeded = await _context.AgentTypes.AnyAsync(),
            LastSeedCheck = DateTime.UtcNow
        };
    }
}

public class SeedingStatus
{
    public int AgentTypesCount { get; set; }
    public int UrlMappingsCount { get; set; }
    public bool IsSeeded { get; set; }
    public DateTime LastSeedCheck { get; set; }
}