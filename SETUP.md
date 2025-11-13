# 🔧 Setup Instructions

## Prerequisites

- .NET 8.0 SDK
- Azure subscription with required services (see below)

## Azure Services Required

1. **Azure OpenAI Service**
   - Deploy `text-embedding-ada-002` model
   - Deploy `gpt-35-turbo` model

2. **Azure AI Search**
   - Standard tier or higher (for vector search)

3. **Azure Storage Account**
   - Create container named `rag-documents`

4. **Azure Document Intelligence**
   - Standard tier

5. **Application Insights** (optional)
   - For telemetry and monitoring

## Configuration Setup

### 1. Copy Template Files

```bash
cp appsettings.json.template appsettings.json
cp appsettings.Development.json.template appsettings.Development.json
```

### 2. Update Configuration Values

Replace the following placeholders in your `appsettings.json` and `appsettings.Development.json`:

#### Application Insights
- `YOUR_APPLICATION_INSIGHTS_CONNECTION_STRING` → Your Application Insights connection string

#### Azure OpenAI
- `YOUR_AZURE_OPENAI_ENDPOINT` → `https://your-resource.openai.azure.com/`
- `YOUR_AZURE_OPENAI_KEY` → Your Azure OpenAI API key

#### Azure AI Search  
- `YOUR_AZURE_SEARCH_ENDPOINT` → `https://your-search.search.windows.net`
- `YOUR_AZURE_SEARCH_KEY` → Your Azure Search admin key

#### Azure Storage
- `YOUR_AZURE_STORAGE_CONNECTION_STRING` → Your storage account connection string

#### Azure Document Intelligence
- `YOUR_AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT` → `https://your-doc-intel.cognitiveservices.azure.com/`
- `YOUR_AZURE_DOCUMENT_INTELLIGENCE_KEY` → Your Document Intelligence API key

### 3. Verify Configuration

```bash
dotnet run
```

Navigate to `https://localhost:7000` to access Swagger UI.

## Getting Started

### Clone Repository
```bash
git clone https://github.com/ttkiviranta/RagAgentApi.git
cd RagAgentApi
```

### Setup Configuration
```bash
# Copy template files
cp appsettings.json.template appsettings.json
cp appsettings.Development.json.template appsettings.Development.json

# Edit the files and add your Azure service credentials
# Use your preferred text editor
```

### Run Application
```bash
dotnet restore
dotnet build
dotnet run
```

## Security Notes

⚠️ **Never commit actual API keys or connection strings to Git!**

- Configuration files with secrets are in `.gitignore`
- Use template files as reference
- For production, use Azure Key Vault or environment variables

## Development Workflow

1. Clone repository: `git clone https://github.com/ttkiviranta/RagAgentApi.git`
2. Copy template files as shown above  
3. Add your Azure service credentials
4. Run `dotnet restore`
5. Run `dotnet run`

## Deployment

For production deployment:
- Use Azure App Service Application Settings
- Or use Azure Key Vault references
- Never deploy with hardcoded secrets

## Troubleshooting

See main README.md for detailed troubleshooting guide.