# ============================================================
# RAG Agent API - Azure Services Setup Script
# Creates and configures all required Azure services
# ============================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$Location,
    
    [Parameter(Mandatory=$true)]
    [string]$EnvironmentName = "dev",
    
    [switch]$SkipAISearch,
    [switch]$SkipBlobStorage,
    [switch]$SkipDocumentIntelligence,
    [switch]$SkipApplicationInsights,
    [switch]$DryRun
)

# Colors for output
$colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
}

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    $color = $colors[$Type] ?? "White"
    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] $Message" -ForegroundColor $color
}

# ============================================================
# 1. VALIDATE AND AUTHENTICATE
# ============================================================
Write-Status "Starting Azure services setup..." "Info"
Write-Status "Validating Azure CLI and authentication..." "Info"

try {
    $azVersion = az --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI not installed"
    }
    Write-Status "? Azure CLI found" "Success"
} catch {
    Write-Status "? Azure CLI required. Install from: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli" "Error"
    exit 1
}

# Login if needed
Write-Status "Checking Azure authentication..." "Info"
$account = az account show 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Status "Please authenticate to Azure..." "Warning"
    az login
}

# Set subscription
az account set --subscription $SubscriptionId
Write-Status "? Subscription set: $SubscriptionId" "Success"

# ============================================================
# 2. CREATE RESOURCE GROUP
# ============================================================
Write-Status "Creating/Verifying resource group: $ResourceGroupName..." "Info"

if ($DryRun) {
    Write-Status "[DRY RUN] Would create resource group: $ResourceGroupName in $Location" "Warning"
} else {
    az group create `
        --name $ResourceGroupName `
        --location $Location `
        --output none
    Write-Status "? Resource group ready" "Success"
}

# ============================================================
# 3. CREATE AZURE OPENAI SERVICE
# ============================================================
$openaiName = "openai-${EnvironmentName}-$([guid]::NewGuid().ToString().Substring(0,8))"

Write-Status "Setting up Azure OpenAI Service: $openaiName..." "Info"

if ($DryRun) {
    Write-Status "[DRY RUN] Would create Azure OpenAI: $openaiName" "Warning"
} else {
    $openaiResult = az cognitiveservices account create `
        --name $openaiName `
        --resource-group $ResourceGroupName `
        --kind OpenAI `
        --sku s0 `
        --location $Location `
        --output json | ConvertFrom-Json
    
    Write-Status "? Azure OpenAI created: $($openaiResult.name)" "Success"
    
    # Get OpenAI endpoint and key
    $openaiEndpoint = $openaiResult.properties.endpoint
    $openaiKey = az cognitiveservices account keys list `
        --name $openaiName `
        --resource-group $ResourceGroupName `
        --output json | ConvertFrom-Json | Select-Object -ExpandProperty key1
    
    Write-Status "  Endpoint: $openaiEndpoint" "Info"
    Write-Status "  Key: $($openaiKey.Substring(0,10))..." "Info"
}

# ============================================================
# 4. CREATE AZURE OPENAI DEPLOYMENTS
# ============================================================
Write-Status "Deploying Azure OpenAI models..." "Info"

if (-not $DryRun) {
    # Deploy text-embedding-ada-002
    Write-Status "Deploying text-embedding-ada-002..." "Info"
    az cognitiveservices account deployment create `
        --name $openaiName `
        --resource-group $ResourceGroupName `
        --deployment-name "text-embedding-ada-002" `
        --model-name "text-embedding-ada-002" `
        --model-version "2" `
        --model-format OpenAI `
        --sku-name "Standard" `
        --sku-capacity 1 `
        --output none 2>$null || Write-Status "  Deployment may already exist or require SKU adjustment" "Warning"
    
    Write-Status "? text-embedding-ada-002 deployed" "Success"
    
    # Deploy gpt-35-turbo
    Write-Status "Deploying gpt-35-turbo..." "Info"
    az cognitiveservices account deployment create `
        --name $openaiName `
        --resource-group $ResourceGroupName `
        --deployment-name "gpt-35-turbo" `
        --model-name "gpt-35-turbo" `
        --model-version "1106" `
        --model-format OpenAI `
        --sku-name "Standard" `
        --sku-capacity 1 `
        --output none 2>$null || Write-Status "  Deployment may already exist or require SKU adjustment" "Warning"
    
    Write-Status "? gpt-35-turbo deployed" "Success"
}

# ============================================================
# 5. CREATE AZURE AI SEARCH (Optional)
# ============================================================
if (-not $SkipAISearch) {
    $searchName = "search-${EnvironmentName}-$([guid]::NewGuid().ToString().Substring(0,8))"
    
    Write-Status "Setting up Azure AI Search: $searchName..." "Info"
    
    if ($DryRun) {
        Write-Status "[DRY RUN] Would create AI Search: $searchName" "Warning"
    } else {
        $searchResult = az search service create `
            --name $searchName `
            --resource-group $ResourceGroupName `
            --sku basic `
            --location $Location `
            --output json | ConvertFrom-Json
        
        Write-Status "? Azure AI Search created: $($searchResult.name)" "Success"
        
        $searchEndpoint = $searchResult.properties.publicNetworkAccess -eq "Enabled" ? "https://$($searchResult.name).search.windows.net" : "N/A"
        $searchKey = az search admin-key show `
            --resource-group $ResourceGroupName `
            --service-name $searchName `
            --output json | ConvertFrom-Json | Select-Object -ExpandProperty primaryKey
        
        Write-Status "  Endpoint: $searchEndpoint" "Info"
        Write-Status "  Key: $($searchKey.Substring(0,10))..." "Info"
    }
} else {
    Write-Status "? Skipping Azure AI Search" "Info"
}

# ============================================================
# 6. CREATE AZURE BLOB STORAGE (Optional)
# ============================================================
if (-not $SkipBlobStorage) {
    $storageName = "storage${EnvironmentName}$([guid]::NewGuid().ToString().Substring(0,6) -replace '-','')"
    
    Write-Status "Setting up Azure Blob Storage: $storageName..." "Info"
    
    if ($DryRun) {
        Write-Status "[DRY RUN] Would create Storage Account: $storageName" "Warning"
    } else {
        $storageResult = az storage account create `
            --name $storageName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --sku Standard_LRS `
            --output json | ConvertFrom-Json
        
        Write-Status "? Azure Blob Storage created: $($storageResult.name)" "Success"
        
        # Get storage key
        $storageKey = az storage account keys list `
            --resource-group $ResourceGroupName `
            --account-name $storageName `
            --output json | ConvertFrom-Json | Select-Object -First 1 -ExpandProperty value
        
        Write-Status "  Account name: $storageName" "Info"
        Write-Status "  Key: $($storageKey.Substring(0,10))..." "Info"
    }
} else {
    Write-Status "? Skipping Azure Blob Storage" "Info"
}

# ============================================================
# 7. CREATE DOCUMENT INTELLIGENCE (Optional)
# ============================================================
if (-not $SkipDocumentIntelligence) {
    $docIntelligenceName = "docint-${EnvironmentName}-$([guid]::NewGuid().ToString().Substring(0,8))"
    
    Write-Status "Setting up Azure Document Intelligence: $docIntelligenceName..." "Info"
    
    if ($DryRun) {
        Write-Status "[DRY RUN] Would create Document Intelligence: $docIntelligenceName" "Warning"
    } else {
        $docIntelligenceResult = az cognitiveservices account create `
            --name $docIntelligenceName `
            --resource-group $ResourceGroupName `
            --kind FormRecognizer `
            --sku S0 `
            --location $Location `
            --output json | ConvertFrom-Json
        
        Write-Status "? Azure Document Intelligence created: $($docIntelligenceResult.name)" "Success"
        
        $docIntelligenceEndpoint = $docIntelligenceResult.properties.endpoint
        $docIntelligenceKey = az cognitiveservices account keys list `
            --name $docIntelligenceName `
            --resource-group $ResourceGroupName `
            --output json | ConvertFrom-Json | Select-Object -ExpandProperty key1
        
        Write-Status "  Endpoint: $docIntelligenceEndpoint" "Info"
        Write-Status "  Key: $($docIntelligenceKey.Substring(0,10))..." "Info"
    }
} else {
    Write-Status "? Skipping Azure Document Intelligence" "Info"
}

# ============================================================
# 8. CREATE APPLICATION INSIGHTS (Optional)
# ============================================================
if (-not $SkipApplicationInsights) {
    $appInsightsName = "insights-${EnvironmentName}-$([guid]::NewGuid().ToString().Substring(0,8))"
    
    Write-Status "Setting up Application Insights: $appInsightsName..." "Info"
    
    if ($DryRun) {
        Write-Status "[DRY RUN] Would create Application Insights: $appInsightsName" "Warning"
    } else {
        $appInsightsResult = az monitor app-insights component create `
            --app $appInsightsName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --application-type web `
            --output json | ConvertFrom-Json
        
        Write-Status "? Application Insights created: $($appInsightsResult.name)" "Success"
        
        $appInsightsKey = $appInsightsResult.instrumentationKey
        Write-Status "  Instrumentation Key: $appInsightsKey" "Info"
    }
} else {
    Write-Status "? Skipping Application Insights" "Info"
}

# ============================================================
# 9. GENERATE CONFIGURATION FILE
# ============================================================
Write-Status "Generating configuration file..." "Info"

$configContent = @"
# RAG Agent API - Azure Configuration
# Generated: $(Get-Date)
# Location: $Location
# Environment: $EnvironmentName

## Required Configuration - Update appsettings.json with these values

### Azure OpenAI Service
AZURE_OPENAI_ENDPOINT=$openaiEndpoint
AZURE_OPENAI_KEY=$openaiKey
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-ada-002
AZURE_OPENAI_CHAT_DEPLOYMENT=gpt-35-turbo

## Optional Configuration

### Azure AI Search (if enabled)
AZURE_SEARCH_ENDPOINT=https://$($searchName).search.windows.net
AZURE_SEARCH_KEY=$searchKey

### Azure Blob Storage (if enabled)
AZURE_STORAGE_ACCOUNT_NAME=$storageName
AZURE_STORAGE_ACCOUNT_KEY=$storageKey

### Document Intelligence (if enabled)
DOCUMENT_INTELLIGENCE_ENDPOINT=$docIntelligenceEndpoint
DOCUMENT_INTELLIGENCE_KEY=$docIntelligenceKey

### Application Insights (if enabled)
APPINSIGHTS_INSTRUMENTATION_KEY=$appInsightsKey

## Resource Group Information
RESOURCE_GROUP=$ResourceGroupName
LOCATION=$Location
SUBSCRIPTION_ID=$SubscriptionId
"@

$configPath = "azure-config-${EnvironmentName}.env"
$configContent | Out-File -FilePath $configPath -Encoding UTF8

Write-Status "? Configuration saved to: $configPath" "Success"

# ============================================================
# 10. SUMMARY
# ============================================================
Write-Status "" "Info"
Write-Status "========================================" "Info"
Write-Status "AZURE SETUP COMPLETED" "Success"
Write-Status "========================================" "Info"
Write-Status "" "Info"
Write-Status "Next Steps:" "Info"
Write-Status "1. Review azure-config-${EnvironmentName}.env for all credentials" "Info"
Write-Status "2. Update appsettings.json with values from the config file" "Info"
Write-Status "3. Keep the config file secure - NEVER commit to Git!" "Warning"
Write-Status "4. Test the connection with: dotnet run" "Info"
Write-Status "" "Info"
Write-Status "Resource Group: $ResourceGroupName" "Info"
Write-Status "Location: $Location" "Info"
Write-Status "" "Info"

# Generate JSON for appsettings
$jsonConfig = @{
    Azure = @{
        OpenAI = @{
            Endpoint = $openaiEndpoint
            Key = $openaiKey
            EmbeddingDeployment = "text-embedding-ada-002"
            ChatDeployment = "gpt-35-turbo"
        }
    }
} | ConvertTo-Json -Depth 3

Write-Status "Suggested appsettings.json configuration:" "Info"
Write-Status $jsonConfig "Info"
