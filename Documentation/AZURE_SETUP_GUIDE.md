# Azure Setup Guide - RAG Agent API

This guide will help you automate the setup of Azure services for the RAG Agent API using PowerShell scripts.

## ?? Prerequisites

### Before You Start
1. **Azure Subscription** - An active Azure account
2. **Azure CLI** - Install from: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
3. **PowerShell 7+** - Desktop version or PowerShell Core
4. **Sufficient Permissions** - Ability to create resources in your Azure subscription

### Verification
```powershell
# Verify Azure CLI
az --version

# Verify PowerShell
$PSVersionTable.PSVersion
```

---

## ?? Quick Start

### 1. Gather Required Information
```powershell
# List available subscriptions
az account list --output table

# Set your desired subscription
$SubscriptionId = "your-subscription-id-here"

# List available locations
az account list-locations --output table
```

### 2. Run the Setup
```powershell
# Navigate to project directory
cd C:\Users\ttkiv\source\repos\RagAgentApi

# Execute setup
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-rg" `
    -Location "westeurope" `
    -EnvironmentName "dev"
```

---

## ?? Script Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `SubscriptionId` | string | ? | Your Azure subscription ID |
| `ResourceGroupName` | string | ? | Name of the new resource group |
| `Location` | string | ? | Azure region (e.g., westeurope, eastus) |
| `EnvironmentName` | string | | Environment identifier (dev, staging, prod) |
| `SkipAISearch` | switch | | Skip Azure AI Search creation |
| `SkipBlobStorage` | switch | | Skip Azure Blob Storage creation |
| `SkipDocumentIntelligence` | switch | | Skip Document Intelligence creation |
| `SkipApplicationInsights` | switch | | Skip Application Insights creation |
| `DryRun` | switch | | Simulate without making actual changes |

---

## ?? Usage Examples

### Example 1: Basic Setup (Azure OpenAI Only)
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-dev" `
    -Location "westeurope"
```

### Example 2: Complete Setup (Recommended for Production)
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-prod" `
    -Location "westeurope" `
    -EnvironmentName "prod"
```

### Example 3: Test Without Making Changes
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-test" `
    -Location "westeurope" `
    -DryRun
```

### Example 4: Minimal Setup
```powershell
.\setup-azure-services.ps1 `
    -SubscriptionId "00000000-0000-0000-0000-000000000000" `
    -ResourceGroupName "rag-agent-minimal" `
    -Location "westeurope" `
    -SkipAISearch `
    -SkipBlobStorage `
    -SkipDocumentIntelligence `
    -SkipApplicationInsights
```

---

## ?? Services and Their Roles

### Required ?
- **Azure OpenAI Service** - Text embeddings and chat completions

### Optional (Recommended)
- **Azure AI Search** - Vector and full-text search (legacy)
- **Azure Blob Storage** - Document storage
- **Application Insights** - Logging and telemetry monitoring

### Optional
- **Document Intelligence** - Document recognition and parsing

---

## ?? Script Output

The script generates a file `azure-config-{environment}.env` containing:
```
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/
AZURE_OPENAI_KEY=your-key-here
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-ada-002
AZURE_OPENAI_CHAT_DEPLOYMENT=gpt-35-turbo
...
```

---

## ? Configuration Finalization

### 1. Copy Values to appsettings Files

**appsettings.json**
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
    },
    "Search": {
      "Endpoint": "https://your-search.search.windows.net",
      "Key": "your-search-key"
    },
    "Storage": {
      "AccountName": "yourstorageaccount",
      "AccountKey": "your-storage-key"
    },
    "DocumentIntelligence": {
      "Endpoint": "https://your-docint.cognitiveservices.azure.com/",
      "Key": "your-docint-key"
    },
    "ApplicationInsights": {
      "InstrumentationKey": "your-appinsights-key"
    }
  }
}
```

### 2. Secure Configuration Files
```powershell
# Add to .gitignore
echo "azure-config-*.env" >> .gitignore
echo "appsettings*.json" >> .gitignore

# Never commit configuration files!
```

### 3. Test Connection
```bash
dotnet run
```

Navigate to `https://localhost:7000` and verify Swagger UI.

---

## ?? Troubleshooting

### Script says: "Azure CLI not installed"
```powershell
# Install Azure CLI
# Windows: choco install azure-cli
# Mac: brew install azure-cli
# Linux: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

### Script says: "Not authenticated"
```powershell
# Login to Azure
az login

# If you have multiple subscriptions, select the correct one
az account set --subscription "your-subscription-id"
```

### Deployment Fails
```powershell
# Check available SKUs
az cognitiveservices account list-kinds --location westeurope

# Check quotas
az cognitiveservices account list-usage --resource-group rag-agent-rg --name your-openai-name
```

### Service Not Found in Azure Portal
```powershell
# Verify resource group
az group list --output table

# List resources in resource group
az resource list --resource-group rag-agent-rg --output table
```

---

## ?? Verifying in Azure Portal

1. Go to https://portal.azure.com
2. Find your resource group: `rag-agent-rg` (or your custom name)
3. Verify created resources:
   - Azure OpenAI Service
   - Azure AI Search (if not skipped)
   - Storage Account (if not skipped)
   - Application Insights (if not skipped)

---

## ?? Deleting Resources (if needed)

```powershell
# Delete entire resource group (WARNING: permanent)
az group delete --name rag-agent-rg --yes

# Delete individual resource
az resource delete `
    --resource-group rag-agent-rg `
    --name your-openai-name `
    --resource-type "Microsoft.CognitiveServices/accounts"
```

---

## ?? Best Practices

? **Do**
- Use separate resource groups for dev/staging/prod environments
- Keep configuration files secure
- Use DryRun option first for testing
- Document script parameters used

? **Don't**
- Commit Azure keys to GitHub
- Share configuration files insecurely
- Use production keys in development
- Accidentally delete resource groups

---

## ?? Additional Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/)
- [Azure AI Search Documentation](https://learn.microsoft.com/en-us/azure/search/)
- [Azure CLI Reference](https://learn.microsoft.com/en-us/cli/azure/)
- [Azure PowerShell Documentation](https://learn.microsoft.com/en-us/powershell/azure/)

---

## ?? Need Help?

If you encounter issues:
1. Check logs: `$HOME\.azure\cmdline_*_log.txt`
2. Use `-Verbose` flag for additional output
3. See [Azure Support](https://azure.microsoft.com/en-us/support/)
