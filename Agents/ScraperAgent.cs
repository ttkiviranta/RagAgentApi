using RagAgentApi.Models;
using HtmlAgilityPack;
using System.Text;

namespace RagAgentApi.Agents;

/// <summary>
/// Scrapes content from web pages and cleans HTML
/// </summary>
public class ScraperAgent : BaseRagAgent
{
    private readonly HttpClient _httpClient;

    public ScraperAgent(HttpClient httpClient, ILogger<ScraperAgent> logger) : base(logger)
    {
        _httpClient = httpClient;
    }

 public override string Name => "ScraperAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
 {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
  LogExecutionStart(context.ThreadId);

    try
        {
        if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
      {
      return AgentResult.CreateFailure("URL not found in context state");
   }

     if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
   {
        return AgentResult.CreateFailure($"Invalid URL format: {url}");
       }

_logger.LogInformation("[ScraperAgent] Scraping content from {Url}", url);

   // Set User-Agent to avoid blocking
   _httpClient.DefaultRequestHeaders.Clear();
    _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

     // Download HTML content
       var html = await _httpClient.GetStringAsync(uri, cancellationToken);
        _logger.LogDebug("[ScraperAgent] Downloaded {Length} chars of HTML from {Url}", html.Length, url);

       // Parse and clean HTML
       var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove unwanted elements but be less aggressive
     var unwantedSelectors = new[] { "//script", "//style", "//noscript" };
        foreach (var selector in unwantedSelectors)
  {
   var nodes = doc.DocumentNode.SelectNodes(selector);
        if (nodes != null)
     {
       foreach (var node in nodes)
          {
        node.Remove();
        }
   }
        }

        // Extract text content with multiple strategies
            var textContent = ExtractTextContent(doc.DocumentNode);
      var cleanedContent = CleanText(textContent);

         _logger.LogDebug("[ScraperAgent] Extracted {Length} chars after cleaning", cleanedContent.Length);

        // If still no content, try alternative extraction methods
 if (string.IsNullOrWhiteSpace(cleanedContent) || cleanedContent.Length < 50)
         {
            _logger.LogWarning("[ScraperAgent] Primary extraction yielded minimal content, trying alternatives");
         
       // Try extracting from common content elements using proper XPath
     var contentSelectors = new[] { 
         "//main", 
        "//article", 
        "//*[contains(@class,'content')]", 
 "//*[@id='content']", 
   "//*[contains(@class,'post')]", 
  "//*[contains(@class,'entry')]",
   "//div[contains(@class,'main')]"
         };

      foreach (var selector in contentSelectors)
     {
      try
        {
     var contentNodes = doc.DocumentNode.SelectNodes(selector);
        if (contentNodes != null)
     {
      foreach (var node in contentNodes)
  {
      var altContent = CleanText(ExtractTextContent(node));
         if (!string.IsNullOrWhiteSpace(altContent) && altContent.Length > cleanedContent.Length)
        {
  cleanedContent = altContent;
            _logger.LogDebug("[ScraperAgent] Found better content using selector: {Selector}, length: {Length}", 
    selector, altContent.Length);
      break;
  }
      }
    if (!string.IsNullOrWhiteSpace(cleanedContent) && cleanedContent.Length > 50)
      break;
   }
      }
       catch (Exception ex)
   {
      _logger.LogDebug("[ScraperAgent] Selector {Selector} failed: {Error}", selector, ex.Message);
       continue;
   }
  }
       }

// Final fallback - extract everything from body
        if (string.IsNullOrWhiteSpace(cleanedContent) || cleanedContent.Length < 50)
      {
      var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
      if (bodyNode != null)
     {
       cleanedContent = CleanText(bodyNode.InnerText);
        _logger.LogDebug("[ScraperAgent] Used body fallback extraction: {Length} chars", cleanedContent.Length);
         }
       }

      if (string.IsNullOrWhiteSpace(cleanedContent) || cleanedContent.Length < 10)
      {
       _logger.LogWarning("[ScraperAgent] No meaningful content found from {Url}. HTML length: {HtmlLength}, Final content length: {ContentLength}", 
        url, html.Length, cleanedContent?.Length ?? 0);
       return AgentResult.CreateFailure($"No meaningful content found after cleaning. URL may contain dynamic content or be blocked.");
}

  // Store in context state
 context.State["raw_content"] = cleanedContent;
    context.State["source_url"] = url;

    stopwatch.Stop();
     LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

        _logger.LogInformation("[ScraperAgent] Scraped {Length} chars from {Url}", 
    cleanedContent.Length, url);

       AddMessage(context, "ChunkerAgent", $"Content scraped successfully ({cleanedContent.Length} characters)", 
           new Dictionary<string, object>
        {
        { "content_length", cleanedContent.Length },
        { "source_url", url }
   });

            return AgentResult.CreateSuccess(
    "Content scraped successfully",
        new Dictionary<string, object>
        {
     { "content_length", cleanedContent.Length },
            { "source_url", url }
 });
      }
 catch (HttpRequestException ex)
        {
     stopwatch.Stop();
  LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
 return HandleException(ex, context.ThreadId, "HTTP request");
        }
    catch (Exception ex)
   {
     stopwatch.Stop();
     LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
  return HandleException(ex, context.ThreadId, "content scraping");
        }
    }

 private string ExtractTextContent(HtmlNode node)
    {
   var sb = new StringBuilder();

     if (node.NodeType == HtmlNodeType.Text)
        {
       var text = node.InnerText.Trim();
      if (!string.IsNullOrWhiteSpace(text))
       {
         sb.Append(text).Append(' ');
            }
        }
      else if (node.NodeType == HtmlNodeType.Element)
   {
            foreach (var child in node.ChildNodes)
    {
   sb.Append(ExtractTextContent(child));
 }

    // Add line breaks for block elements
    if (IsBlockElement(node.Name))
  {
 sb.AppendLine();
      }
     }

  return sb.ToString();
    }

    private bool IsBlockElement(string tagName)
   {
  var blockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
    "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "article", "section", 
            "blockquote", "pre", "ul", "ol", "li", "table", "tr", "td", "th"
       };

return blockElements.Contains(tagName);
  }

    private string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
     return string.Empty;

     // Decode HTML entities
    text = System.Net.WebUtility.HtmlDecode(text);

  // Remove excessive whitespace but preserve some structure
      text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
     text = System.Text.RegularExpressions.Regex.Replace(text, @"\n[ \t]*\n", "\n\n");
       text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");

     // Remove lines that are just whitespace
   var lines = text.Split('\n');
    var cleanedLines = lines.Where(line => !string.IsNullOrWhiteSpace(line.Trim())).ToList();

       // Filter out very short lines (likely navigation elements)
        var meaningfulLines = cleanedLines.Where(line => 
   line.Trim().Length > 3 && 
      !IsNavigationText(line.Trim())).ToList();

        if (meaningfulLines.Any())
        {
   return string.Join("\n", meaningfulLines).Trim();
     }

  // Fallback to all non-empty lines if filtering was too aggressive
  return string.Join("\n", cleanedLines).Trim();
 }

 private bool IsNavigationText(string text)
    {
    var navIndicators = new[] { "menu", "nav", "click", "home", "contact", "about", "login", "register", "search", "©", "cookie" };
        return text.Length < 30 && navIndicators.Any(indicator => 
      text.ToLowerInvariant().Contains(indicator));
    }
}