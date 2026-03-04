# AI Monitoring Agent - Quick Start Guide

## 5-Minute Setup for Local Development

### Step 1: Prerequisites
```bash
# Verify .NET 8 SDK
dotnet --version  # Should output 8.x.x

# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

### Step 2: Configure Local Settings

```bash
cd AIMonitoringAgent/AIMonitoringAgent.Functions

# Create local.settings.json
copy local.settings.json.template local.settings.json
```

Edit `local.settings.json` - minimally configure:

```json
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureWebJobsStorage": "UseDevelopmentStorage=true"
  }
}
```

### Step 3: Build Solution

```bash
cd ../..
dotnet restore
dotnet build
```

### Step 4: Run Functions Locally

```bash
cd AIMonitoringAgent/AIMonitoringAgent.Functions
func start
```

You should see:
```
Azure Functions Core Tools
Version 4.x.x ...
Now listening on: http://0.0.0.0:7071
...
```

### Step 5: Test Chat Endpoint

Open new PowerShell window:

```powershell
$body = @{
    conversationId = "test-123"
    query = "Show me recent errors"
} | ConvertTo-Json

Invoke-WebRequest `
  -Uri "http://localhost:7071/api/chat" `
  -Method POST `
  -ContentType "application/json" `
  -Body $body
```

You should get a 200 response (even without Azure services configured - uses mock implementations).

## Full Azure Setup (20 minutes)

### Prerequisites
- Azure CLI installed: `az --version`
- Azure subscription with sufficient quota
- PowerShell 7+ or Azure CLI

### 1. Login to Azure

```bash
az login
az account show
```

### 2. Deploy Infrastructure (Automated)

Create `deploy.ps1`:

```powershell
param(
    [string]$ResourceGroup = "ai-monitoring-rg",
    [string]$Location = "eastus"
)

# Create resource group
Write-Host "Creating resource group..."
az group create --name $ResourceGroup --location $Location

# Create storage account
$storageAccount = "aimonitoringsa$(Get-Random)"
Write-Host "Creating storage account: $storageAccount"
az storage account create `
    --resource-group $ResourceGroup `
    --name $storageAccount `
    --location $Location `
    --kind StorageV2 `
    --sku Standard_LRS

# Create Event Hubs
Write-Host "Creating Event Hubs..."
az eventhubs namespace create `
    --resource-group $ResourceGroup `
    --name "ai-monitoring-ns-$(Get-Random)" `
    --location $Location `
    --sku Standard

az eventhubs eventhub create `
    --resource-group $ResourceGroup `
    --namespace-name "ai-monitoring-ns" `
    --name "app-insights-events" `
    --partition-count 4

# Create Azure Search
Write-Host "Creating Azure Search..."
az search service create `
    --resource-group $ResourceGroup `
    --name "ai-monitoring-search" `
    --location $Location `
    --sku standard

# Create Application Insights
Write-Host "Creating Application Insights..."
az monitor app-insights component create `
    --app "ai-monitoring-insights" `
    --resource-group $ResourceGroup `
    --location $Location

Write-Host "Infrastructure created successfully!"
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Storage Account: $storageAccount"
```

Run it:
```bash
./deploy.ps1
```

### 3. Configure Local Settings

Get Azure credentials:

```bash
# Event Hub Connection String
$connStr = az eventhubs namespace authorization-rule keys list `
    --resource-group ai-monitoring-rg `
    --namespace-name ai-monitoring-ns `
    --name RootManageSharedAccessKey `
    --query primaryConnectionString -o tsv

echo $connStr
```

Update `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=YOUR_SA;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net",
    "EventHubConnectionString": "PASTE_CONNECTION_STRING_HERE"
  }
}
```

### 4. Test With Azure Services

```bash
cd AIMonitoringAgent/AIMonitoringAgent.Functions
func start
```

Open browser: `http://localhost:7071/`

Test chat endpoint with curl:
```bash
curl -X POST http://localhost:7071/api/chat \
  -H "Content-Type: application/json" \
  -d '{"conversationId":"test","query":"Show me errors"}'
```

## Integration Examples

### Send Test Exception via PowerShell

```powershell
$exception = @{
    timestamp = (Get-Date).ToUniversalTime().ToString("O")
    operationName = "GetUser"
    exceptionType = "NullReferenceException"
    message = "Object reference not set to an instance of an object"
    stackTrace = "at MyApp.Services.UserService.GetUser(int userId) in Program.cs:line 42"
    requestId = "req-$(New-Guid)"
    customDimensions = @{
        "userId" = "12345"
        "environment" = "production"
    }
    customProperties = @{
        "application" = "MyApp"
    }
}

Invoke-WebRequest `
    -Uri "http://localhost:7071/admin/functions/EventHubTrigger/invoke" `
    -Method POST `
    -ContentType "application/json" `
    -Body ($exception | ConvertTo-Json)
```

### Use Teams Webhook

1. Get Teams webhook:
   - Teams → Channel → ⋯ (More options) → Connectors
   - Configure "Incoming Webhook"
   - Copy webhook URL

2. Update `local.settings.json`:
```json
{
  "Values": {
    "TEAMS_WEBHOOK_URL": "https://outlook.webhook.office.com/webhookb2/YOUR_WEBHOOK_ID"
  }
}
```

### Use Slack Webhook

1. Get Slack webhook:
   - Create Slack app: https://api.slack.com/apps
   - Add "Incoming Webhooks" feature
   - Create webhook for channel
   - Copy webhook URL

2. Update `local.settings.json`:
```json
{
  "Values": {
    "SLACK_WEBHOOK_URL": "https://hooks.slack.com/services/YOUR_WEBHOOK_PATH"
  }
}
```

## File Structure

```
AIMonitoringAgent/
├── AIMonitoringAgent.sln
├── README.md
├── DEPLOYMENT_GUIDE.md
│
├── AIMonitoringAgent.Shared/
│   ├── AIMonitoringAgent.Shared.csproj
│   ├── Models/
│   │   └── Models.cs
│   └── Services/
│       ├── ErrorParser.cs
│       ├── ErrorFingerprinter.cs
│       ├── VectorMemoryStore.cs
│       ├── LlmAnalyzer.cs
│       ├── EmbeddingService.cs
│       ├── DeploymentCorrelator.cs
│       ├── NotificationRouter.cs
│       └── ErrorOrchestrator.cs
│
├── AIMonitoringAgent.Functions/
│   ├── AIMonitoringAgent.Functions.csproj
│   ├── Program.cs
│   ├── host.json
│   ├── appsettings.json
│   ├── local.settings.json
│   ├── EventHubFunction.cs
│   ├── ChatFunction.cs
│   └── TeamsBotFunction.cs
│
└── AIMonitoringAgent.TeamsBot/
    ├── AIMonitoringAgent.TeamsBot.csproj
    ├── TeamsBotService.cs
    └── TeamsBotActivityHandler.cs
```

## Common Tasks

### View Logs Locally
```bash
func logs stream
```

### Debug in Visual Studio
1. Open `AIMonitoringAgent.sln` in Visual Studio 2022
2. Set breakpoints
3. Press F5 or Debug → Start Debugging

### Deploy to Azure
```bash
# From AIMonitoringAgent.Functions directory
func azure functionapp publish <FUNCTION_APP_NAME>
```

### View Azure Logs
```bash
az functionapp log tail --name <FUNCTION_APP_NAME> --resource-group <RG_NAME>
```

## Troubleshooting

### Functions Not Starting
```bash
# Check .NET version
dotnet --version

# Clear cache
dotnet nuget locals all --clear

# Rebuild
dotnet clean
dotnet build
```

### Port Already in Use
```bash
# Use different port
func start --port 7072
```

### Configuration Not Loading
```bash
# Check file exists and is valid JSON
cat local.settings.json

# Reset Function Core Tools
npm uninstall -g azure-functions-core-tools
npm install -g azure-functions-core-tools@4
```

## Next Steps

1. **Local Testing** - Run locally and test chat endpoint
2. **Azure Setup** - Complete DEPLOYMENT_GUIDE.md for production
3. **Teams Integration** - Register bot in Teams
4. **Configure Notifications** - Set up webhooks for Teams/Slack
5. **Deploy** - Publish to Azure Functions

## Support

- **Microsoft Docs**: https://docs.microsoft.com/en-us/azure/azure-functions/
- **Azure OpenAI**: https://learn.microsoft.com/en-us/azure/ai-services/openai/
- **Bot Framework**: https://docs.microsoft.com/en-us/bot-framework/
- **Azure Search**: https://docs.microsoft.com/en-us/azure/search/

## License

MIT License - see LICENSE file
