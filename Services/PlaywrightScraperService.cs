using Microsoft.Playwright;
using HtmlAgilityPack;
using System.Text;

namespace RagAgentApi.Services;

/// <summary>
/// Service for scraping JavaScript-rendered websites using Playwright headless browser
/// </summary>
public interface IPlaywrightScraperService
{
    Task<PlaywrightScrapeResult> ScrapeAsync(string url, int waitMs = 3000, CancellationToken cancellationToken = default);
    Task<List<PlaywrightScrapeResult>> ScrapeWithDepthAsync(string startUrl, int depth, int maxPages, bool sameDomainOnly, CancellationToken cancellationToken = default);
    Task InitializeAsync();
    Task DisposeAsync();
}

public class PlaywrightScraperService : IPlaywrightScraperService, IAsyncDisposable
{
    private readonly ILogger<PlaywrightScraperService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public PlaywrightScraperService(ILogger<PlaywrightScraperService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _logger.LogInformation("[PlaywrightScraperService] Initializing Playwright...");
            
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
            });

            _initialized = true;
            _logger.LogInformation("[PlaywrightScraperService] Playwright initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlaywrightScraperService] Failed to initialize Playwright. Run 'pwsh bin/Debug/net8.0/playwright.ps1 install' to install browsers.");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<PlaywrightScrapeResult> ScrapeAsync(string url, int waitMs = 2000, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        var result = new PlaywrightScrapeResult { Url = url, ScrapedAt = DateTime.UtcNow };

        try
        {
            _logger.LogInformation("[PlaywrightScraperService] Scraping {Url} with Playwright", url);

            var page = await _browser!.NewPageAsync();

            try
            {
                // Set viewport and user agent
                await page.SetViewportSizeAsync(1920, 1080);

                // Navigate with faster load strategy (don't wait for all network to be idle)
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded, // Faster than NetworkIdle
                    Timeout = 30000 // 30 seconds max per page
                });

                if (response == null || !response.Ok)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to load page: {response?.Status}";
                    return result;
                }

                // Wait for dynamic content
                await page.WaitForTimeoutAsync(waitMs);

                // Get title
                result.Title = await page.TitleAsync();

                // Get full HTML after JavaScript execution
                var html = await page.ContentAsync();
                result.HtmlLength = html.Length;

                _logger.LogDebug("[PlaywrightScraperService] Got {Length} chars of rendered HTML from {Url}", html.Length, url);

                // Extract text content
                result.Content = ExtractTextContent(html);
                result.Success = !string.IsNullOrWhiteSpace(result.Content) && result.Content.Length > 50;

                // Extract links for crawling
                result.Links = await ExtractLinksAsync(page, url);

                _logger.LogInformation("[PlaywrightScraperService] Scraped {ContentLength} chars of text from {Url}", 
                    result.Content?.Length ?? 0, url);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlaywrightScraperService] Error scraping {Url}", url);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<List<PlaywrightScrapeResult>> ScrapeWithDepthAsync(
        string startUrl, 
        int depth, 
        int maxPages, 
        bool sameDomainOnly,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        var results = new List<PlaywrightScrapeResult>();
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urlsToVisit = new Queue<(string Url, int CurrentDepth)>();
        var startTime = DateTime.UtcNow;
        var maxTotalTime = TimeSpan.FromMinutes(5); // Max 5 minutes total for all pages

        urlsToVisit.Enqueue((startUrl, 0));

        while (urlsToVisit.Count > 0 && results.Count < maxPages && !cancellationToken.IsCancellationRequested)
        {
            // Check total time limit
            if (DateTime.UtcNow - startTime > maxTotalTime)
            {
                _logger.LogWarning("[PlaywrightScraperService] Total time limit reached ({Minutes} min), stopping crawl with {Count} pages", 
                    maxTotalTime.TotalMinutes, results.Count);
                break;
            }

            var (url, currentDepth) = urlsToVisit.Dequeue();

            if (visitedUrls.Contains(url))
                continue;

            visitedUrls.Add(url);

            _logger.LogInformation("[PlaywrightScraperService] Crawling {Url} (depth {Depth}/{MaxDepth}, {Count}/{MaxPages} pages)", 
                url, currentDepth, depth, results.Count, maxPages);

            var result = await ScrapeAsync(url, 2000, cancellationToken);
            result.Depth = currentDepth;

            if (result.Success)
            {
                results.Add(result);

                // Add links for next depth level
                if (currentDepth < depth && results.Count < maxPages && result.Links != null)
                {
                    var baseUri = new Uri(url);
                    foreach (var link in result.Links.Take(maxPages - results.Count))
                    {
                        if (visitedUrls.Contains(link))
                            continue;

                        // Check same domain if required
                        if (sameDomainOnly && Uri.TryCreate(link, UriKind.Absolute, out var linkUri))
                        {
                            if (!string.Equals(linkUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        urlsToVisit.Enqueue((link, currentDepth + 1));
                    }
                }
            }

            // Respectful delay between requests
            await Task.Delay(500, cancellationToken);
        }

        _logger.LogInformation("[PlaywrightScraperService] Crawl complete: {Count} pages scraped", results.Count);
        return results;
    }

    private async Task<List<string>> ExtractLinksAsync(IPage page, string baseUrl)
    {
        var links = new List<string>();

        try
        {
            var baseUri = new Uri(baseUrl);
            var hrefs = await page.EvaluateAsync<string[]>(
                "() => Array.from(document.querySelectorAll('a[href]')).map(a => a.href)");

            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var href in hrefs ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Skip non-content links
                if (href.StartsWith("javascript:") || 
                    href.StartsWith("mailto:") ||
                    href.StartsWith("tel:") ||
                    href.Contains("#") ||
                    href.EndsWith(".pdf") ||
                    href.EndsWith(".jpg") ||
                    href.EndsWith(".png"))
                    continue;

                if (!Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri))
                    continue;

                var normalizedUrl = absoluteUri.GetLeftPart(UriPartial.Path);

                if (seenUrls.Contains(normalizedUrl))
                    continue;

                seenUrls.Add(normalizedUrl);
                links.Add(normalizedUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlaywrightScraperService] Error extracting links from {Url}", baseUrl);
        }

        return links;
    }

    private string ExtractTextContent(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove script, style, noscript elements
            foreach (var selector in new[] { "//script", "//style", "//noscript", "//svg", "//head" })
            {
                var nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                        node.Remove();
                }
            }

            var sb = new StringBuilder();
            ExtractTextRecursive(doc.DocumentNode, sb);

            var text = sb.ToString();
            
            // Clean up whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n[ \t]*\n", "\n\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");

            var lines = text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line.Trim()))
                .Select(line => line.Trim());

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlaywrightScraperService] Error extracting text content");
            return string.Empty;
        }
    }

    private void ExtractTextRecursive(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = System.Net.WebUtility.HtmlDecode(node.InnerText.Trim());
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.Append(text).Append(' ');
            }
        }
        else if (node.NodeType == HtmlNodeType.Element)
        {
            foreach (var child in node.ChildNodes)
            {
                ExtractTextRecursive(child, sb);
            }

            // Add line breaks for block elements
            var blockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "article", "section",
                "blockquote", "pre", "ul", "ol", "li", "table", "tr", "br", "hr"
            };

            if (blockElements.Contains(node.Name))
            {
                sb.AppendLine();
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        _initialized = false;

        _logger.LogInformation("[PlaywrightScraperService] Playwright disposed");
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result from Playwright scraping
/// </summary>
public class PlaywrightScrapeResult
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int HtmlLength { get; set; }
    public DateTime ScrapedAt { get; set; }
    public int Depth { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? Links { get; set; }
}
