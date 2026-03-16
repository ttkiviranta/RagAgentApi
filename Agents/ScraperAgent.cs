using RagAgentApi.Models;
using RagAgentApi.Services;
using HtmlAgilityPack;
using System.Text;

namespace RagAgentApi.Agents;

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
                return AgentResult.CreateFailure("URL not found in context state");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return AgentResult.CreateFailure($"Invalid URL format: {url}");

            var crawlDepth = context.State.TryGetValue("crawl_depth", out var depthObj) && depthObj is int depth ? depth : 0;
            var maxPages = context.State.TryGetValue("max_pages", out var maxObj) && maxObj is int max ? max : 10;
            var sameDomainOnly = !context.State.TryGetValue("same_domain_only", out var sameObj) || sameObj is not bool same || same;

            _logger.LogInformation("[ScraperAgent] Scraping {Url}, Depth={Depth}, MaxPages={MaxPages}", url, crawlDepth, maxPages);

            if (crawlDepth > 0)
            {
                var scrapedPages = await ScrapeWithDepthAsync(url, crawlDepth, maxPages, sameDomainOnly, cancellationToken);

                if (!scrapedPages.Any())
                    return AgentResult.CreateFailure($"No content found from {url} or linked pages");

                var combinedContent = string.Join("\n\n---PAGE BREAK---\n\n",
                    scrapedPages.Select(p => $"# {p.Title}\nSource: {p.Url}\n\n{p.Content}"));

                context.State["raw_content"] = combinedContent;
                context.State["source_url"] = url;
                context.State["pages_scraped"] = scrapedPages.Count;
                context.State["scraped_urls"] = scrapedPages.Select(p => p.Url).ToList();

                stopwatch.Stop();
                LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

                AddMessage(context, "ChunkerAgent", $"Content scraped ({scrapedPages.Count} pages, {combinedContent.Length} chars)");

                return AgentResult.CreateSuccess("Content scraped successfully", new()
                {
                    { "content_length", combinedContent.Length },
                    { "pages_scraped", scrapedPages.Count }
                });
            }

            // Single-page scrape
            var singleContent = await ScrapeSinglePageAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(singleContent))
                return AgentResult.CreateFailure("No meaningful content found after cleaning.");

            context.State["raw_content"] = singleContent;
            context.State["source_url"] = url;

            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

            AddMessage(context, "ChunkerAgent", $"Content scraped ({singleContent.Length} chars)");

            return AgentResult.CreateSuccess("Content scraped successfully", new()
            {
                { "content_length", singleContent.Length }
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
            return HandleException(ex, context.ThreadId, "ScraperAgent");
        }
    }

    // -------------------------------
    // SINGLE PAGE SCRAPE
    // -------------------------------
    private async Task<string> ScrapeSinglePageAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            PrepareHeaders();

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("[ScraperAgent] Single scrape: Status={Status}, Type={Type}, Length={Length}",
                response.StatusCode, contentType, html.Length);

            if (!contentType.Contains("html"))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            RemoveUnwantedNodes(doc);

            var content = CleanText(ExtractTextContent(doc.DocumentNode));

            if (!string.IsNullOrWhiteSpace(content) && content.Length >= 20)
                return content;

            // Playwright fallback
            _logger.LogInformation("[ScraperAgent] Trying Playwright fallback for {Url}", url);

            var pw = await _playwrightService.ScrapeAsync(url, 3000, cancellationToken);
            if (pw.Success && !string.IsNullOrWhiteSpace(pw.Content))
                return pw.Content;

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ScraperAgent] Single page scrape failed for {Url}", url);
            return string.Empty;
        }
    }

    // -------------------------------
    // DEPTH SCRAPE (SMART FALLBACK)
    // -------------------------------
    public async Task<List<ScrapedPage>> ScrapeWithDepthAsync(
        string startUrl,
        int depth,
        int maxPages,
        bool sameDomainOnly,
        CancellationToken cancellationToken = default)
    {
        var scrapedPages = new List<ScrapedPage>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Url, int Level)>();

        queue.Enqueue((startUrl, 0));

        while (queue.Count > 0 && scrapedPages.Count < maxPages)
        {
            var (url, level) = queue.Dequeue();
            if (visited.Contains(url))
                continue;

            visited.Add(url);

            try
            {
                PrepareHeaders();

                var response = await _httpClient.GetAsync(url, cancellationToken);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("[ScraperAgent] Depth={Level} Status={Status} Type={Type} Length={Length} URL={Url}",
                    level, response.StatusCode, contentType, html.Length, url);

                if (!contentType.Contains("html"))
                {
                    _logger.LogWarning("[ScraperAgent] Skipping non-HTML content at {Url}", url);
                    continue;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                RemoveUnwantedNodes(doc);

                var content = CleanText(ExtractTextContent(doc.DocumentNode));

                // SMART FALLBACK
                if (string.IsNullOrWhiteSpace(content) || content.Length < 20)
                {
                    _logger.LogInformation("[ScraperAgent] Low-content page, trying Playwright for {Url}", url);

                    var pw = await _playwrightService.ScrapeAsync(url, 3000, cancellationToken);
                    if (pw.Success && !string.IsNullOrWhiteSpace(pw.Content))
                        content = pw.Content;
                }

                if (!string.IsNullOrWhiteSpace(content) && content.Length >= 5)
                {
                    scrapedPages.Add(new ScrapedPage
                    {
                        Url = url,
                        Content = content,
                        Title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? url,
                        ScrapedAt = DateTime.UtcNow,
                        Depth = level
                    });
                }

                if (level < depth && scrapedPages.Count < maxPages)
                {
                    var links = ExtractLinks(html, url, sameDomainOnly);
                    foreach (var link in links.Take(maxPages - scrapedPages.Count))
                        if (!visited.Contains(link))
                            queue.Enqueue((link, level + 1));
                }

                await Task.Delay(200, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ScraperAgent] Failed to scrape {Url}", url);
            }
        }

        return scrapedPages;
    }

    // -------------------------------
    // HELPERS
    // -------------------------------
    private void PrepareHeaders()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "fi-FI,fi;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
    }

    private void RemoveUnwantedNodes(HtmlDocument doc)
    {
        foreach (var selector in new[] { "//script", "//style", "//noscript" })
        {
            var nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes != null)
                foreach (var node in nodes)
                    node.Remove();
        }
    }

    private string ExtractTextContent(HtmlNode node)
    {
        var sb = new StringBuilder();

        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = node.InnerText.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                sb.Append(text).Append(' ');
        }
        else
        {
            foreach (var child in node.ChildNodes)
                sb.Append(ExtractTextContent(child));
        }

        return sb.ToString();
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n[ \t]*\n", "\n\n");

        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 3)
            .ToList();

        return string.Join("\n", lines);
    }

    public List<string> ExtractLinks(string html, string baseUrl, bool sameDomainOnly)
    {
        var links = new List<string>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                return links;

            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors == null)
                return links;

            foreach (var a in anchors)
            {
                var href = a.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                if (href.StartsWith("#") ||
                    href.StartsWith("javascript:") ||
                    href.EndsWith(".pdf") ||
                    href.EndsWith(".jpg") ||
                    href.EndsWith(".png"))
                    continue;

                Uri? abs;
                if (href.StartsWith("http"))
                    abs = new Uri(href);
                else
                    abs = new Uri(baseUri, href);

                if (sameDomainOnly && abs.Host != baseUri.Host)
                    continue;

                var normalized = abs.GetLeftPart(UriPartial.Path);
                if (!links.Contains(normalized))
                    links.Add(normalized);
            }
        }
        catch { }

        return links;
    }
}

public class ScrapedPage
{
    public string Url { get; set; } = "";
    public string Content { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime ScrapedAt { get; set; }
    public int Depth { get; set; }
}