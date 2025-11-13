using RagAgentApi.Models;
using System.Collections.Concurrent;

namespace RagAgentApi.Services;

/// <summary>
/// Service for orchestrating agent execution and managing thread contexts
/// </summary>
public class AgentOrchestrationService
{
    private readonly ConcurrentDictionary<string, AgentContext> _contexts = new();
  private readonly ILogger<AgentOrchestrationService> _logger;

    public AgentOrchestrationService(ILogger<AgentOrchestrationService> logger)
    {
        _logger = logger;
    }

 /// <summary>
    /// Create a new agent context for a thread
    /// </summary>
    public AgentContext CreateContext(string? threadId = null)
    {
      var context = new AgentContext
        {
ThreadId = threadId ?? Guid.NewGuid().ToString()
        };

        _contexts[context.ThreadId] = context;
    _logger.LogInformation("Created new agent context for thread {ThreadId}", context.ThreadId);

        return context;
    }

/// <summary>
    /// Get an existing context by thread ID
    /// </summary>
    public AgentContext? GetContext(string threadId)
    {
   return _contexts.TryGetValue(threadId, out var context) ? context : null;
 }

    /// <summary>
    /// Update the context state
    /// </summary>
  public void UpdateContext(AgentContext context)
  {
  context.UpdatedAt = DateTimeOffset.UtcNow;
        _contexts[context.ThreadId] = context;
    }

    /// <summary>
    /// Add a message to the context
    /// </summary>
    public void AddMessage(AgentContext context, string from, string to, string content, Dictionary<string, object>? data = null)
{
        var message = new AgentMessage
   {
       From = from,
  To = to,
     Content = content,
   Data = data
   };

      context.Messages.Add(message);
      context.UpdatedAt = DateTimeOffset.UtcNow;
  _contexts[context.ThreadId] = context;

        _logger.LogDebug("Added message from {From} to {To} in thread {ThreadId}", from, to, context.ThreadId);
    }

    /// <summary>
    /// Remove old contexts to prevent memory leaks
    /// </summary>
    public int CleanupOldContexts(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
      var oldContexts = _contexts.Where(kvp => kvp.Value.UpdatedAt < cutoff).ToList();

        foreach (var kvp in oldContexts)
        {
            _contexts.TryRemove(kvp.Key, out _);
        }

        if (oldContexts.Any())
     {
   _logger.LogInformation("Cleaned up {Count} old contexts older than {MaxAge}", 
          oldContexts.Count, maxAge);
        }

    return oldContexts.Count;
    }

  /// <summary>
 /// Get all contexts (for monitoring/debugging)
  /// </summary>
   public List<AgentContext> GetAllContexts()
   {
     return _contexts.Values.ToList();
 }

    /// <summary>
    /// Remove a specific context
  /// </summary>
    public bool RemoveContext(string threadId)
    {
 var removed = _contexts.TryRemove(threadId, out _);
   if (removed)
   {
        _logger.LogInformation("Removed context for thread {ThreadId}", threadId);
   }
return removed;
  }
}