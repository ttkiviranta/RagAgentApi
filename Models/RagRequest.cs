using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Models;

/// <summary>
/// Request model for ingesting content from a URL into the RAG system
/// </summary>
public class RagRequest
{
    /// <summary>
    /// URL or resource identifier to scrape and ingest
    /// Can be an HTTP URL or a local resource identifier (e.g., local://document/filename)
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Size of text chunks (100-5000 characters)
    /// </summary>
    [Range(100, 5000)]
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks (must be less than ChunkSize/2)
    /// </summary>
    [Range(0, 2500)]
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Crawl depth for following links on the page.
    /// 0 = Only scrape the given URL (default)
    /// 1 = Also scrape links found on the page (same domain only)
    /// 2 = Follow links from those pages too (max recommended)
    /// </summary>
    [Range(0, 3)]
    public int CrawlDepth { get; set; } = 0;

    /// <summary>
    /// Maximum number of pages to crawl when CrawlDepth > 0
    /// </summary>
    [Range(1, 50)]
    public int MaxPages { get; set; } = 10;

    /// <summary>
    /// Whether to stay on the same domain when crawling
    /// </summary>
    public bool SameDomainOnly { get; set; } = true;
}