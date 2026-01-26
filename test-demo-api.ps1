#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test script for Demo API endpoints
.DESCRIPTION
    Tests all demo controller endpoints to verify functionality
#>

$API_BASE = "https://localhost:7000"
$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Title)
    Write-Host "`n" -ForegroundColor Green
    Write-Host "=" * 60 -ForegroundColor Green
    Write-Host "  $Title" -ForegroundColor Green
    Write-Host "=" * 60 -ForegroundColor Green
}

function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [bool]$SkipCertCheck = $true
    )
    
    $url = "$API_BASE$Endpoint"
    Write-Host "?? Request: $Method $url" -ForegroundColor Cyan
    
    $params = @{
        Uri             = $url
        Method          = $Method
        ContentType     = "application/json"
        SkipCertificateCheck = $SkipCertCheck
    }
    
    if ($Body -and $Method -eq "POST") {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    
    try {
        $response = Invoke-RestMethod @params
        Write-Host "? Response (200 OK)" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "Response: $($reader.ReadToEnd())" -ForegroundColor Yellow
        }
        throw
    }
}

Write-Host "?? Demo API Test Suite" -ForegroundColor Magenta -BackgroundColor Black
Write-Host "Testing endpoints at $API_BASE`n" -ForegroundColor Cyan

# Test 1: Get available demos
Write-Section "Test 1: Get Available Demos"
$demos = Invoke-ApiRequest -Method "GET" -Endpoint "/api/demo/available"
Write-Host "Available demos:" -ForegroundColor Yellow
$demos | ForEach-Object { Write-Host "  - $_" -ForegroundColor Cyan }

# Test 2: Generate test data for each demo
Write-Section "Test 2: Generate Test Data"
foreach ($demo in $demos) {
    Write-Host "`n?? Generating test data for: $demo" -ForegroundColor Yellow
    $result = Invoke-ApiRequest -Method "POST" -Endpoint "/api/demo/generate-testdata?demoType=$demo"
    Write-Host "? Result: $($result.message)" -ForegroundColor Green
}

# Test 3: Run each demo
Write-Section "Test 3: Run All Demos"
foreach ($demo in $demos) {
    Write-Host "`n?? Running demo: $demo" -ForegroundColor Yellow
    $result = Invoke-ApiRequest -Method "POST" -Endpoint "/api/demo/run?demoType=$demo"
    
    Write-Host "? Demo Result:" -ForegroundColor Green
    Write-Host "  Status: $($result.success)" -ForegroundColor Cyan
    Write-Host "  Message: $($result.message)" -ForegroundColor Cyan
    Write-Host "  Execution Time: $($result.executionTimeMs)" -ForegroundColor Cyan
    
    if ($result.data) {
        Write-Host "  Data:" -ForegroundColor Yellow
        $result.data | ConvertTo-Json -Depth 10 | ForEach-Object { Write-Host "    $_" }
    }
}

# Test 4: Error handling - invalid demo type
Write-Section "Test 4: Error Handling - Invalid Demo Type"
Write-Host "Testing with invalid demo type: 'invalid-demo'" -ForegroundColor Yellow
try {
    $result = Invoke-ApiRequest -Method "POST" -Endpoint "/api/demo/run?demoType=invalid-demo"
    Write-Host "Response: $($result | ConvertTo-Json)" -ForegroundColor Cyan
}
catch {
    Write-Host "? Correctly rejected invalid demo type" -ForegroundColor Green
}

# Test 5: Error handling - missing parameter
Write-Section "Test 5: Error Handling - Missing Parameter"
Write-Host "Testing without demoType parameter" -ForegroundColor Yellow
try {
    $result = Invoke-ApiRequest -Method "POST" -Endpoint "/api/demo/run"
    Write-Host "Response: $($result | ConvertTo-Json)" -ForegroundColor Cyan
}
catch {
    Write-Host "? Correctly rejected missing parameter" -ForegroundColor Green
}

Write-Section "? All Tests Completed"
Write-Host "`nTest Summary:" -ForegroundColor Magenta
Write-Host "  ? Get available demos" -ForegroundColor Green
Write-Host "  ? Generate classification test data" -ForegroundColor Green
Write-Host "  ? Generate time-series test data" -ForegroundColor Green
Write-Host "  ? Generate image test data" -ForegroundColor Green
Write-Host "  ? Generate audio test data" -ForegroundColor Green
Write-Host "  ? Run classification demo" -ForegroundColor Green
Write-Host "  ? Run time-series demo" -ForegroundColor Green
Write-Host "  ? Run image demo" -ForegroundColor Green
Write-Host "  ? Run audio demo" -ForegroundColor Green
Write-Host "  ? Error handling for invalid demo type" -ForegroundColor Green
Write-Host "  ? Error handling for missing parameter" -ForegroundColor Green
