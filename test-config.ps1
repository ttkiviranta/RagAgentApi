#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests Azure and database connections from appsettings.Development.json
#>

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Testing Configuration" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Lue konfiguraatio
$config = Get-Content "appsettings.Development.json" | ConvertFrom-Json

# Testaa Azure OpenAI
Write-Host "Testing Azure OpenAI Service..." -ForegroundColor Yellow
try {
    $endpoint = $config.Azure.OpenAI.Endpoint
    $key = $config.Azure.OpenAI.Key
    
    if ([string]::IsNullOrEmpty($endpoint) -or [string]::IsNullOrEmpty($key) -or $key.Contains("REPLACE")) {
        Write-Host "FAIL: Azure OpenAI credentials not configured" -ForegroundColor Red
    } else {
        Write-Host "PASS: Azure OpenAI endpoint configured: $endpoint" -ForegroundColor Green
    }
} catch {
    Write-Host "FAIL: Azure OpenAI test error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Testaa Azure Search
Write-Host "Testing Azure Cognitive Search..." -ForegroundColor Yellow
try {
    $endpoint = $config.Azure.Search.Endpoint
    $key = $config.Azure.Search.Key
    
    if ([string]::IsNullOrEmpty($endpoint) -or [string]::IsNullOrEmpty($key)) {
        Write-Host "FAIL: Azure Search credentials not configured" -ForegroundColor Red
    } else {
        Write-Host "PASS: Azure Search endpoint configured: $endpoint" -ForegroundColor Green
    }
} catch {
    Write-Host "FAIL: Azure Search test error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Testaa Azure Storage
Write-Host "Testing Azure Storage..." -ForegroundColor Yellow
try {
    $connString = $config.Azure.Storage.ConnectionString
    
    if ([string]::IsNullOrEmpty($connString) -or $connString.Contains("REPLACE")) {
        Write-Host "FAIL: Azure Storage credentials not configured" -ForegroundColor Red
    } else {
        if ($connString -match "AccountName=" -and $connString -match "AccountKey=") {
            Write-Host "PASS: Azure Storage connection string format valid" -ForegroundColor Green
        } else {
            Write-Host "FAIL: Azure Storage connection string format invalid" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "FAIL: Azure Storage test error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Testaa PostgreSQL
Write-Host "Testing PostgreSQL Connection..." -ForegroundColor Yellow
try {
    $pgConnString = $config.ConnectionStrings.PostgreSQL
    
    if ([string]::IsNullOrEmpty($pgConnString) -or $pgConnString.Contains("REPLACE")) {
        Write-Host "FAIL: PostgreSQL credentials not configured" -ForegroundColor Red
    } else {
        Write-Host "PASS: PostgreSQL connection string configured" -ForegroundColor Green

        # Yritä pingata porttia
        if ($pgConnString -match 'Host=([^;]+)') {
            $dbhost = $matches[1]
            $dbport = 5432
            if ($pgConnString -match 'Port=([^;]+)') {
                $dbport = $matches[1]
            }

            Write-Host "   Checking connection to $dbhost`:$dbport..." -ForegroundColor Gray

            try {
                $tcpClient = New-Object System.Net.Sockets.TcpClient
                $asyncConnect = $tcpClient.ConnectAsync($dbhost, [int]$dbport)
                $asyncConnect.Wait(3000) | Out-Null

                if ($tcpClient.Connected) {
                    Write-Host "   PASS: PostgreSQL port is accessible" -ForegroundColor Green
                    $tcpClient.Close()
                } else {
                    Write-Host "   WARN: PostgreSQL port $dbport not accessible (not started?)" -ForegroundColor Yellow
                }
            } catch {
                Write-Host "   WARN: PostgreSQL connection check failed (may not be running)" -ForegroundColor Yellow
            }
        }
    }
} catch {
    Write-Host "FAIL: PostgreSQL test error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Configuration Test Complete" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. If any tests failed, update appsettings.Development.json"
Write-Host "  2. Make sure PostgreSQL is running (docker, local installation, etc)"
Write-Host "  3. Run: dotnet run" -ForegroundColor Green
Write-Host ""
