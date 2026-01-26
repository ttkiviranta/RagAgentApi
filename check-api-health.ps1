#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check API health status
.DESCRIPTION
    Simple health check for the API
#>

$API_BASE = "https://localhost:7000"

# Disable SSL certificate validation for localhost development
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

Write-Host "?? Checking API health..." -ForegroundColor Cyan
Write-Host "Target: $API_BASE" -ForegroundColor Yellow
Write-Host ""

# Test 1: Check if API is responding
try {
    $response = Invoke-RestMethod -Uri "$API_BASE/api/demo/available" -Method GET -ErrorAction Stop
    Write-Host "? API is running and responding" -ForegroundColor Green
    Write-Host "   Available demos: $($response -join ', ')" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "? API is NOT responding" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "?? To fix this:" -ForegroundColor Cyan
    Write-Host "   1. Open Visual Studio" -ForegroundColor White
    Write-Host "   2. Right-click on RagAgentApi project" -ForegroundColor White
    Write-Host "   3. Select 'Set as Startup Project'" -ForegroundColor White
    Write-Host "   4. Press F5 or click 'Start Debugging'" -ForegroundColor White
    Write-Host "   5. Wait for 'Now listening on: https://localhost:7000'" -ForegroundColor White
    Write-Host ""
    exit 1
}
