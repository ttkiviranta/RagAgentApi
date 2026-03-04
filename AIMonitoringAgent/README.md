# AI Monitoring Agent - Azure Functions v4 Solution

A production-ready Azure Functions v4 (.NET 8) solution that implements real-time AI monitoring of Application Insights with long-term memory, Teams chat integration, and deployment correlation.

## Architecture Overview

### Three-Project Structure

1. **AIMonitoringAgent.Shared** - Shared services and models
   - Error parsing and fingerprinting
   - LLM analysis with Azure OpenAI
   - Vector memory management (Azure AI Search)
   - Deployment correlation (Azure DevOps)
   - Notification routing

2. **AIMonitoringAgent.Functions** - Azure Functions
   - `EventHubFunction` - Real-time Application Insights event processing
   - `ChatFunction` - HTTP endpoint for programmatic access
   - `TeamsBotFunction` - Bot message endpoint

3. **AIMonitoringAgent.TeamsBot** - Teams Bot Framework
   - `TeamsBotService` - Bot logic and responses
   - `TeamsBotActivityHandler` - Activity processing

## Prerequisites

### Azure Services Required

- **Azure OpenAI** - GPT-4o for analysis, text-embedding-3-small for vectors
- **Azure AI Search** - Vector memory store
- **Azure Event Hubs** - Real-time ingestion from Application Insights
- **Application Insights** - Telemetry source
- **Azure DevOps** - Deployment correlation
- **Azure Bot Service** - Teams bot registration
- **Azure Storage Account** - Functions runtime storage
- **Azure App Insights** - Monitoring and logging

### Local Development

- .NET 8.0 SDK
- Visual Studio 2022+ or VS Code
- Azure Functions Core Tools v4+
- Azure CLI
- Azure Storage Emulator (optional)

## Setup Instructions

### 1. Azure Resource Setup

```bash
# Create resource group
az group create --name ai-monitoring --location eastus

# Create Storage Account
az storage account create \
  --resource-group ai-monitoring \
  --name aimonitoringsa \
  --sku Standard_LRS

# Create Application Insights
az monitor app-insights component create \
  --app ai-monitoring-insights \
  --resource-group ai-monitoring \
  --location eastus

# Create Azure Search
az search service create \
  --name ai-monitoring-search \
  --resource-group ai-monitoring \
  --sku standard

# Create Azure OpenAI
az cognitiveservices account create \
  --name ai-monitoring-openai \
  --resource-group ai-monitoring \
  --kind OpenAI \
  --sku S0 \
  --location eastus

# Create Event Hubs
az eventhubs namespace create \
  --resource-group ai-monitoring \
  --name ai-monitoring-ns

az eventhubs eventhub create \
  --resource-group ai-monitoring \
  --namespace-name ai-monitoring-ns \
  --name app-insights-events
```

### 2. Configure OpenAI Deployments

Deploy the required models in Azure OpenAI:

```bash
# Deploy GPT-4o
az cognitiveservices account deployment create \
  --resource-group ai-monitoring \
  --name ai-monitoring-openai \
  --deployment-name gpt-4o \
  --model-name gpt-4o \
  --model-version "2024-05" \
  --sku-capacity 1 \
  --sku-name Standard

# Deploy Text Embedding Model
az cognitiveservices account deployment create \
  --resource-group ai-monitoring \
  --name ai-monitoring-openai \
  --deployment-name text-embedding-3-small \
  --model-name text-embedding-3-small \
  --model-version "1" \
  --sku-capacity 1 \
  --sku-name Standard
```

### 3. Local Configuration

Copy and customize configuration files:

```bash
cd AIMonitoringAgent/AIMonitoringAgent.Functions

# Copy configuration templates
cp appsettings.json.template appsettings.json
cp local.settings.json.template local.settings.json
```

Edit `appsettings.json` and `local.settings.json` with your Azure credentials:

**Key configurations:**
- `Azure:OpenAI:Endpoint` - Your OpenAI instance endpoint
- `Azure:OpenAI:Key` - OpenAI API key
- `Azure:Search:Endpoint` - Azure AI Search endpoint
- `Azure:Search:Key` - Search API key
- `Azure:DevOps:Token` - Azure DevOps Personal Access Token
- `EventHubConnectionString` - Event Hub connection string
- `TEAMS_WEBHOOK_URL` - Incoming Webhook URL for Teams
- `SLACK_WEBHOOK_URL` - Webhook URL for Slack
- `EMAIL_RECIPIENTS` - Semicolon-separated email list

### 4. Build and Run Locally

```bash
# Restore and build
dotnet restore
dotnet build

# Start Azure Functions locally
cd AIMonitoringAgent.Functions
func start

# The functions will be available at:
# - EventHubFunction: Triggered by Event Hub messages
# - Chat: http://localhost:7071/api/chat
# - Messages: http://localhost:7071/api/messages
```

### 5. Deploy to Azure

```bash
# Create Function App in Azure
az functionapp create \
  --resource-group ai-monitoring \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8.0 \
  --functions-version 4 \
  --name ai-monitoring-functions \
  --storage-account aimonitoringsa

# Publish functions
func azure functionapp publish ai-monitoring-functions

# Set application settings
az functionapp config appsettings set \
  --name ai-monitoring-functions \
  --resource-group ai-monitoring \
  --settings "Azure:OpenAI:Endpoint=YOUR_VALUE" "Azure:OpenAI:Key=YOUR_VALUE" ...
```

## API Endpoints

### Chat Endpoint

**POST** `/api/chat`

Request:
```json
{
  "conversationId": "conv-123",
  "query": "Show me all errors related to SQL timeouts"
}
```

Response:
```json
{
  "conversationId": "conv-123",
  "query": "Show me all errors related to SQL timeouts",
  "response": "Found 3 SQL timeout errors...",
  "relevantErrorsCount": 3,
  "relevantErrors": [
    {
      "fingerprintHash": "hash...",
      "exceptionType": "SqlException",
      "message": "Timeout expired",
      "severity": "high",
      "occurrenceCount": 5
    }
  ],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Teams Bot Endpoint

**POST** `/api/messages`

Used by Microsoft Bot Framework. Messages from Teams are automatically routed here.

### Supported Chat Queries

- **"Show me all errors related to [keyword]"** - Filter by keyword
- **"Is this error new or recurring?"** - Check recurrence
- **"What changed before this error started?"** - Show deployment correlation
- **"Did this error correlate with a deployment?"** - Deployment analysis

## Event Hub Integration

Configure Application Insights to export exceptions to Event Hub:

1. In Application Insights → Continuous Export
2. Create new export with filter:
   ```
   SELECT * FROM events WHERE eventType='Exception'
   ```
3. Choose Event Hubs as destination
4. Select `app-insights-events` hub

## Key Features

### 1. Real-Time Error Processing
- Event Hub trigger processes exceptions as they occur
- Fingerprinting prevents duplicate analysis
- Sub-second latency for critical errors

### 2. Vector Memory with Long-Term Learning
- Azure AI Search stores all error analysis
- Semantic search finds similar past errors
- Embeddings capture error context

### 3. GPT-4o Analysis
- Structured JSON output with severity and category
- Root cause analysis
- Recommended actions
- Recurring error detection

### 4. Deployment Correlation
- Fetches recent Azure DevOps deployments
- Correlates errors with release timing
- Identifies changed files in deployment
- Flags likely cause if within 30 minutes

### 5. Multi-Channel Notifications
- **Teams** - Adaptive Cards with rich formatting
- **Slack** - Formatted messages with blocks
- **Email** - Detailed HTML reports (SMTP)
- Configurable recipients and channels

### 6. Teams Bot Integration
- Natural language queries about errors
- Historical error analysis
- Deployment correlation info
- Recurring error tracking

## Configuration Examples

### Enable Only Teams Notifications

```json
{
  "Notifications": {
    "Teams": {
      "WebhookUrl": "https://outlook.webhook.office.com/..."
    }
  }
}
```

### Setup Email Notifications

```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "alerts@yourdomain.com",
    "SmtpPassword": "app-password",
    "FromAddress": "alerts@yourdomain.com"
  }
}
```

### Configure Deployment Correlation

```json
{
  "Azure": {
    "DevOps": {
      "Organization": "https://dev.azure.com/yourorg",
      "Project": "MyProject",
      "Token": "YOUR_PAT_TOKEN"
    }
  }
}
```

## Monitoring and Troubleshooting

### View Function Logs

```bash
# Local logs
func logs stream

# Azure logs
az monitor log-analytics query \
  --workspace /subscriptions/SUB_ID/resourcegroups/RG/providers/microsoft.operationalinsights/workspaces/WS_NAME \
  --analytics-query "AppServicePlatformLogs | where FunctionName == 'EventHubFunction'"
```

### Common Issues

**Issue:** "No OpenAI models deployed"
- Solution: Verify model deployments in Azure OpenAI (gpt-4o, text-embedding-3-small)

**Issue:** "Event Hub connection failed"
- Solution: Check Event Hub connection string and namespace access

**Issue:** "Search index not found"
- Solution: Run `VectorMemoryStore.InitializeAsync()` to create index

**Issue:** "Teams bot not responding"
- Solution: Verify bot registration and endpoint in Bot Service settings

## Testing

### Test Event Hub Processing

```bash
# Send test event
curl -X POST "http://localhost:7071/admin/functions/EventHubTrigger/invoke" \
  -H "Content-Type: application/json" \
  -d '{
    "exceptionType": "NullReferenceException",
    "message": "Object reference not set to an instance of an object",
    "stackTrace": "at MyApp.Service.Process()",
    "timestamp": "2024-01-15T10:30:00Z",
    "operationName": "GetUser"
  }'
```

### Test Chat Endpoint

```bash
curl -X POST "http://localhost:7071/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "test-123",
    "query": "Show me all recent errors"
  }'
```

### Test Teams Bot

1. Register bot in Azure Bot Service
2. Add bot to Teams channel
3. Send message to bot in Teams

## Production Checklist

- [ ] All Azure credentials configured securely (Key Vault recommended)
- [ ] HTTPS enabled on all endpoints
- [ ] Authentication configured for chat endpoint
- [ ] Event Hub consumer group created
- [ ] Application Insights telemetry sampling configured
- [ ] Log retention policies set
- [ ] Teams bot published and available
- [ ] Notification webhooks tested
- [ ] Error budget/SLA defined
- [ ] On-call runbook created
- [ ] Health monitoring configured

## Security Considerations

### Recommended Practices

1. **Store secrets in Azure Key Vault**
   ```bash
   az keyvault secret set \
     --vault-name ai-monitoring-kv \
     --name "azure-openai-key" \
     --value YOUR_KEY
   ```

2. **Enable authentication on chat endpoint**
   - Add Azure AD or API key validation

3. **Use Managed Identity**
   - Assign system-managed identity to Function App
   - Grant necessary Azure RBAC roles

4. **Network Security**
   - Configure VNET integration
   - Use private endpoints for Azure services
   - Enable firewall rules

5. **Audit and Logging**
   - Enable audit logging for all operations
   - Monitor unauthorized access attempts
   - Archive logs to secure storage

## Cost Optimization

- **Event Hub:** Use standard tier, configure auto-scaling
- **Azure Search:** Use free tier for development, standard for production
- **Azure OpenAI:** Monitor token consumption, consider model optimization
- **Functions:** Use consumption plan, set memory limits appropriately
- **Storage:** Enable lifecycle policies for log retention

## Support and Contributing

For issues or questions:
1. Check the troubleshooting section above
2. Review Azure service documentation
3. Check function logs for detailed errors
4. Contact Azure Support for infrastructure issues

## License

MIT License - see LICENSE file for details
