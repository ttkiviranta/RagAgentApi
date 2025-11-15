using RagAgentApi.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RagAgentApi.Agents;

/// <summary>
/// Specialized agent for YouTube videos
/// Uses YoutubeExplode library to extract video metadata and transcripts
/// </summary>
public class YouTubeTranscriptAgent : BaseRagAgent
{
private readonly HttpClient _httpClient;

  public YouTubeTranscriptAgent(HttpClient httpClient, ILogger<YouTubeTranscriptAgent> logger) : base(logger)
    {
        _httpClient = httpClient;
    }

    public override string Name => "YouTubeTranscriptAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
 var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      LogExecutionStart(context.ThreadId);

        try
        {
            // Validate URL is YouTube
            if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
            {
  return AgentResult.CreateFailure("URL not found in context state");
      }

 if (!IsYouTubeUrl(url))
        {
                return AgentResult.CreateFailure($"URL {url} is not a YouTube URL");
       }

            _logger.LogInformation("[YouTubeTranscriptAgent] Processing YouTube URL: {Url}", url);

          // TODO: Implement YouTube transcript extraction
     // - Use YoutubeExplode library for video metadata
       // - Extract video title, description, duration, view count
          // - Fetch auto-generated or manual transcripts/captions
   // - Handle multiple language tracks
            // - Extract timestamps for transcript segments
            // - Support playlists and channels

            var videoInfo = ParseYouTubeUrl(url);
            var placeholderContent = GeneratePlaceholderContent(url, videoInfo);

    // Store extracted content in context
            context.State["raw_content"] = placeholderContent;
   context.State["source_url"] = url;
      context.State["content_type"] = "youtube";
      context.State["youtube_metadata"] = JsonDocument.Parse(JsonSerializer.Serialize(videoInfo));

     stopwatch.Stop();
          LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

       _logger.LogInformation("[YouTubeTranscriptAgent] Processed YouTube URL: {Url} (ID: {VideoId})", 
      url, videoInfo.VideoId);

            AddMessage(context, "ChunkerAgent", 
  $"YouTube content extracted successfully (Video ID: {videoInfo.VideoId})",
        new Dictionary<string, object>
      {
      { "url", url },
           { "video_id", videoInfo.VideoId },
    { "playlist_id", videoInfo.PlaylistId ?? "" },
    { "content_length", placeholderContent.Length }
       });

            return AgentResult.CreateSuccess(
        "YouTube content extracted successfully (PLACEHOLDER)",
   new Dictionary<string, object>
                {
 { "url", url },
    { "video_id", videoInfo.VideoId },
   { "playlist_id", videoInfo.PlaylistId ?? "" },
             { "content_length", placeholderContent.Length },
        { "extraction_time_ms", stopwatch.ElapsedMilliseconds }
   });
        }
        catch (Exception ex)
      {
            stopwatch.Stop();
 LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
     return HandleException(ex, context.ThreadId, "YouTube transcript processing");
        }
    }

    private static bool IsYouTubeUrl(string url)
    {
      return url.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase) ||
    url.Contains("m.youtube.com/", StringComparison.OrdinalIgnoreCase);
    }

  private static YouTubeUrlInfo ParseYouTubeUrl(string url)
    {
        var info = new YouTubeUrlInfo();

        // Extract video ID from various YouTube URL formats
    var videoIdPatterns = new[]
   {
            @"(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})",
     @"youtube\.com/embed/([a-zA-Z0-9_-]{11})",
            @"youtube\.com/v/([a-zA-Z0-9_-]{11})"
        };

  foreach (var pattern in videoIdPatterns)
 {
       var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
     if (match.Success)
      {
  info.VideoId = match.Groups[1].Value;
              break;
     }
        }

        // Extract playlist ID if present
        var playlistMatch = Regex.Match(url, @"[&?]list=([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
        if (playlistMatch.Success)
   {
            info.PlaylistId = playlistMatch.Groups[1].Value;
      }

        // Extract timestamp if present
        var timestampMatch = Regex.Match(url, @"[&?]t=(\d+)", RegexOptions.IgnoreCase);
if (timestampMatch.Success && int.TryParse(timestampMatch.Groups[1].Value, out var timestamp))
      {
       info.StartTime = timestamp;
        }

  return info;
    }

    private static string GeneratePlaceholderContent(string url, YouTubeUrlInfo videoInfo)
    {
   return $@"# YouTube Video Analysis

**URL**: {url}
**Video ID**: {videoInfo.VideoId}
{(string.IsNullOrEmpty(videoInfo.PlaylistId) ? "" : $"**Playlist ID**: {videoInfo.PlaylistId}")}
{(videoInfo.StartTime.HasValue ? $"**Start Time**: {videoInfo.StartTime}s" : "")}

## TODO: YouTube Transcript Integration

This is a placeholder for YouTube transcript extraction. When implemented, this agent will:

1. **Video Metadata Extraction**:
   - Title, description, duration, view count
   - Channel information (name, subscriber count)
   - Publication date, tags, category
   - Thumbnail URLs and video quality options

2. **Transcript/Caption Processing**:
   - Auto-generated transcripts (when available)
   - Manual/professional captions in multiple languages
   - Timestamp-aligned text segments
   - Speaker identification (when available)

3. **Content Analysis**:
   - Extract key topics and themes
   - Identify important timestamps and segments
   - Generate chapter markers from transcript
   - Extract mentioned links and resources

4. **Advanced Features**:
   - Support for live stream transcripts
   - Playlist processing (multiple videos)
   - Comment extraction and analysis
   - Integration with YouTube Data API v3

## Implementation Plan

- Install YoutubeExplode NuGet package
- Add YouTube Data API integration
- Implement transcript chunking by time segments
- Add support for multiple language tracks
- Handle rate limiting and quota management
- Extract and preserve timestamp information for citations

This placeholder allows the RAG system to understand that this URL points to a YouTube video
and provides basic video identification for processing.

**Video URL**: {url}
**Extracted Video ID**: {videoInfo.VideoId ?? "Unable to extract"}

Note: Actual video content and transcript will be available once YoutubeExplode integration is implemented.";
    }

  private class YouTubeUrlInfo
    {
    public string VideoId { get; set; } = "";
        public string? PlaylistId { get; set; }
        public int? StartTime { get; set; }
    }
}