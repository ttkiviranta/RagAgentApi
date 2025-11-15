using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace RagAgentApi.Services;

/// <summary>
/// Service for selecting the appropriate agent type based on URL patterns
/// </summary>
public class AgentSelectorService
{
 private readonly RagDbContext _context;
    private readonly ILogger<AgentSelectorService> _logger;

  // Cache for agent mappings to avoid database queries
    private readonly Dictionary<string, List<CachedAgentMapping>> _cachedMappings = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    private readonly object _cacheLock = new();

    public AgentSelectorService(RagDbContext context, ILogger<AgentSelectorService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Select the best agent type for processing the given URL
    /// </summary>
    /// <param name="url">URL to analyze</param>
    /// <returns>AgentType with highest priority match, or default agent if no match</returns>
    public async Task<AgentType> SelectAgentTypeAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("[AgentSelectorService] Selecting agent for URL: {Url}", url);

            // Get cached or fresh agent mappings
      var mappings = await GetAgentMappingsAsync(cancellationToken);

 // Find the best matching agent based on priority
  var bestMatch = FindBestMatch(url, mappings);

    if (bestMatch != null)
            {
     _logger.LogInformation("[AgentSelectorService] Selected agent '{AgentName}' (Priority: {Priority}) for URL: {Url}", 
         bestMatch.AgentName, bestMatch.Priority, url);
     return bestMatch.AgentType;
  }

            // Fall back to default agent
            _logger.LogInformation("[AgentSelectorService] No specific agent found, using default agent for URL: {Url}", url);
    return await GetDefaultAgentAsync(cancellationToken);
      }
    catch (Exception ex)
        {
     _logger.LogError(ex, "[AgentSelectorService] Error selecting agent for URL: {Url}", url);
 
         // Return default agent on error
            return await GetDefaultAgentAsync(cancellationToken);
    }
    }

    /// <summary>
 /// Get all available agent types with their capabilities
    /// </summary>
    public async Task<List<AgentTypeInfo>> GetAvailableAgentTypesAsync(CancellationToken cancellationToken = default)
    {
   var agentTypes = await _context.AgentTypes
    .Include(at => at.UrlMappings)
 .Where(at => at.IsActive)
.OrderBy(at => at.Name)
     .ToListAsync(cancellationToken);

        return agentTypes.Select(at => new AgentTypeInfo
        {
      Id = at.Id,
       Name = at.Name,
        Description = at.Description,
      IsActive = at.IsActive,
            UrlPatterns = at.UrlMappings
  .Where(um => um.IsActive)
 .OrderByDescending(um => um.Priority)
          .Select(um => new UrlPatternInfo
    {
        Pattern = um.Pattern,
      Priority = um.Priority
              })
    .ToList(),
         AgentPipeline = at.AgentPipeline,
            Capabilities = at.Capabilities
 }).ToList();
    }

    /// <summary>
    /// Test which agent would be selected for a URL without actually creating the agent
    /// </summary>
    public async Task<AgentSelectionResult> TestAgentSelectionAsync(string url, CancellationToken cancellationToken = default)
    {
     try
        {
            var mappings = await GetAgentMappingsAsync(cancellationToken);
    var allMatches = FindAllMatches(url, mappings);

      var selectedAgent = await SelectAgentTypeAsync(url, cancellationToken);

  return new AgentSelectionResult
            {
                Url = url,
                SelectedAgent = new AgentTypeInfo
    {
      Id = selectedAgent.Id,
         Name = selectedAgent.Name,
    Description = selectedAgent.Description,
   IsActive = selectedAgent.IsActive,
   AgentPipeline = selectedAgent.AgentPipeline,
          Capabilities = selectedAgent.Capabilities
        },
                AllMatches = allMatches.Select(m => new MatchResult
            {
     AgentName = m.AgentName,
   Pattern = m.Pattern,
      Priority = m.Priority,
          MatchType = DetermineMatchType(url, m.Pattern)
    }).ToList(),
     SelectionReason = allMatches.Any() 
? $"Best match based on priority {allMatches.First().Priority}" 
         : "Default agent (no pattern matches)"
         };
        }
        catch (Exception ex)
        {
   _logger.LogError(ex, "[AgentSelectorService] Error testing agent selection for URL: {Url}", url);
   throw;
        }
    }

    /// <summary>
    /// Add or update an agent mapping
    /// </summary>
    public async Task<UrlAgentMapping> AddOrUpdateMappingAsync(
   string agentTypeName, 
  string pattern, 
        int priority, 
        bool isActive = true, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var agentType = await _context.AgentTypes
.FirstOrDefaultAsync(at => at.Name == agentTypeName, cancellationToken);

       if (agentType == null)
       {
         throw new ArgumentException($"Agent type '{agentTypeName}' not found");
   }

            // Check if mapping already exists
     var existingMapping = await _context.UrlAgentMappings
        .FirstOrDefaultAsync(um => um.AgentTypeId == agentType.Id && um.Pattern == pattern, cancellationToken);

     if (existingMapping != null)
     {
    // Update existing mapping
existingMapping.Priority = priority;
       existingMapping.IsActive = isActive;
   _logger.LogInformation("[AgentSelectorService] Updated mapping: {Pattern} -> {AgentName}", pattern, agentTypeName);
      }
          else
            {
                // Create new mapping
          existingMapping = new UrlAgentMapping
      {
   AgentTypeId = agentType.Id,
         Pattern = pattern,
    Priority = priority,
   IsActive = isActive,
         CreatedAt = DateTime.UtcNow
       };

                _context.UrlAgentMappings.Add(existingMapping);
           _logger.LogInformation("[AgentSelectorService] Created new mapping: {Pattern} -> {AgentName}", pattern, agentTypeName);
            }

       await _context.SaveChangesAsync(cancellationToken);
            
            // Invalidate cache
            InvalidateCache();

        return existingMapping;
        }
    catch (Exception ex)
        {
         _logger.LogError(ex, "[AgentSelectorService] Error adding/updating mapping: {Pattern} -> {AgentName}", 
  pattern, agentTypeName);
       throw;
  }
    }

    private async Task<List<CachedAgentMapping>> GetAgentMappingsAsync(CancellationToken cancellationToken)
    {
        lock (_cacheLock)
    {
    // Check if cache is still valid
       if (_cachedMappings.Any() && DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry)
            {
                return _cachedMappings.Values.SelectMany(x => x).ToList();
   }
      }

    // Refresh cache
        var mappings = await _context.UrlAgentMappings
            .Include(um => um.AgentType)
            .Where(um => um.IsActive && um.AgentType.IsActive)
      .OrderByDescending(um => um.Priority)
         .ToListAsync(cancellationToken);

        var cachedMappings = mappings.Select(um => new CachedAgentMapping
        {
        Pattern = um.Pattern,
   Priority = um.Priority,
   AgentName = um.AgentType.Name,
            AgentType = um.AgentType,
 CompiledRegex = new Regex(um.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
        }).ToList();

   lock (_cacheLock)
  {
            _cachedMappings.Clear();
 foreach (var mapping in cachedMappings)
   {
   if (!_cachedMappings.ContainsKey(mapping.AgentName))
    {
_cachedMappings[mapping.AgentName] = new List<CachedAgentMapping>();
     }
          _cachedMappings[mapping.AgentName].Add(mapping);
     }
  _lastCacheUpdate = DateTime.UtcNow;
        }

        _logger.LogDebug("[AgentSelectorService] Refreshed agent mappings cache with {Count} mappings", cachedMappings.Count);
        return cachedMappings;
    }

    private static CachedAgentMapping? FindBestMatch(string url, List<CachedAgentMapping> mappings)
    {
        return mappings
  .Where(mapping => mapping.CompiledRegex.IsMatch(url))
     .OrderByDescending(mapping => mapping.Priority)
    .FirstOrDefault();
    }

    private static List<CachedAgentMapping> FindAllMatches(string url, List<CachedAgentMapping> mappings)
    {
   return mappings
            .Where(mapping => mapping.CompiledRegex.IsMatch(url))
          .OrderByDescending(mapping => mapping.Priority)
            .ToList();
    }

    private async Task<AgentType> GetDefaultAgentAsync(CancellationToken cancellationToken)
    {
      var defaultAgent = await _context.AgentTypes
            .FirstOrDefaultAsync(at => at.Name == "default_agent" && at.IsActive, cancellationToken);

        if (defaultAgent == null)
        {
      throw new InvalidOperationException("Default agent not found in database. Please ensure the database is seeded properly.");
        }

        return defaultAgent;
    }

    private static string DetermineMatchType(string url, string pattern)
    {
 // Simple heuristics for match type
     if (pattern.Contains("github.com"))
  return "GitHub Repository";
        if (pattern.Contains("youtube.com") || pattern.Contains("youtu.be"))
            return "YouTube Video";
        if (pattern.Contains("arxiv.org"))
            return "Academic Paper";
 if (pattern.Contains("news") || pattern.Contains("blog"))
       return "News/Blog Article";
        
        return "Generic Pattern";
    }

    private void InvalidateCache()
    {
        lock (_cacheLock)
        {
         _cachedMappings.Clear();
     _lastCacheUpdate = DateTime.MinValue;
      }
    }

    // Helper classes
    private class CachedAgentMapping
    {
        public string Pattern { get; set; } = "";
 public int Priority { get; set; }
      public string AgentName { get; set; } = "";
  public AgentType AgentType { get; set; } = null!;
        public Regex CompiledRegex { get; set; } = null!;
    }
}

// DTOs for API responses
public class AgentTypeInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<UrlPatternInfo> UrlPatterns { get; set; } = new();
    public System.Text.Json.JsonDocument? AgentPipeline { get; set; }
    public System.Text.Json.JsonDocument? Capabilities { get; set; }
}

public class UrlPatternInfo
{
    public string Pattern { get; set; } = "";
    public int Priority { get; set; }
}

public class AgentSelectionResult
{
    public string Url { get; set; } = "";
 public AgentTypeInfo SelectedAgent { get; set; } = null!;
    public List<MatchResult> AllMatches { get; set; } = new();
    public string SelectionReason { get; set; } = "";
}

public class MatchResult
{
    public string AgentName { get; set; } = "";
    public string Pattern { get; set; } = "";
    public int Priority { get; set; }
    public string MatchType { get; set; } = "";
}