#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Seed demo data into PostgreSQL database
.DESCRIPTION
    Generates test data and runs demos, storing results in PostgreSQL
    Requires API to be running and PostgreSQL configured
#>

param(
    [string]$ApiUrl = "https://localhost:7000",
    [int]$DelayMs = 500
)

# Disable SSL certificate validation for localhost
if (-not ([System.Management.Automation.PSTypeName]'ServerCertificateValidationCallback').Type) {
    $certCallback = @"
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    public class ServerCertificateValidationCallback
    {
        public static void Ignore()
        {
            if(ServicePointManager.ServerCertificateValidationCallback ==null)
            {
                ServicePointManager.ServerCertificateValidationCallback += 
                    delegate (Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
                    {
                        return true;
                    };
            }
        }
    }
"@
    Add-Type $certCallback
}
[ServerCertificateValidationCallback]::Ignore()

# Colors
$GREEN = "Green"
$YELLOW = "Yellow"
$RED = "Red"
$CYAN = "Cyan"

Write-Host "`n" -ForegroundColor $CYAN
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host "  PostgreSQL Demo Data Seeding" -ForegroundColor $CYAN
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host ""

# Check API health
Write-Host "?? Checking API health..." -ForegroundColor $CYAN

try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/api/demo/available" -Method GET
    Write-Host "? API is responding" -ForegroundColor $GREEN
    Write-Host "   Available demos: $($health -join ', ')" -ForegroundColor $CYAN
} catch {
    Write-Host "? API is not responding at $ApiUrl" -ForegroundColor $RED
    Write-Host "   Make sure API is running and PostgreSQL DataSource is configured" -ForegroundColor $YELLOW
    exit 1
}

Write-Host ""
Write-Host "?? Database Seeding Plan:" -ForegroundColor $CYAN
Write-Host "   1. Generate test data for all demos" -ForegroundColor $WHITE
Write-Host "   2. Run all demos and store results in PostgreSQL" -ForegroundColor $WHITE
Write-Host "   3. Verify data was persisted" -ForegroundColor $WHITE
Write-Host ""

$demos = @("classification", "time-series", "image", "audio")
$successCount = 0
$failureCount = 0
$results = @()

# Phase 1: Generate test data
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host "  Phase 1: Generating Test Data" -ForegroundColor $CYAN
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host ""

foreach ($demo in $demos) {
    Write-Host "?? Generating test data for: $demo" -ForegroundColor $YELLOW
    
    try {
        $response = Invoke-RestMethod `
            -Uri "$ApiUrl/api/demo/generate-testdata?demoType=$demo" `
            -Method POST `
            -ErrorAction Stop
        
        if ($response.success) {
            Write-Host "   ? Success: $($response.message)" -ForegroundColor $GREEN
            $successCount++
        } else {
            Write-Host "   ? Failed: $($response.message)" -ForegroundColor $RED
            $failureCount++
        }
    } catch {
        Write-Host "   ? Error: $($_.Exception.Message)" -ForegroundColor $RED
        $failureCount++
    }
    
    Start-Sleep -Milliseconds $DelayMs
}

Write-Host ""

# Phase 2: Run demos and store in database
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host "  Phase 2: Running Demos (storing in PostgreSQL)" -ForegroundColor $CYAN
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host ""

foreach ($demo in $demos) {
    Write-Host "?? Running demo: $demo" -ForegroundColor $YELLOW
    
    try {
        $response = Invoke-RestMethod `
            -Uri "$ApiUrl/api/demo/run?demoType=$demo" `
            -Method POST `
            -ErrorAction Stop
        
        if ($response.success) {
            Write-Host "   ? Success: $($response.message)" -ForegroundColor $GREEN
            Write-Host "   ??  Execution time: $($response.executionTimeMs)" -ForegroundColor $CYAN
            
            # Store result for verification
            $results += @{
                DemoType = $response.demoType
                Success = $response.success
                ExecutionTime = $response.executionTimeMs
            }
            
            $successCount++
        } else {
            Write-Host "   ? Failed: $($response.message)" -ForegroundColor $RED
            $failureCount++
        }
    } catch {
        Write-Host "   ? Error: $($_.Exception.Message)" -ForegroundColor $RED
        $failureCount++
    }
    
    Start-Sleep -Milliseconds $DelayMs
}

Write-Host ""

# Phase 3: Verification
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host "  Phase 3: Verification" -ForegroundColor $CYAN
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host ""

Write-Host "? Demo Results Summary:" -ForegroundColor $GREEN
foreach ($result in $results) {
    $status = if ($result.Success) { "?" } else { "?" }
    Write-Host "   $status $($result.DemoType): $($result.ExecutionTime)" -ForegroundColor $CYAN
}

Write-Host ""
Write-Host "?? Statistics:" -ForegroundColor $CYAN
Write-Host "   Total executions: $($successCount + $failureCount)" -ForegroundColor $WHITE
Write-Host "   Successful: $successCount" -ForegroundColor $GREEN
Write-Host "   Failed: $failureCount" -ForegroundColor $(if ($failureCount -gt 0) { $RED } else { $GREEN })

Write-Host ""
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host "  Next Steps" -ForegroundColor $CYAN
Write-Host "=" * 70 -ForegroundColor $CYAN
Write-Host ""
Write-Host "? Verify data in PostgreSQL:" -ForegroundColor $CYAN
Write-Host '  SELECT * FROM "DemoExecutions" ORDER BY "CreatedAt" DESC;' -ForegroundColor $WHITE
Write-Host ""
Write-Host "? Check demo statistics:" -ForegroundColor $CYAN
Write-Host '  SELECT "DemoType", COUNT(*) as "ExecutionCount", AVG("ExecutionTimeMs") as "AvgTime"' -ForegroundColor $WHITE
Write-Host '  FROM "DemoExecutions" GROUP BY "DemoType";' -ForegroundColor $WHITE
Write-Host ""
Write-Host "? Query results:" -ForegroundColor $CYAN
Write-Host '  SELECT "DemoType", "Success", "ExecutionTimeMs", "CreatedAt"' -ForegroundColor $WHITE
Write-Host '  FROM "DemoExecutions" WHERE "CreatedAt" > NOW() - INTERVAL '"'"'1 hour'"'"';' -ForegroundColor $WHITE
Write-Host ""

if ($failureCount -eq 0) {
    Write-Host "? Seeding completed successfully!" -ForegroundColor $GREEN
    exit 0
} else {
    Write-Host "??  Seeding completed with $failureCount failures" -ForegroundColor $YELLOW
    exit 1
}
