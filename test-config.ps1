#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests Azure and database connections from appsettings.Development.json
.DESCRIPTION
    Validates all Azure services and PostgreSQL database configuration
#>

param(
    [string]$ConfigPath = "appsettings.Development.json"
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Azure & Database Connection Tester" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Lue konfiguraatio
if (!(Test-Path $ConfigPath)) {
    Write-Host "❌ Configuration file not found: $ConfigPath" -ForegroundColor Red
    exit 1
}

$config = Get-Content $ConfigPath | ConvertFrom-Json

# Testaa Azure OpenAI
Write-Host "🔍 Testing Azure OpenAI Service..." -ForegroundColor Yellow
try {
    $endpoint = $config.Azure.OpenAI.Endpoint
    $key = $config.Azure.OpenAI.Key
    
    if ([string]::IsNullOrEmpty($endpoint) -or [string]::IsNullOrEmpty($key) -or $key.Contains("REPLACE")) {
        Write-Host "❌ Azure OpenAI: Credentials not configured" -ForegroundColor Red
    } else {
        $uri = "$endpoint/openai/deployments?api-version=2024-02-15-preview"
        $headers = @{
            "api-key" = $key
            "Content-Type" = "application/json"
        }
        
        $response = Invoke-WebRequest -Uri $uri -Headers $headers -Method Get -ErrorAction Stop
        Write-Host "✅ Azure OpenAI: Connected successfully" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Azure OpenAI: Connection failed - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Testaa Azure Search
Write-Host "🔍 Testing Azure Cognitive Search..." -ForegroundColor Yellow
try {
    $endpoint = $config.Azure.Search.Endpoint
    $key = $config.Azure.Search.Key
    
    if ([string]::IsNullOrEmpty($endpoint) -or [string]::IsNullOrEmpty($key) -or $key.Contains("REPLACE")) {
        Write-Host "❌ Azure Search: Credentials not configured" -ForegroundColor Red
    } else {
        $uri = "$endpoint/indexes?api-version=2024-05-01-preview"
        $headers = @{
            "api-key" = $key
            "Content-Type" = "application/json"
        }
        
        $response = Invoke-WebRequest -Uri $uri -Headers $headers -Method Get -ErrorAction Stop
        Write-Host "✅ Azure Search: Connected successfully" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Azure Search: Connection failed - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Testaa Azure Storage
Write-Host "🔍 Testing Azure Storage..." -ForegroundColor Yellow
try {
    $connString = $config.Azure.Storage.ConnectionString
    
    if ([string]::IsNullOrEmpty($connString) -or $connString.Contains("REPLACE")) {
        Write-Host "❌ Azure Storage: Credentials not configured" -ForegroundColor Red
    } else {
        # Validoi connection string -formaatti
        if ($connString -match "AccountName=([^;]+)" -and $connString -match "AccountKey=([^;]+)") {
            Write-Host "✅ Azure Storage: Connection string format valid" -ForegroundColor Green
            Write-Host "   (Full validation requires Azure Storage SDK)" -ForegroundColor Gray
        } else {
            Write-Host "❌ Azure Storage: Invalid connection string format" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "❌ Azure Storage: Validation failed - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Testaa PostgreSQL
Write-Host "🔍 Testing PostgreSQL Connection..." -ForegroundColor Yellow
try {
    $pgConnString = $config.ConnectionStrings.PostgreSQL
    
    if ([string]::IsNullOrEmpty($pgConnString) -or $pgConnString.Contains("REPLACE")) {
        Write-Host "❌ PostgreSQL: Credentials not configured" -ForegroundColor Red
    } else {
        # Yritä parseta connection string
        $host = [regex]::Match($pgConnString, 'Host=([^;]+)').Groups[1].Value
        $port = [regex]::Match($pgConnString, 'Port=([^;]+)').Groups[1].Value
        $database = [regex]::Match($pgConnString, 'Database=([^;]+)').Groups[1].Value
        $user = [regex]::Match($pgConnString, 'Username=([^;]+)').Groups[1].Value
        
        Write-Host "   Parsed connection details:"
        Write-Host "   - Host: $host"
        Write-Host "   - Port: $port"
        Write-Host "   - Database: $database"
        Write-Host "   - User: $user"
        
        # Yritä pingata porttia
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.ConnectAsync($host, [int]$port).Wait(3000) | Out-Null
        
        if ($tcpClient.Connected) {
            Write-Host "✅ PostgreSQL: Port is accessible" -ForegroundColor Green
            Write-Host "   (Note: Full validation requires psql client)" -ForegroundColor Gray
            $tcpClient.Close()
        } else {
            Write-Host "❌ PostgreSQL: Port $port not accessible on $host" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "❌ PostgreSQL: Connection test failed - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Fix any failed tests above"
Write-Host "2. Run: dotnet run"
Write-Host "3. Check application logs for errors"
Write-Host ""
