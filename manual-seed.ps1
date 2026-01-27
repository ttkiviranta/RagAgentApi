#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Simple manual seed - directly run demo endpoints and show results
.DESCRIPTION
    No complex PowerShell functions, just straightforward API calls
#>

Write-Host ""
Write-Host "========== DEMO SEEDING - MANUAL MODE ==========" -ForegroundColor Cyan
Write-Host ""

$demos = @("classification", "time-series", "image", "audio")
$baseUrl = "https://localhost:7000/api/demo"

# Disable SSL verification for self-signed certificates
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Test 1: Get available demos
Write-Host "1??  Getting available demos..." -ForegroundColor Yellow
try {
    $url = "$baseUrl/available"
    $result = Invoke-WebRequest -Uri $url -UseBasicParsing
    $content = $result.Content | ConvertFrom-Json
    Write-Host "? Available: $($content -join ', ')" -ForegroundColor Green
} catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 2: Generate test data
Write-Host "2??  Generating test data..." -ForegroundColor Yellow
foreach ($demo in $demos) {
    try {
        $url = "$baseUrl/generate-testdata?demoType=$demo"
        Write-Host "   - $demo - " -NoNewline
        $result = Invoke-WebRequest -Uri $url -Method POST -UseBasicParsing
        Write-Host "?" -ForegroundColor Green
    } catch {
        Write-Host "?" -ForegroundColor Red
    }
}

Write-Host ""

# Test 3: Run demos
Write-Host "3??  Running demos..." -ForegroundColor Yellow
$results = @()
foreach ($demo in $demos) {
    try {
        $url = "$baseUrl/run?demoType=$demo"
        Write-Host "   - $demo - " -NoNewline
        $result = Invoke-WebRequest -Uri $url -Method POST -UseBasicParsing
        $json = $result.Content | ConvertFrom-Json
        
        if ($json.success) {
            Write-Host "? ($($json.executionTimeMs))" -ForegroundColor Green
            $results += $json
        } else {
            Write-Host "? ($($json.message))" -ForegroundColor Red
        }
    } catch {
        Write-Host "? Error" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========== SUMMARY ==========" -ForegroundColor Cyan
Write-Host "Total demos run: $($results.Count)" -ForegroundColor Cyan
Write-Host "Successful: $(($results | Where-Object {$_.success}).Count)" -ForegroundColor Green

Write-Host ""
Write-Host "? Seeding complete! Data should now be in PostgreSQL." -ForegroundColor Green
Write-Host ""
Write-Host "?? To verify in PostgreSQL, run:" -ForegroundColor Cyan
Write-Host "   SELECT COUNT(*) FROM ""DemoExecutions"";" -ForegroundColor White
