# RAG Agent API

A sophisticated multi-agent Retrieval-Augmented Generation (RAG) system built with ASP.NET Core 8.0 and Microsoft Agent Framework, utilizing Azure AI services for intelligent document processing and querying.

## 🏗️ Architecture

### Multi-Agent System
The API implements a multi-agent architecture where specialized agents collaborate to process documents and handle queries:

- **OrchestratorAgent**: Coordinates the execution pipeline and manages agent communication
- **ScraperAgent**: Extracts and cleans content from web URLs using HtmlAgilityPack
- **ChunkerAgent**: Intelligently splits content into overlapping chunks with sentence boundary preservation
- **EmbeddingAgent**: Generates vector embeddings using Azure OpenAI text-embedding-ada-002
- **StorageAgent**: Stores documents and embeddings in Azure AI Search with vector indexing
- **QueryAgent**: Handles user queries with vector similarity search and RAG-based answer generation

### Azure Services Integration
- **Azure OpenAI Service**: Text embeddings and chat completions (GPT-3.5-turbo)
- **Azure AI Search**: Vector database with HNSW algorithm for semantic search
- **Azure Blob Storage**: Document storage and management
- **Azure Document Intelligence**: PDF parsing and text extraction
- **Application Insights**: Comprehensive telemetry and monitoring

## 🚀 Features

### Core Capabilities
- ✅ **Web Content Ingestion**: Scrape and process content from any URL
- ✅ **Intelligent Chunking**: Preserve semantic meaning with sentence-aware splitting
- ✅ **Vector Embeddings**: High-quality embeddings using Azure OpenAI
- ✅ **Semantic Search**: Find relevant content using vector similarity
- ✅ **RAG Generation**: Context-aware answer generation
- ✅ **Thread Management**: Stateful conversations with context preservation
- ✅ **Comprehensive Telemetry**: Full observability with Application Insights

### Quality & Reliability
- ✅ **Production Ready**: Error handling, retries, and graceful degradation
- ✅ **Comprehensive Testing**: Unit tests with xUnit and Moq
- ✅ **API Documentation**: OpenAPI/Swagger with detailed schemas
- ✅ **Health Monitoring**: Service health checks and dependency validation
- ✅ **Background Services**: Automatic context cleanup and maintenance

## 📋 Prerequisites

- .NET 8.0 SDK
- Azure subscription with the following resources:
  - Azure OpenAI Service (text-embedding-ada-002, gpt-35-turbo deployments)
  - Azure AI Search instance
  - Azure Blob Storage account
  - Azure Document Intelligence service
  - Application Insights instance (optional)

## ⚙️ Configuration

**⚠️ Security First:** Never commit API keys to Git!

### Quick Setup
1. Copy template configuration files:
```bash
cp appsettings.json.template appsettings.json
cp appsettings.Development.json.template appsettings.Development.json
```

2. Update configuration files with your Azure service endpoints and keys:
   - See `SETUP.md` for detailed instructions
   - Replace all `YOUR_*` placeholders with actual values

### Required Azure Services
- Azure OpenAI Service (text-embedding-ada-002, gpt-35-turbo deployments)
- Azure AI Search instance
- Azure Storage account  