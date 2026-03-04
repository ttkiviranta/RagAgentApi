#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick start script for AI Monitoring Agent local development
.DESCRIPTION
    Sets up local environment and runs the Functions
.EXAMPLE
    .\Start-Development.ps1
#>

param(
    [switch]$Clean,
    [switch]$Build,
    [int]$Port = 7071
)

$ErrorActionPreference = "Stop"

# Colors for output
$InfoColor = "Cyan"
$SuccessColor = "Green"
$ErrorColor = "Red"
$WarningColor = "Yellow"

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor $InfoColor
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor $SuccessColor
}

function Write-WarningMessage {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor $WarningColor
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor $ErrorColor
}

# Header
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════╗" -ForegroundColor $InfoColor
Write-Host "║   AI Monitoring Agent - Development Start Script   ║" -ForegroundColor $InfoColor
Write-Host "╚════════════════════════════════════════════════════╝" -ForegroundColor $InfoColor
Write-Host ""

# Check prerequisites
Write-Info "Checking prerequisites..."

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Success "Found .NET SDK: $dotnetVersion"
} catch {
    Write-ErrorMessage ".NET 8.0 SDK not found. Please install from https://dotnet.microsoft.com/download"
    exit 1
}

# Check Azure Functions Core Tools
try {
    $funcVersion = func --version
    Write-Success "Found Azure Functions Core Tools: $funcVersion"
} catch {
    Write-WarningMessage "Azure Functions Core Tools not found. Installing..."
    npm install -g azure-functions-core-tools@4 --unsafe-perm true
    Write-Success "Azure Functions Core Tools installed"
}

# Change to project directory
$projectPath = Join-Path $PSScriptRoot "AIMonitoringAgent.Functions"
if (-not (Test-Path $projectPath)) {
    Write-ErrorMessage "AIMonitoringAgent.Functions folder not found at: $projectPath"
    exit 1
}

Push-Location $projectPath

try {
    # Clean if requested
    if ($Clean) {
        Write-Info "Cleaning solution..."
        dotnet clean | Out-Null
        Get-ChildItem -Recurse -Filter "bin" -Type Directory | Remove-Item -Recurse -Force
        Get-ChildItem -Recurse -Filter "obj" -Type Directory | Remove-Item -Recurse -Force
        Write-Success "Solution cleaned"
    }

    # Restore NuGet packages
    Write-Info "Restoring NuGet packages..."
    dotnet restore --quiet
    Write-Success "NuGet packages restored"

    # Build if requested or needed
    if ($Build -or $Clean) {
        Write-Info "Building solution..."
        $buildOutput = dotnet build 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Solution built successfully"
        } else {
            Write-ErrorMessage "Build failed:"
            Write-Host $buildOutput -ForegroundColor $ErrorColor
            exit 1
        }
    }

    # Check local.settings.json
    $localSettingsPath = Join-Path $projectPath "local.settings.json"

    if (-not (Test-Path $localSettingsPath)) {
        Write-WarningMessage "local.settings.json not found"
        Write-Info "Creating local.settings.json with minimal configuration..."

        $localSettings = @{
            IsEncrypted = $false
            Values = @{
                FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
                AzureWebJobsStorage = "UseDevelopmentStorage=true"
            }
        } | ConvertTo-Json

        Set-Content -Path $localSettingsPath -Value $localSettings
        Write-Success "Created local.settings.json (minimal setup)"
        Write-WarningMessage "⚠️  For full functionality, configure Azure settings in local.settings.json"
    } else {
        Write-Success "local.settings.json found"
    }

    # Start Functions
    Write-Host ""
    Write-Info "Starting Azure Functions..."
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor $InfoColor

    func start --port $Port

} finally {
    Pop-Location
}
