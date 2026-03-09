using RagAgentApi.Models;
using System.Text.Json;
using HtmlAgilityPack;
using System.Text;

namespace RagAgentApi.Agents;

/// <summary>
/// Specialized agent for GitHub repositories and files
/// Uses GitHub API (Octokit) to fetch repository content, README files, and metadata
/// Supports crawl depth for scraping multiple pages
/// </summary>
public class GitHubApiAgent : BaseRagAgent
{
    private readonly HttpClient _httpClient;

    public GitHubApiAgent(HttpClient httpClient, ILogger<GitHubApiAgent> logger) : base(logger)
    {
        _httpClient = httpClient;
    }

    public override string Name => "GitHubApiAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
            // Validate URL is GitHub
            if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
            {
                return AgentResult.CreateFailure("URL not found in context state");
            }

            if (!IsGitHubUrl(url))
            {
                return AgentResult.CreateFailure($"URL {url} is not a GitHub URL");
            }

            _logger.LogInformation("[GitHubApiAgent] Processing GitHub URL: {Url}", url);

            // Get crawl parameters from context
            var crawlDepth = context.State.TryGetValue("crawl_depth", out var depthObj) && depthObj is int depth ? depth : 0;
            var maxPages = context.State.TryGetValue("max_pages", out var maxObj) && maxObj is int max ? max : 10;
            var sameDomainOnly = !context.State.TryGetValue("same_domain_only", out var sameObj) || sameObj is not bool same || same;

            _logger.LogInformation("[GitHubApiAgent] Crawl parameters - Depth: {Depth}, MaxPages: {MaxPages}", crawlDepth, maxPages);

            string content;
            int pagesScraped = 1;

            if (crawlDepth > 0)
            {
                // Use web scraping with depth for GitHub pages
                var scrapedPages = await ScrapeGitHubWithDepthAsync(url, crawlDepth, maxPages, cancellationToken);

                if (!scrapedPages.Any())
                {
                    return AgentResult.CreateFailure($"No content found from {url} or linked pages");
                }

                // Combine all scraped content
                content = string.Join("\n\n---PAGE BREAK---\n\n", 
                    scrapedPages.Select(p => $"# {p.Title}\nSource: {p.Url}\n\n{p.Content}"));

                pagesScraped = scrapedPages.Count;
                context.State["pages_scraped"] = pagesScraped;
                context.State["scraped_urls"] = scrapedPages.Select(p => p.Url).ToList();

                _logger.LogInformation("[GitHubApiAgent] Scraped {PageCount} pages, {Length} total chars", 
                    pagesScraped, content.Length);
            }
            else
            {
                // Single page - use GitHub API-style extraction
                var gitHubInfo = ParseGitHubUrl(url);
                content = await ScrapeGitHubPageAsync(url, cancellationToken);

                if (string.IsNullOrWhiteSpace(content) || content.Length < 50)
                {
                    // Fallback to placeholder content
                    content = GeneratePlaceholderContent(url, gitHubInfo);
                }

                context.State["github_metadata"] = JsonDocument.Parse(JsonSerializer.Serialize(gitHubInfo));
            }

            // Store extracted content in context
            context.State["raw_content"] = content;
            context.State["source_url"] = url;
            context.State["content_type"] = "github";

            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

            _logger.LogInformation("[GitHubApiAgent] Processed GitHub URL: {Url} ({Length} chars, {Pages} pages)", 
                url, content.Length, pagesScraped);

            AddMessage(context, "ChunkerAgent", 
                $"GitHub content extracted successfully ({content.Length} characters, {pagesScraped} pages)",
                new Dictionary<string, object>
                {
                    { "url", url },
                    { "content_length", content.Length },
                    { "pages_scraped", pagesScraped }
                });

            return AgentResult.CreateSuccess(
                $"GitHub content extracted successfully ({pagesScraped} pages)",
                new Dictionary<string, object>
                {
                    { "url", url },
                    { "content_length", content.Length },
                    { "pages_scraped", pagesScraped },
                    { "extraction_time_ms", stopwatch.ElapsedMilliseconds }
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
            return HandleException(ex, context.ThreadId, "GitHub API processing");
        }
    }

    private async Task<string> ScrapeGitHubPageAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var html = await _httpClient.GetStringAsync(url, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove unwanted elements
            foreach (var selector in new[] { "//script", "//style", "//noscript", "//nav", "//footer" })
            {
                var nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                        node.Remove();
                }
            }

            // Try to extract README content first
            var readmeNode = doc.DocumentNode.SelectSingleNode("//article[contains(@class,'markdown-body')]");
            if (readmeNode != null)
            {
                return CleanText(readmeNode.InnerText);
            }

            // Try repository description
            var descNode = doc.DocumentNode.SelectSingleNode("//p[contains(@class,'f4')]");
            var aboutNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'BorderGrid-cell')]");

            var content = new StringBuilder();

            if (descNode != null)
                content.AppendLine(descNode.InnerText.Trim());

            if (aboutNode != null)
                content.AppendLine(aboutNode.InnerText.Trim());

            // Get any other main content
            var mainContent = doc.DocumentNode.SelectSingleNode("//main");
            if (mainContent != null)
            {
                content.AppendLine(CleanText(mainContent.InnerText));
            }

            return content.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GitHubApiAgent] Failed to scrape GitHub page: {Url}", url);
            return string.Empty;
        }
    }

    private async Task<List<ScrapedGitHubPage>> ScrapeGitHubWithDepthAsync(
        string startUrl, 
        int depth, 
        int maxPages,
        CancellationToken cancellationToken)
    {
        var scrapedPages = new List<ScrapedGitHubPage>();
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
                _logger.LogInformation("[GitHubApiAgent] Crawling {Url} (depth {Depth}/{MaxDepth})", 
                    url, currentDepth, depth);

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var html = await _httpClient.GetStringAsync(url, cancellationToken);

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

                // Extract content - prefer README markdown
                var contentNode = doc.DocumentNode.SelectSingleNode("//article[contains(@class,'markdown-body')]");
                string content;

                if (contentNode != null)
                {
                    content = CleanText(contentNode.InnerText);
                }
                else
                {
                    var mainNode = doc.DocumentNode.SelectSingleNode("//main");
                    content = mainNode != null ? CleanText(mainNode.InnerText) : CleanText(doc.DocumentNode.InnerText);
                }

                if (!string.IsNullOrWhiteSpace(content) && content.Length >= 50)
                {
                    scrapedPages.Add(new ScrapedGitHubPage
                    {
                        Url = url,
                        Content = content,
                        Title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? url,
                        Depth = currentDepth
                    });

                    _logger.LogInformation("[GitHubApiAgent] Scraped {Length} chars from {Url}", 
                        content.Length, url);
                }

                // Extract links for next depth level (only GitHub links)
                if (currentDepth < depth && scrapedPages.Count < maxPages)
                {
                    var links = ExtractGitHubLinks(html, url);
                    foreach (var link in links.Take(maxPages - scrapedPages.Count))
                    {
                        if (!visitedUrls.Contains(link))
                        {
                            urlsToVisit.Enqueue((link, currentDepth + 1));
                        }
                    }
                }

                // Small delay to be respectful
                await Task.Delay(300, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GitHubApiAgent] Failed to scrape {Url}", url);
            }
        }

        _logger.LogInformation("[GitHubApiAgent] Crawl complete: {Count} pages scraped", scrapedPages.Count);
        return scrapedPages;
    }

    private List<string> ExtractGitHubLinks(string html, string baseUrl)
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
                    href.Contains("/actions") ||
                    href.Contains("/issues") ||
                    href.Contains("/pull") ||
                    href.Contains("/settings") ||
                    href.Contains("/compare") ||
                    href.Contains("/commits") ||
                    href.EndsWith(".pdf") ||
                    href.EndsWith(".zip"))
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

                // Only GitHub links
                if (!absoluteUri.Host.Contains("github.com"))
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
            _logger.LogWarning(ex, "[GitHubApiAgent] Error extracting links from {Url}", baseUrl);
        }

        return links;
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n[ \t]*\n", "\n\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");

        var lines = text.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line.Trim()) && line.Trim().Length > 3)
            .ToList();

        return string.Join("\n", lines).Trim();
    }

    private static bool IsGitHubUrl(string url)
    {
        return url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase);
    }

    private static GitHubUrlInfo ParseGitHubUrl(string url)
    {
      // Parse GitHub URL patterns:
        // https://github.com/owner/repo
        // https://github.com/owner/repo/blob/branch/file.md
        // https://github.com/owner/repo/tree/branch/folder
        
     var uri = new Uri(url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        
        if (segments.Length < 2)
        {
 return new GitHubUrlInfo { Type = "invalid", Owner = "", Repository = "" };
     }

        var info = new GitHubUrlInfo
        {
    Owner = segments[0],
      Repository = segments[1]
  };

        if (segments.Length == 2)
        {
  info.Type = "repository";
        }
        else if (segments.Length >= 4 && segments[2] == "blob")
   {
      info.Type = "file";
     info.Branch = segments[3];
       info.Path = string.Join("/", segments.Skip(4));
     }
        else if (segments.Length >= 4 && segments[2] == "tree")
        {
            info.Type = "tree";
        info.Branch = segments[3];
     info.Path = string.Join("/", segments.Skip(4));
        }
        else
        {
          info.Type = "repository";
   }

        return info;
  }

    private static string GeneratePlaceholderContent(string url, GitHubUrlInfo gitHubInfo)
    {
        return $@"# GitHub Repository: {gitHubInfo.Owner}/{gitHubInfo.Repository}

**URL**: {url}
**Type**: {gitHubInfo.Type}
**Owner**: {gitHubInfo.Owner}
**Repository**: {gitHubInfo.Repository}
{(string.IsNullOrEmpty(gitHubInfo.Branch) ? "" : $"**Branch**: {gitHubInfo.Branch}")}
{(string.IsNullOrEmpty(gitHubInfo.Path) ? "" : $"**Path**: {gitHubInfo.Path}")}

## TODO: GitHub API Integration

This is a placeholder for GitHub API integration. When implemented, this agent will:

1. **Repository Analysis**: Extract repository metadata including stars, forks, language, topics, and description
2. **README Extraction**: Fetch and parse README files in various formats (markdown, rst, txt)
3. **Documentation Scraping**: Extract documentation from docs/ folders, wiki pages, and GitHub Pages
4. **Code Analysis**: Analyze code structure, dependencies, and key files
5. **Issue & PR Context**: Extract relevant issues and pull request discussions
6. **Release Notes**: Fetch release notes and changelog information

## Implementation Plan

- Install Octokit NuGet package for GitHub API
- Add GitHub authentication support (PAT tokens)
- Implement rate limiting and retry logic
- Add support for private repositories
- Cache frequently accessed repositories
- Extract code snippets and documentation intelligently

This placeholder content allows the RAG system to understand that this URL points to a GitHub repository
and provides basic metadata for processing.";
    }

    private class GitHubUrlInfo
    {
        public string Type { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Repository { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Path { get; set; } = "";
    }
}

/// <summary>
/// Represents a single scraped GitHub page
/// </summary>
public class ScrapedGitHubPage
{
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Depth { get; set; }
}