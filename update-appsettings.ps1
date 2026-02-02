# ============================================================
# Update appsettings.json with Azure Configuration
# ============================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$ConfigFile,
    
    [Parameter(Mandatory=$true)]
    [string]$Environment = "dev"
)

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    $colors = @{
        Success = "Green"
        Error = "Red"
        Warning = "Yellow"
        Info = "Cyan"
    }
    $color = $colors[$Type] ?? "White"
    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] $Message" -ForegroundColor $color
}

# ============================================================
# 1. READ CONFIGURATION FILE
# ============================================================
Write-Status "Reading Azure configuration from: $ConfigFile" "Info"

if (-not (Test-Path $ConfigFile)) {
    Write-Status "Configuration file not found: $ConfigFile" "Error"
    exit 1
}

$config = @{}
Get-Content $ConfigFile | ForEach-Object {
    if ($_ -match "^([^=]+)=(.*)$") {
        $config[$matches[1].Trim()] = $matches[2].Trim()
    }
}

Write-Status "? Configuration loaded with $($config.Count) settings" "Success"

# ============================================================
# 2. FIND APPSETTINGS FILE
# ============================================================
$appSettingsPath = "appsettings.json"
$appSettingsEnvPath = "appsettings.$Environment.json"

if (-not (Test-Path $appSettingsPath)) {
    Write-Status "appsettings.json not found" "Error"
    exit 1
}

Write-Status "Reading appsettings.json..." "Info"
$appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json

# ============================================================
# 3. UPDATE CONFIGURATION
# ============================================================
Write-Status "Updating Azure configuration..." "Info"

# Ensure Azure object exists
if (-not $appSettings.Azure) {
    $appSettings | Add-Member -Type NoteProperty -Name "Azure" -Value @{}
}

# Update OpenAI
if (-not $appSettings.Azure.OpenAI) {
    $appSettings.Azure | Add-Member -Type NoteProperty -Name "OpenAI" -Value @{}
}

if ($config["AZURE_OPENAI_ENDPOINT"]) {
    $appSettings.Azure.OpenAI.Endpoint = $config["AZURE_OPENAI_ENDPOINT"]
    Write-Status "  ? OpenAI Endpoint updated" "Success"
}

if ($config["AZURE_OPENAI_KEY"]) {
    $appSettings.Azure.OpenAI.Key = $config["AZURE_OPENAI_KEY"]
    Write-Status "  ? OpenAI Key updated" "Success"
}

if ($config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"]) {
    $appSettings.Azure.OpenAI.EmbeddingDeployment = $config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"]
    Write-Status "  ? Embedding Deployment updated" "Success"
}

if ($config["AZURE_OPENAI_CHAT_DEPLOYMENT"]) {
    $appSettings.Azure.OpenAI.ChatDeployment = $config["AZURE_OPENAI_CHAT_DEPLOYMENT"]
    Write-Status "  ? Chat Deployment updated" "Success"
}

# Update Search
if ($config["AZURE_SEARCH_ENDPOINT"]) {
    if (-not $appSettings.Azure.Search) {
        $appSettings.Azure | Add-Member -Type NoteProperty -Name "Search" -Value @{}
    }
    
    $appSettings.Azure.Search.Endpoint = $config["AZURE_SEARCH_ENDPOINT"]
    $appSettings.Azure.Search.Key = $config["AZURE_SEARCH_KEY"]
    Write-Status "  ? Search configuration updated" "Success"
}

# Update Storage
if ($config["AZURE_STORAGE_ACCOUNT_NAME"]) {
    if (-not $appSettings.Azure.Storage) {
        $appSettings.Azure | Add-Member -Type NoteProperty -Name "Storage" -Value @{}
    }
    
    $appSettings.Azure.Storage.AccountName = $config["AZURE_STORAGE_ACCOUNT_NAME"]
    $appSettings.Azure.Storage.AccountKey = $config["AZURE_STORAGE_ACCOUNT_KEY"]
    Write-Status "  ? Storage configuration updated" "Success"
}

# Update Document Intelligence
if ($config["DOCUMENT_INTELLIGENCE_ENDPOINT"]) {
    if (-not $appSettings.Azure.DocumentIntelligence) {
        $appSettings.Azure | Add-Member -Type NoteProperty -Name "DocumentIntelligence" -Value @{}
    }
    
    $appSettings.Azure.DocumentIntelligence.Endpoint = $config["DOCUMENT_INTELLIGENCE_ENDPOINT"]
    $appSettings.Azure.DocumentIntelligence.Key = $config["DOCUMENT_INTELLIGENCE_KEY"]
    Write-Status "  ? Document Intelligence updated" "Success"
}

# Update Application Insights
if ($config["APPINSIGHTS_INSTRUMENTATION_KEY"]) {
    if (-not $appSettings.Azure.ApplicationInsights) {
        $appSettings.Azure | Add-Member -Type NoteProperty -Name "ApplicationInsights" -Value @{}
    }
    
    $appSettings.Azure.ApplicationInsights.InstrumentationKey = $config["APPINSIGHTS_INSTRUMENTATION_KEY"]
    Write-Status "  ? Application Insights updated" "Success"
}

# ============================================================
# 4. SAVE UPDATED CONFIGURATION
# ============================================================
Write-Status "Saving updated configuration..." "Info"

$appSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $appSettingsPath -Encoding UTF8

Write-Status "? appsettings.json updated successfully" "Success"

# ============================================================
# 5. CREATE ENVIRONMENT-SPECIFIC FILE (if needed)
# ============================================================
if ($Environment -ne "" -and -not (Test-Path $appSettingsEnvPath)) {
    Write-Status "Creating environment-specific file: $appSettingsEnvPath" "Info"
    
    # Copy general settings for environment-specific file
    Get-Content $appSettingsPath | Out-File -FilePath $appSettingsEnvPath -Encoding UTF8
    Write-Status "? Created $appSettingsEnvPath" "Success"
}

Write-Status "" "Info"
Write-Status "========================================" "Info"
Write-Status "CONFIGURATION UPDATE COMPLETED" "Success"
Write-Status "========================================" "Info"
Write-Status "" "Info"
Write-Status "Next: Build and run the application" "Info"
Write-Status "  dotnet build" "Info"
Write-Status "  dotnet run" "Info"
