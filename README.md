# RAG Agent API

A sophisticated **multi-agent Retrieval-Augmented Generation (RAG) system** built with ASP.NET Core 8.0, featuring **PostgreSQL+pgvector** for vector storage and **dynamic agent selection** for intelligent document processing and querying.

## 🏗️ Architecture

### Multi-Agent System with Dynamic Selection
The API implements an **enhanced multi-agent architecture** where specialized agents are **dynamically selected** based on content type:

#### Core Pipeline Agents
- **OrchestratorAgent**: **Enhanced** coordinator with dynamic agent selection and pipeline execution
- **ScraperAgent**: Extracts and cleans content from web URLs using HtmlAgilityPack
- **ChunkerAgent**: Intelligently splits content into overlapping chunks with sentence boundary preservation
- **EmbeddingAgent**: Generates vector embeddings using Azure OpenAI text-embedding-ada-002
- **PostgresStorageAgent**: **NEW** - Stores documents and embeddings in PostgreSQL with pgvector
- **PostgresQueryAgent**: **NEW** - Handles queries with PostgreSQL vector similarity search

#### Specialized Content Agents
- **GitHubApiAgent**: **NEW** - Specialized for GitHub repositories, files, and documentation
- **YouTubeTranscriptAgent**: **NEW** - Extracts video metadata and transcripts from YouTube
- **ArxivScraperAgent**: **NEW** - Processes academic papers from arXiv with PDF support
- **NewsArticleScraperAgent**: **NEW** - Optimized for news articles and blog posts

#### Legacy Agents (Deprecated)
- **StorageAgent**: Legacy Azure Search storage (replaced by PostgresStorageAgent)
- **QueryAgent**: Legacy Azure Search queries (replaced by PostgresQueryAgent)

### Database & Storage Architecture
- **PostgreSQL 16 + pgvector**: **Primary vector database** with cosine similarity search
- **Azure OpenAI Service**: Text embeddings and chat completions (GPT-3.5-turbo)
- **Azure AI Search**: Legacy vector storage (being phased out)
- **Application Insights**: Comprehensive telemetry and monitoring

### Agent Selection System
- **AgentSelectorService**: **NEW** - Automatically selects optimal agent based on URL patterns
- **AgentFactory**: **NEW** - Dynamically creates agent pipelines from database configuration
- **URL Pattern Matching**: Regex-based routing (GitHub → GitHub agent, YouTube → YouTube agent, etc.)

## 🚀 Features

### Enhanced Core Capabilities
- ✅ **Dynamic Agent Selection**: Automatic agent routing based on content type
- ✅ **Specialized Content Processing**: GitHub, YouTube, arXiv, News optimizations
- ✅ **PostgreSQL Vector Storage**: High-performance pgvector with IVFFlat indexing
- ✅ **Web Content Ingestion**: Scrape and process content from any URL
- ✅ **Intelligent Chunking**: Preserve semantic meaning with sentence-aware splitting
- ✅ **Vector Embeddings**: High-quality embeddings using Azure OpenAI ada-002
- ✅ **Semantic Search**: PostgreSQL cosine similarity search with metadata
- ✅ **RAG Generation**: Context-aware answer generation with source tracking
- ✅ **Conversation Management**: Full conversation history with PostgreSQL
- ✅ **Pipeline Analytics**: Complete execution tracking and performance metrics

### Advanced Features
- ✅ **URL-Based Agent Routing**: Automatic selection of specialized agents
- ✅ **Database-Driven Configuration**: Agent types and URL mappings stored in PostgreSQL
- ✅ **Execution Analytics**: Comprehensive pipeline performance tracking
- ✅ **Similar Query Detection**: Find related past queries using vector similarity
- ✅ **Agent Pipeline Orchestration**: Sequential agent execution with error handling
- ✅ **Enhanced API Endpoints**: Both legacy and enhanced processing modes

### Quality & Reliability
- ✅ **Production Ready**: Error handling, retries, and graceful degradation
- ✅ **Comprehensive Testing**: Unit tests with xUnit and Moq
- ✅ **API Documentation**: OpenAPI/Swagger with detailed schemas
- ✅ **Health Monitoring**: Service health checks and dependency validation
- ✅ **Background Services**: Automatic context cleanup and database maintenance

## 📋 Prerequisites

### Required Software
- .NET 8.0 SDK
- PostgreSQL 16 with pgvector extension
- Docker (for PostgreSQL container)

### Azure Services
- Azure OpenAI Service (text-embedding-ada-002, gpt-35-turbo deployments)
- Azure AI Search instance (legacy, optional)
- Azure Blob Storage account (optional)
- Azure Document Intelligence service (optional)
- Application Insights instance (optional)

### Database Setup
The system uses **PostgreSQL 16 with pgvector** as the primary database:

```bash
# Run PostgreSQL with pgvector using Docker
docker run -d \
  --name ragagentdb \
  -e POSTGRES_PASSWORD=YourPassword \
  -p 5433:5432 \
  pgvector/pgvector:pg16
```

## ⚙️ Configuration

**⚠️ Security First:** Never commit API keys to Git!

### Quick Setup
1. Copy template configuration files:
```bash
cp appsettings.json.template appsettings.json
cp appsettings.Development.json.template appsettings.Development.json
```

2. Update configuration files with your service endpoints and keys:
   - See `SETUP.md` for detailed instructions
   - Replace all `YOUR_*` placeholders with actual values

### Required Configuration
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5433;Database=ragagentdb;Username=postgres;Password=YourPassword"
  },
  "Azure": {
    "OpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
      "Key": "your-openai-key",
      "EmbeddingDeployment": "text-embedding-ada-002",
  "ChatDeployment": "gpt-35-turbo"
    }
  },
  "RagSettings": {
    "Mode": "hybrid"
  }
}
```

### RAG Modes

The API supports two operational modes for handling queries without document context:

#### Hybrid Mode (Default: `"hybrid"`)
- **With documents**: Uses RAG with document context
- **Without documents**: Falls back to ChatGPT general knowledge
- **Best for**: Interactive chatbots that should always provide helpful answers
- **User experience**: Clearly indicates when answering without documents

#### Strict Mode (`"strict"`)
- **With documents**: Uses RAG with document context
- **Without documents**: Returns error message requiring document ingestion
- **Best for**: Applications requiring only fact-based answers from ingested documents
- **User experience**: Ensures no hallucination or general knowledge mixing

**Configuration:**
```json
{
  "RagSettings": {
    "Mode": "hybrid",  // or "strict"
    "DefaultChunkSize": 1000
  }
}
```

## 🚀 Getting Started

### 1. Database Setup
```bash
# Start PostgreSQL with pgvector
docker run -d --name ragagentdb -e POSTGRES_PASSWORD=YourPassword -p 5433:5432 pgvector/pgvector:pg16

# The application will automatically:
# - Create database schema with Entity Framework migrations
# - Seed initial agent types and URL mappings
# - Set up vector indexes for optimal performance
```

### 2. Run the Application
```bash
dotnet restore
dotnet build
dotnet run
```

Navigate to `https://localhost:7000` for Swagger UI.

### 3. Test the Enhanced API

#### Enhanced Content Ingestion (Recommended)
```bash
curl -X POST "https://localhost:7000/api/Rag/ingest-enhanced" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://github.com/microsoft/semantic-kernel",
    "chunkSize": 1000,
    "chunkOverlap": 200
  }'
```

**Response includes agent selection:**
```json
{
  "thread_id": "...",
  "agent_type": "github_specialist",
  "pipeline_agents": ["GitHubApiAgent", "ChunkerAgent", "EmbeddingAgent", "PostgresStorageAgent"],
  "chunks_processed": 15,
  "execution_time_ms": 3500,
  "enhanced_processing": true
}
```

#### Query with PostgreSQL
```bash
curl -X POST "https://localhost:7000/api/Rag/query" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "What is Semantic Kernel?",
    "topK": 5
  }'
```

## 📊 Agent Types & URL Routing

The system automatically selects specialized agents based on URL patterns:

| URL Pattern | Agent Type | Specialization |
|-------------|------------|----------------|
| `github.com/*` | **GitHubApiAgent** | Repository analysis, README extraction, code structure |
| `youtube.com/*` | **YouTubeTranscriptAgent** | Video metadata, transcript extraction, timestamps |
| `arxiv.org/*` | **ArxivScraperAgent** | Academic papers, PDF processing, citations |
| `news/*`, `blog/*` | **NewsArticleScraperAgent** | Article content, author detection, publication dates |
| `*` (fallback) | **DefaultAgent** | General web scraping with ScraperAgent |

## 🗄️ Database Schema

The system uses PostgreSQL with the following key tables:

### Vector Storage
- **Documents**: URL-level metadata and full content
- **DocumentChunks**: Text chunks with 1536-dimensional vectors
- **Messages**: Query history with embeddings for similarity search

### Agent Management
- **AgentTypes**: Available agent configurations and pipelines
- **UrlAgentMappings**: URL pattern → Agent type routing rules
- **AgentExecutions**: Complete pipeline execution history and analytics

### Conversation Tracking
- **Conversations**: User conversation sessions
- **Messages**: All messages with embeddings for similar query detection

## 🎯 Demo Services

The API includes **4 standalone demo services** for demonstrating AI/ML capabilities without affecting the RAG pipeline.

### Available Demos
- **Classification**: Text sentiment classification with 20 training samples
- **Time-Series**: Forecasting and trend analysis with statistical metrics
- **Image Processing**: Image generation and color analysis (256x256 PNG)
- **Audio Processing**: Signal analysis with frequency detection (440Hz WAV)

### Demo Endpoints

```bash
# Get available demos
curl https://localhost:7000/api/demo/available

# Generate test data
curl -X POST https://localhost:7000/api/demo/generate-testdata?demoType=classification

# Run demo
curl -X POST https://localhost:7000/api/demo/run?demoType=classification
```

### Demo Results
Each demo returns structured results with execution metrics:

```json
{
  "demoType": "classification",
  "success": true,
  "message": "Classification demo completed successfully",
  "data": {
    "total_samples": 20,
    "label_distribution": {"positive": 10, "negative": 10},
    "model_accuracy": "92.00%",
    "classes_found": ["positive", "negative"]
  },
  "executionTimeMs": "2ms"
}
```

### Test Data Storage
Generated demo files are stored in `demos/` directory:
```
demos/
├── classification/data/classification_training.csv
├── time-series/data/timeseries_data.csv
├── image-processing/data/test_image.png
└── audio-processing/data/test_audio.wav
```

### Testing Demo Services
```powershell
# Run comprehensive test suite
./test-demo-api.ps1

# Check API health
./check-api-health.ps1
```

For detailed demo documentation, see [Documentation/DEMO_SERVICES.md](Documentation/DEMO_SERVICES.md)

---

## 🧪 Testing & Development

### Available Test Endpoints

#### Database Tests
- `GET /api/DatabaseTest/connection` - Test PostgreSQL connection
- `GET /api/DatabaseTest/vector-test` - Test pgvector operations
- `GET /api/DatabaseTest/stats` - Database statistics

#### PostgreSQL Services
- `POST /api/PostgresTest/full-pipeline` - Test complete PostgreSQL RAG pipeline
- `POST /api/PostgresTest/query-service` - Test vector similarity search

#### Specialized Agents
- `POST /api/SpecializedAgentsTest/test-github` - Test GitHub agent
- `POST /api/SpecializedAgentsTest/test-youtube` - Test YouTube agent
- `POST /api/SpecializedAgentsTest/test-all` - Test all specialized agents

#### Agent Management
- `GET /api/AgentManagementTest/agent-types` - View all agent types
- `POST /api/AgentManagementTest/test-selection` - Test agent selection
- `POST /api/AgentManagementTest/test-batch-selection` - Batch URL testing

#### Enhanced Orchestration
- `POST /api/OrchestratorTest/test-enhanced-orchestration` - Test full enhanced pipeline
- `GET /api/OrchestratorTest/execution-analytics` - Pipeline performance analytics

## 🔧 Configuration Management

### Agent Type Configuration
Agent types and URL mappings are stored in PostgreSQL and can be managed via API:

```bash
# Add new URL pattern
curl -X POST "https://localhost:7000/api/AgentManagementTest/mappings" \
  -H "Content-Type: application/json" \
  -d '{
    "agentTypeName": "github_specialist",
    "pattern": "^https://github.com/.*",
    "priority": 10,
    "isActive": true
  }'
```

### Pipeline Configuration
Each agent type has a configurable pipeline stored as JSON:

```json
{
  "name": "github_specialist",
  "pipeline": [
    "GitHubApiAgent",
    "ChunkerAgent", 
    "EmbeddingAgent",
    "PostgresStorageAgent"
  ]
}
```

## 📈 Performance & Analytics

### Execution Tracking
Every pipeline execution is tracked with detailed metrics:
- Agent selection reasoning
- Individual agent performance
- Step-by-step execution times
- Success/failure rates
- Error analysis

### Vector Search Performance
- PostgreSQL pgvector with IVFFlat indexing
- Cosine similarity search optimized for 1536-dimensional vectors
- Metadata filtering and hybrid search capabilities

## 🔄 Migration from Azure Search

The system supports both legacy Azure Search and new PostgreSQL storage:

- **Enhanced endpoints** (`/ingest-enhanced`) use PostgreSQL automatically
- **Legacy endpoints** (`/ingest`) continue to work with Azure Search
- **Gradual migration** path available for existing deployments

## 🚨 Known Issues & Limitations

### Specialized Agents (Placeholder Status)
Current specialized agents are **placeholder implementations** and require additional libraries:

- **GitHubApiAgent**: Requires Octokit library for GitHub API integration
- **YouTubeTranscriptAgent**: Requires YoutubeExplode for transcript extraction
- **ArxivScraperAgent**: Requires PDF processing libraries for paper analysis
- **NewsArticleScraperAgent**: Requires HtmlAgilityPack for content extraction

### Future Enhancements
- Real implementation of specialized agents
- Multi-language support for content processing
- Advanced citation tracking and cross-referencing
- Real-time content monitoring and updates

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📞 Support

For issues and questions:
- Create an issue in the GitHub repository
- Check the existing documentation in `SETUP.md`
- Review the Swagger UI at `https://localhost:7000` for API documentation