# RAG Agent API - Azure Infrastructure

This directory contains the Azure Bicep infrastructure-as-code templates for deploying the RAG Agent API.

## 📁 Structure

```
infra/
├── main.bicep                    # Main orchestrator template
├── parameters/
│   ├── dev.bicepparam           # Development environment parameters
│   └── prod.bicepparam          # Production environment parameters
└── modules/
    ├── openai.bicep             # Azure OpenAI (GPT-35-Turbo, text-embedding-ada-002)
    ├── postgres.bicep           # PostgreSQL Flexible Server with pgvector
    ├── insights.bicep           # Application Insights + Log Analytics
    ├── storage.bicep            # Azure Storage Account
    ├── acs.bicep                # Azure Communication Services (Email)
    ├── keyvault.bicep           # Azure Key Vault
    ├── app.bicep                # App Service (API + UI)
    └── docintelligence.bicep    # Document Intelligence (optional)
```

## 🚀 Deployment

### Prerequisites

1. Azure CLI installed and logged in
2. Bicep CLI installed (`az bicep install`)
3. Azure subscription with required permissions

### Deploy to Development

```powershell
# Create resource group
az group create --name rg-ragagent-dev --location swedencentral

# Deploy infrastructure
az deployment group create \
  --resource-group rg-ragagent-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters postgresAdminPassword='<secure-password>'
```

### Deploy to Production

```powershell
# Create resource group
az group create --name rg-ragagent-prod --location swedencentral

# Deploy infrastructure
az deployment group create \
  --resource-group rg-ragagent-prod \
  --template-file infra/main.bicep \
  --parameters infra/parameters/prod.bicepparam \
  --parameters postgresAdminPassword='<secure-password>'
```

### Using What-If (Preview Changes)

```powershell
az deployment group what-if \
  --resource-group rg-ragagent-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters postgresAdminPassword='<secure-password>'
```

## 📦 Resources Deployed

| Resource | Purpose | Module |
|----------|---------|--------|
| Azure OpenAI | GPT-35-Turbo & text-embedding-ada-002 | `openai.bicep` |
| PostgreSQL Flexible Server | Vector database with pgvector | `postgres.bicep` |
| Application Insights | Telemetry and monitoring | `insights.bicep` |
| Log Analytics Workspace | Log storage and queries | `insights.bicep` |
| Storage Account | Document storage | `storage.bicep` |
| Azure Communication Services | Email notifications | `acs.bicep` |
| Key Vault | Secrets management | `keyvault.bicep` |
| App Service Plan | Hosting plan | `app.bicep` |
| Web App (API) | RAG Agent API | `app.bicep` |
| Web App (UI) | Blazor UI | `app.bicep` |
| Document Intelligence | PDF processing (optional) | `docintelligence.bicep` |

## ⚙️ Configuration

### Environment-Specific SKUs

| Resource | Dev | Prod |
|----------|-----|------|
| PostgreSQL | Standard_B1ms (Burstable) | Standard_D2s_v3 (GeneralPurpose) |
| App Service | B1 | P1v3 |
| Storage | Standard_LRS | Standard_GRS |
| Document Intelligence | Not deployed | S0 |

### Key Outputs

After deployment, these values are available:

- `apiUrl` - API endpoint URL
- `uiUrl` - UI endpoint URL
- `openAiEndpoint` - Azure OpenAI endpoint
- `keyVaultUri` - Key Vault URI for secrets
- `postgresConnectionString` - Database connection string
- `storageConnectionString` - Storage connection string

### Retrieve Outputs

```powershell
az deployment group show \
  --resource-group rg-ragagent-dev \
  --name main \
  --query properties.outputs
```

## 🔐 Security

- All secrets are stored in Azure Key Vault
- App Services use Managed Identity for Key Vault access
- HTTPS enforced on all web apps
- PostgreSQL uses SSL/TLS connections
- Storage uses private containers (no public access)

## 🔧 Post-Deployment Steps

1. **Run Database Migrations**
   ```bash
   dotnet ef database update --project RagAgentApi
   ```

2. **Deploy Application Code**
   ```powershell
   # API
   az webapp deployment source config-zip \
     --resource-group rg-ragagent-dev \
     --name app-ragagent-dev \
     --src publish/api.zip
   
   # UI
   az webapp deployment source config-zip \
     --resource-group rg-ragagent-dev \
     --name app-ragagent-dev-ui \
     --src publish/ui.zip
   ```

3. **Verify ACS Email Domain**
   - Navigate to Azure Portal > Communication Services
   - Verify the email domain for sending emails

## 📊 Cost Estimation

### Development (~€150-200/month)
- PostgreSQL B1ms: ~€25/month
- App Service B1: ~€12/month
- Azure OpenAI: Pay-per-use (~€50-100/month)
- Storage: ~€5/month
- Other services: ~€30/month

### Production (~€400-600/month)
- PostgreSQL D2s_v3: ~€100/month
- App Service P1v3: ~€100/month
- Azure OpenAI: Pay-per-use (~€100-200/month)
- Storage (GRS): ~€20/month
- Document Intelligence: ~€50/month
- Other services: ~€50/month

## 🛠️ Troubleshooting

### Common Issues

1. **OpenAI quota exceeded**: Request quota increase in Azure Portal
2. **PostgreSQL connection failed**: Check firewall rules and SSL settings
3. **Key Vault access denied**: Verify managed identity role assignments
4. **ACS email not working**: Ensure email domain is verified

### Useful Commands

```powershell
# Check deployment status
az deployment group list --resource-group rg-ragagent-dev

# View deployment logs
az deployment group show \
  --resource-group rg-ragagent-dev \
  --name main

# Delete all resources
az group delete --name rg-ragagent-dev --yes
```
