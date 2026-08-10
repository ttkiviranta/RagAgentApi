using RagAgentApi.Models;

namespace RagAgentApi.Services;

public static class AgentMetadataDefinitions
{
    public static IReadOnlyDictionary<string, AgentMetadata> Create()
    {
        var list = new List<AgentMetadata>
        {
            new()
            {
                Name = "OrchestratorAgent",
                Description = "Coordinates multi-agent execution and routes work to the appropriate orchestration mode.",
                Capabilities = [ "pipeline coordination", "mode switching", "execution supervision" ],
                Inputs = [ "url", "thread context", "shared state" ],
                Outputs = [ "orchestrated result", "execution metadata" ]
            },
            new()
            {
                Name = "ScraperAgent",
                Description = "Scrapes and normalizes web page content from a URL.",
                Capabilities = [ "html extraction", "content cleaning", "metadata extraction" ],
                Inputs = [ "url" ],
                Outputs = [ "raw content", "document metadata" ]
            },
            new()
            {
                Name = "ChunkerAgent",
                Description = "Splits long content into chunked segments for embedding and retrieval.",
                Capabilities = [ "text chunking", "overlap handling", "chunk metadata generation" ],
                Inputs = [ "content", "chunk configuration" ],
                Outputs = [ "content chunks", "chunk statistics" ]
            },
            new()
            {
                Name = "EmbeddingAgent",
                Description = "Generates vector embeddings for text chunks using LLM embedding models.",
                Capabilities = [ "embedding generation", "vector preparation" ],
                Inputs = [ "chunks" ],
                Outputs = [ "embeddings", "embedding metadata" ]
            },
            new()
            {
                Name = "StorageAgent",
                Description = "Stores documents and vectors in Azure Search (legacy path).",
                Capabilities = [ "azure search indexing", "document persistence" ],
                Inputs = [ "document", "chunks", "embeddings" ],
                Outputs = [ "indexing result", "document id" ]
            },
            new()
            {
                Name = "PostgresStorageAgent",
                Description = "Stores documents, chunks, and vectors in PostgreSQL with pgvector.",
                Capabilities = [ "postgres persistence", "pgvector storage", "document versioning" ],
                Inputs = [ "document", "chunks", "embeddings" ],
                Outputs = [ "stored document id", "storage summary" ]
            },
            new()
            {
                Name = "QueryAgent",
                Description = "Performs query answering using Azure Search retrieval (legacy path).",
                Capabilities = [ "vector retrieval", "answer generation" ],
                Inputs = [ "query", "retrieval context" ],
                Outputs = [ "answer", "retrieval evidence" ]
            },
            new()
            {
                Name = "PostgresQueryAgent",
                Description = "Runs semantic retrieval against PostgreSQL vectors and builds final answers.",
                Capabilities = [ "pgvector similarity search", "context ranking", "answer generation" ],
                Inputs = [ "query", "document vectors" ],
                Outputs = [ "answer", "matched chunks", "relevance metrics" ]
            },
            new()
            {
                Name = "GitHubApiAgent",
                Description = "Fetches and processes repository content from GitHub endpoints.",
                Capabilities = [ "github api access", "repository file extraction", "readme processing" ],
                Inputs = [ "github url", "api response" ],
                Outputs = [ "repository content", "repository metadata" ]
            },
            new()
            {
                Name = "YouTubeTranscriptAgent",
                Description = "Extracts transcript and metadata from YouTube videos.",
                Capabilities = [ "video metadata parsing", "transcript extraction" ],
                Inputs = [ "youtube url" ],
                Outputs = [ "transcript", "video metadata" ]
            },
            new()
            {
                Name = "ArxivScraperAgent",
                Description = "Processes arXiv papers and related document content.",
                Capabilities = [ "arxiv metadata extraction", "paper ingestion", "pdf processing" ],
                Inputs = [ "arxiv url", "paper content" ],
                Outputs = [ "paper text", "paper metadata" ]
            },
            new()
            {
                Name = "NewsArticleScraperAgent",
                Description = "Extracts article text and metadata from news/blog pages.",
                Capabilities = [ "article scraping", "content cleanup", "publisher metadata extraction" ],
                Inputs = [ "news url" ],
                Outputs = [ "article content", "article metadata" ]
            },
            new()
            {
                Name = "EdgeSensorAgent",
                Description = "Collects and normalizes edge IoT sensor readings.",
                Capabilities = [ "sensor ingestion", "signal normalization", "edge telemetry" ],
                Inputs = [ "sensor payload" ],
                Outputs = [ "normalized sensor data", "sensor summary" ]
            },
            new()
            {
                Name = "EdgeAnalyzerAgent",
                Description = "Analyzes edge sensor streams for events, trends, and anomalies.",
                Capabilities = [ "time series analysis", "anomaly detection", "event classification" ],
                Inputs = [ "normalized sensor data", "analysis parameters" ],
                Outputs = [ "analysis report", "detected anomalies" ]
            }
        };

        return list.ToDictionary(m => m.Name, m => m, StringComparer.OrdinalIgnoreCase);
    }
}
