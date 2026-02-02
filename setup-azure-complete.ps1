# ============================================================
# Complete Azure Setup Automation Script
# Orchestrates full setup from services to configuration
# ============================================================

param(
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location,
    
    [Parameter(Mandatory=$false)]
    [string]$EnvironmentName = "dev"
)

$ErrorActionPreference = "Stop"

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

function Read-UserInput {
    param([string]$Prompt, [string]$Default)
    
    if ($Default) {
        $input = Read-Host "$Prompt [$Default]"
        return if ($input) { $input } else { $Default }
    } else {
        return Read-Host $Prompt
    }
}

# ============================================================
# 1. GATHER INFORMATION
# ============================================================
Write-Status "========================================" "Info"
Write-Status "AZURE SETUP - INTERACTIVE CONFIGURATION" "Cyan"
Write-Status "========================================" "Info"
Write-Status "" "Info"

# Get subscription
if (-not $SubscriptionId) {
    Write-Status "Listing available Azure subscriptions..." "Info"
    az account list --output table
    $SubscriptionId = Read-UserInput "Enter Subscription ID"
}

# Get resource group name
if (-not $ResourceGroupName) {
    $ResourceGroupName = Read-UserInput "Enter Resource Group name" "rag-agent-rg"
}

# Get location
if (-not $Location) {
    Write-Status "Common Azure locations: westeurope, eastus, uksouth, australiaeast" "Info"
    $Location = Read-UserInput "Enter Azure location" "westeurope"
}

# Get environment name
$EnvironmentName = Read-UserInput "Enter environment name (dev/staging/prod)" $EnvironmentName

# Ask about optional services
Write-Status "" "Info"
Write-Status "Optional Services:" "Info"

$includeSearch = Read-Host "Include Azure AI Search? (y/n) [y]"
$includeSearch = if ($includeSearch -eq "n") { $true } else { $false }

$includeStorage = Read-Host "Include Azure Blob Storage? (y/n) [y]"
$includeStorage = if ($includeStorage -eq "n") { $true } else { $false }

$includeDocInt = Read-Host "Include Document Intelligence? (y/n) [n]"
$includeDocInt = if ($includeDocInt -eq "y") { $false } else { $true }

$includeAppInsights = Read-Host "Include Application Insights? (y/n) [y]"
$includeAppInsights = if ($includeAppInsights -eq "n") { $true } else { $false }

# ============================================================
# 2. CONFIRM SETTINGS
# ============================================================
Write-Status "" "Info"
Write-Status "Confirming configuration:" "Info"
Write-Status "  Subscription ID: $SubscriptionId" "Info"
Write-Status "  Resource Group: $ResourceGroupName" "Info"
Write-Status "  Location: $Location" "Info"
Write-Status "  Environment: $EnvironmentName" "Info"
Write-Status "  AI Search: $(if ($includeSearch) { 'No' } else { 'Yes' })" "Info"
Write-Status "  Blob Storage: $(if ($includeStorage) { 'No' } else { 'Yes' })" "Info"
Write-Status "  Document Intelligence: $(if ($includeDocInt) { 'No' } else { 'Yes' })" "Info"
Write-Status "  App Insights: $(if ($includeAppInsights) { 'No' } else { 'Yes' })" "Info"
Write-Status "" "Info"

$confirm = Read-Host "Proceed with setup? (y/n)"
if ($confirm -ne "y") {
    Write-Status "Setup cancelled" "Warning"
    exit 0
}

# ============================================================
# 3. BUILD SCRIPT ARGUMENTS
# ============================================================
$setupArgs = @(
    "-SubscriptionId", $SubscriptionId,
    "-ResourceGroupName", $ResourceGroupName,
    "-Location", $Location,
    "-EnvironmentName", $EnvironmentName
)

if ($includeSearch) {
    $setupArgs += "-SkipAISearch"
}

if ($includeStorage) {
    $setupArgs += "-SkipBlobStorage"
}

if ($includeDocInt) {
    $setupArgs += "-SkipDocumentIntelligence"
}

if ($includeAppInsights) {
    $setupArgs += "-SkipApplicationInsights"
}

# ============================================================
# 4. RUN AZURE SETUP SCRIPT
# ============================================================
Write-Status "" "Info"
Write-Status "Starting Azure services setup..." "Info"
Write-Status "" "Info"

try {
    & ".\setup-azure-services.ps1" @setupArgs
} catch {
    Write-Status "Azure setup failed: $_" "Error"
    exit 1
}

# ============================================================
# 5. UPDATE APPSETTINGS
# ============================================================
Write-Status "" "Info"
Write-Status "Updating application configuration..." "Info"

$configFile = "azure-config-${EnvironmentName}.env"
$updateArgs = @(
    "-ConfigFile", $configFile,
    "-Environment", $EnvironmentName
)

try {
    & ".\update-appsettings.ps1" @updateArgs
} catch {
    Write-Status "Configuration update failed: $_" "Error"
    Write-Status "You can manually run: .\update-appsettings.ps1 -ConfigFile $configFile -Environment $EnvironmentName" "Warning"
}

# ============================================================
# 6. BUILD & TEST
# ============================================================
Write-Status "" "Info"
Write-Status "Building application..." "Info"

try {
    dotnet build
    Write-Status "? Build successful" "Success"
} catch {
    Write-Status "Build failed. Fix errors and run: dotnet build" "Error"
    exit 1
}

# ============================================================
# 7. FINAL SUMMARY
# ============================================================
Write-Status "" "Info"
Write-Status "========================================" "Success"
Write-Status "SETUP COMPLETE!" "Success"
Write-Status "========================================" "Success"
Write-Status "" "Info"

Write-Status "Configuration Summary:" "Info"
Write-Status "  Resource Group: $ResourceGroupName" "Info"
Write-Status "  Location: $Location" "Info"
Write-Status "  Environment: $EnvironmentName" "Info"
Write-Status "" "Info"

Write-Status "Next Steps:" "Info"
Write-Status "1. Review appsettings.json for any adjustments" "Info"
Write-Status "2. Start PostgreSQL: docker run -d --name ragagentdb -e POSTGRES_PASSWORD=YourPassword -p 5433:5432 pgvector/pgvector:pg16" "Info"
Write-Status "3. Run migrations: dotnet ef database update" "Info"
Write-Status "4. Start application: dotnet run" "Info"
Write-Status "5. Open Swagger: https://localhost:7000" "Info"
Write-Status "" "Info"

Write-Status "Important Files:" "Info"
Write-Status "  Config: $configFile" "Info"
Write-Status "  Settings: appsettings.json" "Info"
Write-Status "  Env-specific: appsettings.$EnvironmentName.json" "Info"
Write-Status "" "Info"

Write-Status "??  SECURITY REMINDER:" "Warning"
Write-Status "  - Add .gitignore entries for config files" "Warning"
Write-Status "  - Never commit sensitive credentials" "Warning"
Write-Status "  - Use Key Vault for production" "Warning"
