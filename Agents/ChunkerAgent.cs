using RagAgentApi.Models;
using System.Text.RegularExpressions;

namespace RagAgentApi.Agents;

/// <summary>
/// Chunks text content into smaller, overlapping pieces
/// </summary>
public class ChunkerAgent : BaseRagAgent
{
    private readonly IConfiguration _configuration;

  public ChunkerAgent(IConfiguration configuration, ILogger<ChunkerAgent> logger) : base(logger)
    {
        _configuration = configuration;
    }

    public override string Name => "ChunkerAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
   LogExecutionStart(context.ThreadId);

  try
  {
   if (!context.State.TryGetValue("raw_content", out var contentObj) || contentObj is not string content)
 {
   return AgentResult.CreateFailure("Raw content not found in context state");
      }

     // Get chunking parameters
     var chunkSize = GetChunkSize(context);
            var chunkOverlap = GetChunkOverlap(context);

       // Validate parameters
     var validationResult = ValidateParameters(chunkSize, chunkOverlap);
     if (!validationResult.Success)
      {
      return validationResult;
      }

    _logger.LogInformation("[ChunkerAgent] Chunking content with size {ChunkSize}, overlap {ChunkOverlap}", 
   chunkSize, chunkOverlap);

    // Split content into chunks
     var chunks = ChunkText(content, chunkSize, chunkOverlap);

   if (!chunks.Any())
  {
         return AgentResult.CreateFailure("No chunks created from content");
  }

   // Store chunks in context state
     context.State["chunks"] = chunks;
        context.State["chunk_size"] = chunkSize;
   context.State["chunk_overlap"] = chunkOverlap;

      stopwatch.Stop();
       LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

    _logger.LogInformation("[ChunkerAgent] Created {Count} chunks", chunks.Count);

    AddMessage(context, "EmbeddingAgent", $"Content chunked into {chunks.Count} pieces",
   new Dictionary<string, object>
      {
        { "chunk_count", chunks.Count },
       { "chunk_size", chunkSize },
  { "chunk_overlap", chunkOverlap }
       });

  return AgentResult.CreateSuccess(
       "Content chunked successfully",
    new Dictionary<string, object>
      {
     { "chunk_count", chunks.Count },
      { "chunk_size", chunkSize },
  { "chunk_overlap", chunkOverlap }
     });
   }
        catch (Exception ex)
     {
       stopwatch.Stop();
    LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
  return HandleException(ex, context.ThreadId, "content chunking");
 }
    }

private int GetChunkSize(AgentContext context)
 {
        if (context.State.TryGetValue("chunk_size", out var chunkSizeObj) && chunkSizeObj is int chunkSize)
   {
            return chunkSize;
 }

      return _configuration.GetValue<int>("RagSettings:DefaultChunkSize", 1000);
    }

 private int GetChunkOverlap(AgentContext context)
  {
   if (context.State.TryGetValue("chunk_overlap", out var overlapObj) && overlapObj is int overlap)
      {
 return overlap;
        }

      return _configuration.GetValue<int>("RagSettings:DefaultChunkOverlap", 200);
  }

 private AgentResult ValidateParameters(int chunkSize, int chunkOverlap)
   {
     var minChunkSize = _configuration.GetValue<int>("RagSettings:MinChunkSize", 100);
   var maxChunkSize = _configuration.GetValue<int>("RagSettings:MaxChunkSize", 5000);

   var errors = new List<string>();

    if (chunkSize < minChunkSize || chunkSize > maxChunkSize)
 {
      errors.Add($"Chunk size must be between {minChunkSize} and {maxChunkSize}, got {chunkSize}");
        }

        if (chunkOverlap < 0 || chunkOverlap >= chunkSize / 2)
  {
  errors.Add($"Chunk overlap must be between 0 and {chunkSize / 2}, got {chunkOverlap}");
}

      if (errors.Any())
        {
     return AgentResult.CreateFailure("Invalid chunking parameters", errors);
 }

      return AgentResult.CreateSuccess("Parameters validated");
    }

    private List<string> ChunkText(string text, int chunkSize, int overlap)
   {
  var chunks = new List<string>();

   if (string.IsNullOrWhiteSpace(text))
  {
  return chunks;
  }

        // Split text into sentences first to preserve sentence boundaries
  var sentences = SplitIntoSentences(text);
        
  var currentChunk = new List<string>();
var currentLength = 0;

        for (int i = 0; i < sentences.Count; i++)
    {
var sentence = sentences[i];
      var sentenceLength = sentence.Length;

  // If adding this sentence would exceed chunk size, finalize current chunk
    if (currentLength > 0 && currentLength + sentenceLength > chunkSize)
     {
      var chunkText = string.Join(" ", currentChunk).Trim();
   if (!string.IsNullOrWhiteSpace(chunkText))
          {
        chunks.Add(chunkText);
               }

         // Start new chunk with overlap
  var overlapText = GetOverlapText(chunkText, overlap);
   currentChunk = new List<string>();
       currentLength = 0;

       if (!string.IsNullOrWhiteSpace(overlapText))
      {
       currentChunk.Add(overlapText);
              currentLength = overlapText.Length;
           }
     }

 // Add current sentence to chunk
    currentChunk.Add(sentence);
     currentLength += sentenceLength + (currentChunk.Count > 1 ? 1 : 0); // +1 for space
        }

        // Add final chunk
     if (currentChunk.Any())
   {
      var chunkText = string.Join(" ", currentChunk).Trim();
      if (!string.IsNullOrWhiteSpace(chunkText))
     {
      chunks.Add(chunkText);
    }
      }

  return chunks;
  }

    private List<string> SplitIntoSentences(string text)
   {
  // Split on sentence boundaries while preserving the delimiter
      var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])";
   var sentences = Regex.Split(text, sentencePattern)
   .Where(s => !string.IsNullOrWhiteSpace(s))
    .Select(s => s.Trim())
 .ToList();

        return sentences;
    }

  private string GetOverlapText(string text, int overlap)
    {
      if (overlap <= 0 || string.IsNullOrWhiteSpace(text))
 {
       return string.Empty;
        }

    // Take last 'overlap' characters, but try to break at word boundaries
    if (text.Length <= overlap)
 {
  return text;
        }

        var overlapText = text.Substring(text.Length - overlap);
       var lastSpaceIndex = overlapText.IndexOf(' ');

 if (lastSpaceIndex > 0)
      {
   overlapText = overlapText.Substring(lastSpaceIndex + 1);
 }

        return overlapText.Trim();
  }
}