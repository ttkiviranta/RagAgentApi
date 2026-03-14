using RagAgentApi.Models;
using RagAgentApi.Services;
using HtmlAgilityPack;
using System.Text;

namespace RagAgentApi.Agents;

/// <summary>
/// Scrapes content from web pages and cleans HTML.
/// Uses standard HTTP for simple sites, falls back to Playwright for JavaScript-heavy sites.
/// </summary>
public class ScraperAgent : BaseRagAgent
{
    private readonly HttpClient _httpClient;
    private readonly IPlaywrightScraperService _playwrightService;
    private const string HttpClientName = "ScraperAgent";

    public ScraperAgent(
        IHttpClientFactory httpClientFactory, 
        ILogger<ScraperAgent> logger,
        IPlaywrightScraperService playwrightService) : base(logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _playwrightService = playwrightService;
        _logger.LogInformation("[ScraperAgent] Initialized with Playwright support");
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

            // Get crawl parameters from context
            var crawlDepth = context.State.TryGetValue("crawl_depth", out var depthObj) && depthObj is int depth ? depth : 0;
            var maxPages = context.State.TryGetValue("max_pages", out var maxObj) && maxObj is int max ? max : 10;
            var sameDomainOnly = !context.State.TryGetValue("same_domain_only", out var sameObj) || sameObj is not bool same || same;

            _logger.LogInformation("[ScraperAgent] Scraping content from {Url}, Depth: {Depth}, MaxPages: {MaxPages}", url, crawlDepth, maxPages);

                        // Check if we should use Playwright (for JavaScript-heavy sites)
                        var usePlaywright = context.State.TryGetValue("use_playwright", out var pwObj) && pwObj is bool pw && pw;

                                                // Use depth-aware scraping if crawl depth > 0
                                                if (crawlDepth > 0)
                                                {
                                                    List<ScrapedPage> scrapedPages;

                                                    if (usePlaywright)
                                                    {
                                                        _logger.LogInformation("[ScraperAgent] Using Playwright for JavaScript rendering (explicit)");
                                                        var playwrightResults = await _playwrightService.ScrapeWithDepthAsync(url, crawlDepth, maxPages, sameDomainOnly, cancellationToken);
                                                        scrapedPages = playwrightResults
                                                            .Where(r => r.Success)
                                                            .Select(r => new ScrapedPage
                                                            {
                                                                Url = r.Url,
                                                                Title = r.Title ?? r.Url,
                                                                Content = r.Content ?? string.Empty,
                                                                ScrapedAt = r.ScrapedAt,
                                                                Depth = r.Depth
                                                            })
                                                            .ToList();
                                                    }
                                                    else
                                                    {
                                                        scrapedPages = await ScrapeWithDepthAsync(url, crawlDepth, maxPages, sameDomainOnly, cancellationToken);

                                                        // Fallback to Playwright if standard scraping found nothing
                                                        if (!scrapedPages.Any())
                                                        {
                                                            _logger.LogInformation("[ScraperAgent] Standard scraping failed, trying Playwright fallback");
                                                            var playwrightResults = await _playwrightService.ScrapeWithDepthAsync(url, crawlDepth, maxPages, sameDomainOnly, cancellationToken);
                                                            scrapedPages = playwrightResults
                                                                .Where(r => r.Success)
                                                                .Select(r => new ScrapedPage
                                                                {
                                                                    Url = r.Url,
                                                                    Title = r.Title ?? r.Url,
                                                                    Content = r.Content ?? string.Empty,
                                                                    ScrapedAt = r.ScrapedAt,
                                                                    Depth = r.Depth
                                                                })
                                                                .ToList();
                                                        }
                                                    }

                            if (!scrapedPages.Any())
                {
                    return AgentResult.CreateFailure($"No content found from {url} or linked pages");
                }

                // Combine all scraped content
                var combinedContent = string.Join("\n\n---PAGE BREAK---\n\n", 
                    scrapedPages.Select(p => $"# {p.Title}\nSource: {p.Url}\n\n{p.Content}"));

                context.State["raw_content"] = combinedContent;
                context.State["source_url"] = url;
                context.State["pages_scraped"] = scrapedPages.Count;
                context.State["scraped_urls"] = scrapedPages.Select(p => p.Url).ToList();

                stopwatch.Stop();
                LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

                _logger.LogInformation("[ScraperAgent] Scraped {PageCount} pages, {Length} total chars from {Url}", 
                    scrapedPages.Count, combinedContent.Length, url);

                AddMessage(context, "ChunkerAgent", $"Content scraped successfully ({scrapedPages.Count} pages, {combinedContent.Length} characters)", 
                    new Dictionary<string, object>
                    {
                        { "content_length", combinedContent.Length },
                        { "source_url", url },
                        { "pages_scraped", scrapedPages.Count }
                    });

                return AgentResult.CreateSuccess(
                    $"Content scraped successfully from {scrapedPages.Count} pages",
                    new Dictionary<string, object>
                    {
                        { "content_length", combinedContent.Length },
                        { "source_url", url },
                        { "pages_scraped", scrapedPages.Count }
                    });
            }

            // Single page scraping (original logic)
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "fi-FI,fi;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

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

      // If still no content, try Playwright for JavaScript-rendered sites
      if (string.IsNullOrWhiteSpace(cleanedContent) || cleanedContent.Length < 20)
      {
          _logger.LogInformation("[ScraperAgent] No content from HTTP scraping, trying Playwright for JavaScript rendering");
          try
          {
              var playwrightResult = await _playwrightService.ScrapeAsync(url, 3000, cancellationToken);
              if (playwrightResult.Success && !string.IsNullOrWhiteSpace(playwrightResult.Content))
              {
                  cleanedContent = playwrightResult.Content;
                  _logger.LogInformation("[ScraperAgent] Playwright extracted {Length} chars from {Url}", 
                      cleanedContent.Length, url);
              }
          }
          catch (Exception pwEx)
          {
              _logger.LogWarning(pwEx, "[ScraperAgent] Playwright fallback failed for {Url}", url);
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

    /// <summary>
    /// Extract all links from the page that are on the same domain
    /// </summary>
    public List<string> ExtractLinks(string html, string baseUrl, bool sameDomainOnly = true)
    {
        var links = new List<string>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                return links;

            var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchorNodes == null)
                return links;

            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var anchor in anchorNodes)
            {
                var href = anchor.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Skip non-content links
                if (href.StartsWith("#") || 
                    href.StartsWith("javascript:") || 
                    href.StartsWith("mailto:") ||
                    href.StartsWith("tel:") ||
                    href.EndsWith(".pdf") ||
                    href.EndsWith(".jpg") ||
                    href.EndsWith(".png") ||
                    href.EndsWith(".gif") ||
                    href.EndsWith(".zip") ||
                    href.EndsWith(".exe"))
                    continue;

                // Resolve relative URLs
                Uri? absoluteUri;
                if (href.StartsWith("http://") || href.StartsWith("https://"))
                {
                    if (!Uri.TryCreate(href, UriKind.Absolute, out absoluteUri))
                        continue;
                }
                else
                {
                    if (!Uri.TryCreate(baseUri, href, out absoluteUri))
                        continue;
                }

                // Check same domain if required
                if (sameDomainOnly && !string.Equals(absoluteUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                    continue;

                var normalizedUrl = absoluteUri.GetLeftPart(UriPartial.Path);

                // Avoid duplicates
                if (seenUrls.Contains(normalizedUrl))
                    continue;

                seenUrls.Add(normalizedUrl);
                links.Add(normalizedUrl);
            }

            _logger.LogDebug("[ScraperAgent] Found {Count} unique links on {Url}", links.Count, baseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ScraperAgent] Error extracting links from {Url}", baseUrl);
        }

        return links;
    }

    /// <summary>
    /// Scrape a URL and optionally follow links up to a certain depth
    /// </summary>
    public async Task<List<ScrapedPage>> ScrapeWithDepthAsync(
        string startUrl, 
        int depth, 
        int maxPages, 
        bool sameDomainOnly,
        CancellationToken cancellationToken = default)
    {
        var scrapedPages = new List<ScrapedPage>();
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urlsToVisit = new Queue<(string Url, int CurrentDepth)>();

        urlsToVisit.Enqueue((startUrl, 0));

        while (urlsToVisit.Count > 0 && scrapedPages.Count < maxPages)
        {
            var (url, currentDepth) = urlsToVisit.Dequeue();

            if (visitedUrls.Contains(url))
                continue;

            visitedUrls.Add(url);

            try
            {
                _logger.LogInformation("[ScraperAgent] Crawling {Url} (depth {Depth}/{MaxDepth})", 
                    url, currentDepth, depth);

                // Download HTML with full browser-like headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                _httpClient.DefaultRequestHeaders.Add("Accept-Language", "fi-FI,fi;q=0.9,en;q=0.8");
                _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                var html = await _httpClient.GetStringAsync(url, cancellationToken);
                _logger.LogDebug("[ScraperAgent] Downloaded {Length} chars from {Url}", html.Length, url);

                // Parse and extract content
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Remove unwanted elements
                foreach (var selector in new[] { "//script", "//style", "//noscript" })
                {
                    var nodes = doc.DocumentNode.SelectNodes(selector);
                    if (nodes != null)
                    {
                        foreach (var node in nodes)
                            node.Remove();
                    }
                }

                var content = CleanText(ExtractTextContent(doc.DocumentNode));
                _logger.LogDebug("[ScraperAgent] Extracted {Length} chars of text from {Url}", content?.Length ?? 0, url);

                // Lower threshold for JavaScript-heavy sites - even 20 chars is meaningful
                if (!string.IsNullOrWhiteSpace(content) && content.Length >= 20)
                {
                    scrapedPages.Add(new ScrapedPage
                    {
                        Url = url,
                        Content = content,
                        Title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? url,
                        ScrapedAt = DateTime.UtcNow,
                        Depth = currentDepth
                    });

                    _logger.LogInformation("[ScraperAgent] Scraped {Length} chars from {Url}", 
                        content.Length, url);
                }

                // Extract links for next depth level
                if (currentDepth < depth && scrapedPages.Count < maxPages)
                {
                    var links = ExtractLinks(html, url, sameDomainOnly);
                    foreach (var link in links.Take(maxPages - scrapedPages.Count))
                    {
                        if (!visitedUrls.Contains(link))
                        {
                            urlsToVisit.Enqueue((link, currentDepth + 1));
                        }
                    }
                }

                // Small delay to be respectful
                await Task.Delay(200, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ScraperAgent] Failed to scrape {Url}", url);
            }
        }

        _logger.LogInformation("[ScraperAgent] Crawl complete: {Count} pages scraped", scrapedPages.Count);
        return scrapedPages;
    }
}

/// <summary>
/// Represents a single scraped page
/// </summary>
public class ScrapedPage
{
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; }
    public int Depth { get; set; }
}