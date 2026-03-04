# AI Monitoring Agent - Deployment Guide

## Production Deployment Steps

### Phase 1: Azure Infrastructure Setup

#### 1.1 Create Resource Group

```bash
$rgName = "ai-monitoring-prod"
$location = "eastus"

az group create \
  --name $rgName \
  --location $location
```

#### 1.2 Create Storage Account

```bash
$saName = "aimonitoringsa$(Get-Random)"

az storage account create \
  --name $saName \
  --resource-group $rgName \
  --location $location \
  --kind StorageV2 \
  --sku Standard_LRS \
  --access-tier Hot \
  --https-only true
```

#### 1.3 Create Azure AI Search

```bash
$searchName = "ai-monitoring-search"

az search service create \
  --name $searchName \
  --resource-group $rgName \
  --location $location \
  --sku standard \
  --partition-count 1 \
  --replica-count 1
```

#### 1.4 Create Azure OpenAI

```bash
$openaiName = "ai-monitoring-openai"

az cognitiveservices account create \
  --name $openaiName \
  --resource-group $rgName \
  --kind OpenAI \
  --sku S0 \
  --location eastus \
  --custom-domain $openaiName
```

#### 1.5 Deploy OpenAI Models

```bash
# Deploy GPT-4o
az cognitiveservices account deployment create \
  --resource-group $rgName \
  --name $openaiName \
  --deployment-name gpt-4o \
  --model-name gpt-4o \
  --model-version "2024-05-13" \
  --sku-capacity 10 \
  --sku-name Standard

# Deploy Text Embedding Model
az cognitiveservices account deployment create \
  --resource-group $rgName \
  --name $openaiName \
  --deployment-name text-embedding-3-small \
  --model-name text-embedding-3-small \
  --model-version "1" \
  --sku-capacity 10 \
  --sku-name Standard
```

#### 1.6 Create Event Hubs

```bash
$nsName = "ai-monitoring-ns"
$hubName = "app-insights-events"

# Create namespace
az eventhubs namespace create \
  --resource-group $rgName \
  --name $nsName \
  --location $location \
  --sku Standard

# Create event hub
az eventhubs eventhub create \
  --resource-group $rgName \
  --namespace-name $nsName \
  --name $hubName \
  --partition-count 4 \
  --message-retention 7
```

#### 1.7 Create Application Insights

```bash
$appInsightsName = "ai-monitoring-insights"

az monitor app-insights component create \
  --app $appInsightsName \
  --resource-group $rgName \
  --location $location \
  --kind web \
  --retention-time 30 \
  --workspace /subscriptions/{subscription-id}/resourcegroups/{rg}/providers/microsoft.operationalinsights/workspaces/{workspace}
```

#### 1.8 Create Azure Key Vault

```bash
$kvName = "ai-monitoring-kv"

az keyvault create \
  --name $kvName \
  --resource-group $rgName \
  --location $location \
  --enabled-for-deployment true \
  --enabled-for-template-deployment true \
  --enabled-for-disk-encryption false \
  --sku standard
```

### Phase 2: Store Secrets in Key Vault

```bash
$kvName = "ai-monitoring-kv"

# OpenAI Secrets
az keyvault secret set \
  --vault-name $kvName \
  --name "azure-openai-endpoint" \
  --value "https://YOUR_OPENAI.openai.azure.com/"

az keyvault secret set \
  --vault-name $kvName \
  --name "azure-openai-key" \
  --value "YOUR_OPENAI_KEY"

# Search Secrets
az keyvault secret set \
  --vault-name $kvName \
  --name "azure-search-endpoint" \
  --value "https://YOUR_SEARCH.search.windows.net/"

az keyvault secret set \
  --vault-name $kvName \
  --name "azure-search-key" \
  --value "YOUR_SEARCH_KEY"

# Event Hub
az keyvault secret set \
  --vault-name $kvName \
  --name "event-hub-connection-string" \
  --value "Endpoint=sb://YOUR_NS.servicebus.windows.net/;..."

# DevOps
az keyvault secret set \
  --vault-name $kvName \
  --name "azure-devops-token" \
  --value "YOUR_PAT_TOKEN"

# Webhooks
az keyvault secret set \
  --vault-name $kvName \
  --name "teams-webhook-url" \
  --value "https://outlook.webhook.office.com/webhookb2/..."

az keyvault secret set \
  --vault-name $kvName \
  --name "slack-webhook-url" \
  --value "https://hooks.slack.com/services/..."
```

### Phase 3: Create Function App and Deploy Code

#### 3.1 Create Function App

```bash
$functionAppName = "ai-monitoring-functions"
$saConnectionString = "DefaultEndpointsProtocol=https;AccountName=$saName;AccountKey=$(az storage account keys list --resource-group $rgName --account-name $saName --query [0].value -o tsv);EndpointSuffix=core.windows.net"

az functionapp create \
  --resource-group $rgName \
  --consumption-plan-location $location \
  --runtime dotnet-isolated \
  --runtime-version 8.0 \
  --functions-version 4 \
  --name $functionAppName \
  --storage-account $saName \
  --assign-identity \
  --disable-app-insights false
```

#### 3.2 Configure Function App Settings from Key Vault

```bash
$functionAppName = "ai-monitoring-functions"
$kvName = "ai-monitoring-kv"
$rgName = "ai-monitoring-prod"

# Create Key Vault references for secrets
az functionapp config appsettings set \
  --name $functionAppName \
  --resource-group $rgName \
  --settings \
    "Azure:OpenAI:Endpoint=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/azure-openai-endpoint/)" \
    "Azure:OpenAI:Key=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/azure-openai-key/)" \
    "Azure:Search:Endpoint=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/azure-search-endpoint/)" \
    "Azure:Search:Key=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/azure-search-key/)" \
    "EventHubConnectionString=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/event-hub-connection-string/)" \
    "Azure:DevOps:Organization=https://dev.azure.com/YOUR_ORG" \
    "Azure:DevOps:Project=YOUR_PROJECT" \
    "Azure:DevOps:Token=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/azure-devops-token/)" \
    "TEAMS_WEBHOOK_URL=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/teams-webhook-url/)" \
    "SLACK_WEBHOOK_URL=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/slack-webhook-url/)" \
    "APPINSIGHTS_INSTRUMENTATIONKEY=$appInsightsKey"
```

#### 3.3 Grant Key Vault Access to Function App

```bash
$functionAppName = "ai-monitoring-functions"
$kvName = "ai-monitoring-kv"
$rgName = "ai-monitoring-prod"

# Get the managed identity of the function app
$principalId = az functionapp identity show \
  --name $functionAppName \
  --resource-group $rgName \
  --query principalId -o tsv

# Grant Key Vault access
az keyvault set-policy \
  --name $kvName \
  --object-id $principalId \
  --secret-permissions get list
```

#### 3.4 Publish Functions

```bash
# From the solution directory
cd AIMonitoringAgent

# Publish using Azure Functions Core Tools
func azure functionapp publish $functionAppName --build remote

# Or use .NET CLI with deployment
dotnet publish -c Release -o ./publish

# Then deploy using Azure CLI
az webapp deployment source config-zip \
  --resource-group $rgName \
  --name $functionAppName \
  --src ./publish.zip
```

### Phase 4: Configure Application Insights Export

1. Go to Application Insights resource
2. Navigate to **Continuous Export** (left menu)
3. Create new export with:
   - **Event type:** Exception
   - **Properties:** (select all)
   - **Destination:** Event Hubs
   - **Event Hubs namespace:** ai-monitoring-ns
   - **Event hub:** app-insights-events

### Phase 5: Register Teams Bot

#### 5.1 Create Bot Service in Azure

```bash
$botName = "ai-monitoring-bot"

az bot create \
  --app-type MultiTenant \
  --name $botName \
  --resource-group $rgName \
  --kind functions \
  --sku S1
```

#### 5.2 Get Bot Credentials

```bash
az bot identity show \
  --name $botName \
  --resource-group $rgName

# Get App ID and Password from Azure Portal
# Bot Service → AI Monitoring Bot → Configuration → Microsoft App ID
```

#### 5.3 Configure Teams Channel

1. Go to Bot Service in Azure Portal
2. Channels → Microsoft Teams
3. Configure and save
4. Get the Team channel ID from Teams app configuration

### Phase 6: Enable Application Insights Monitoring

```bash
$functionAppName = "ai-monitoring-functions"
$rgName = "ai-monitoring-prod"

# Enable Application Insights
az functionapp config appsettings set \
  --name $functionAppName \
  --resource-group $rgName \
  --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$(az monitor app-insights component show --app ai-monitoring-insights --resource-group $rgName --query instrumentationKey -o tsv)"

# Enable Application Insights profiler
az functionapp config appsettings set \
  --name $functionAppName \
  --resource-group $rgName \
  --settings "ApplicationInsightsAgent_EXTENSION_VERSION=~3"
```

### Phase 7: Testing

#### 7.1 Test Event Hub Trigger

```bash
# Send test exception to Event Hub
$ns = "ai-monitoring-ns"
$hub = "app-insights-events"

$connectionString = az eventhubs namespace authorization-rule keys list \
  --resource-group $rgName \
  --namespace-name $ns \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv

# Use a client to send test event (PowerShell or Node.js)
```

#### 7.2 Test Chat Endpoint

```bash
$functionAppUrl = "https://$functionAppName.azurewebsites.net"

Invoke-WebRequest -Uri "$functionAppUrl/api/chat" \
  -Method POST \
  -ContentType "application/json" \
  -Body '{"query":"Show me recent errors"}'
```

#### 7.3 Test Teams Bot

1. Add bot to Teams channel
2. Send a message to the bot
3. Verify response in Teams

### Phase 8: Configure Monitoring and Alerts

```bash
# Create alert for failed function executions
az monitor metrics alert create \
  --name "FunctionApp-HighFailureRate" \
  --resource-group $rgName \
  --scopes /subscriptions/{subscription-id}/resourcegroups/$rgName/providers/Microsoft.Web/sites/$functionAppName \
  --condition "avg Percentage >= 10" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action alert-action

# Create alert for event processing lag
az monitor metrics alert create \
  --name "EventHub-HighLag" \
  --resource-group $rgName \
  --scopes /subscriptions/{subscription-id}/resourcegroups/$rgName/providers/Microsoft.EventHub/namespaces/ai-monitoring-ns/eventhubs/$hubName \
  --condition "avg BacklogSize >= 1000" \
  --window-size 5m \
  --evaluation-frequency 1m
```

### Phase 9: Enable Autoscaling

For non-consumption plans:

```bash
# Create autoscale profile
az monitor autoscale create \
  --resource-group $rgName \
  --resource-name $functionAppName \
  --resource-type "Microsoft.Web/sites" \
  --resource-provider-namespace Microsoft.Web \
  --resource-parent-name $functionAppName \
  --resource-parent-type serverFarms \
  --min-count 2 \
  --max-count 10 \
  --resource-parent-namespace Microsoft.Web \
  --resource-parent-path appServicePlans/$(az appservice plan list --resource-group $rgName --query [0].name -o tsv)
```

## Rollback Procedure

If issues occur after deployment:

```bash
$functionAppName = "ai-monitoring-functions"
$rgName = "ai-monitoring-prod"

# Stop function app
az functionapp stop --name $functionAppName --resource-group $rgName

# Disable Event Hub trigger
az functionapp config appsettings set \
  --name $functionAppName \
  --resource-group $rgName \
  --settings "EventHubTrigger_Disabled=true"

# Restart function app
az functionapp start --name $functionAppName --resource-group $rgName

# Redeploy previous version if needed
func azure functionapp publish $functionAppName --build remote
```

## Post-Deployment Validation Checklist

- [ ] All secrets accessible from Key Vault
- [ ] Function App identity has Key Vault access
- [ ] Event Hub receives Application Insights events
- [ ] Chat endpoint returns 200 status
- [ ] Teams bot responds to messages
- [ ] Slack webhook delivers messages
- [ ] Vector memory stores error records
- [ ] LLM analysis generates correct output
- [ ] Deployment correlation works
- [ ] Application Insights shows function executions
- [ ] No errors in Application Insights logs
- [ ] Monitoring alerts are active

## Maintenance Tasks

### Weekly
- Review Application Insights failure rates
- Check Event Hub lag metrics
- Verify webhook deliveries

### Monthly
- Update Models in Azure OpenAI if available
- Optimize AI Search indexes
- Review and clean up old error records

### Quarterly
- Review security compliance
- Update dependencies
- Performance optimization review

## Troubleshooting Common Issues

### Functions Not Triggering
```bash
# Check Event Hub connection
az eventhubs namespace authorization-rule keys list \
  --resource-group $rgName \
  --namespace-name ai-monitoring-ns \
  --name RootManageSharedAccessKey

# Restart function app
az functionapp restart --name $functionAppName --resource-group $rgName
```

### LLM Not Responding
```bash
# Verify OpenAI deployment
az cognitiveservices account list-usages \
  --name ai-monitoring-openai \
  --resource-group $rgName

# Check quota limits
az cognitiveservices account list \
  --resource-group $rgName
```

### Search Index Issues
```bash
# Recreate index
# Delete and recreate using VectorMemoryStore.InitializeAsync()

# Rebuild indexes
az search service set --name ai-monitoring-search \
  --resource-group $rgName \
  --update-now
```
