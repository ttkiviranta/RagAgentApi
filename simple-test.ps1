# Simple test - just check conversations endpoint

Write-Host "Testing Conversations API..." -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation
Add-Type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$apiBase = "https://localhost:7000"

# Test: Get conversations
Write-Host "Getting conversations from API..." -ForegroundColor Yellow
try {
    $conversations = Invoke-RestMethod -Uri "$apiBase/api/conversations" -Method Get -ErrorAction Stop
    
    if ($conversations -and $conversations.Count -gt 0) {
        Write-Host "? Found $($conversations.Count) conversations" -ForegroundColor Green
        Write-Host ""
        foreach ($conv in $conversations) {
            Write-Host "  • $($conv.title)" -ForegroundColor White
            Write-Host "    ID: $($conv.id)" -ForegroundColor Gray
            Write-Host "    Messages: $($conv.messageCount)" -ForegroundColor Gray
            Write-Host "    Created: $($conv.createdAt)" -ForegroundColor Gray
            Write-Host ""
        }
        
        Write-Host "SUCCESS! Database has conversations." -ForegroundColor Green
        Write-Host "If UI doesn't show them, check:" -ForegroundColor Yellow
        Write-Host "  1. UI is running (cd RagAgentUI; dotnet run)" -ForegroundColor White
        Write-Host "  2. Browser console (F12) for errors" -ForegroundColor White
        Write-Host "  3. API base URL in RagAgentUI\appsettings.json" -ForegroundColor White
    } else {
        Write-Host "? API returned 0 conversations" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Creating a test conversation..." -ForegroundColor Cyan
        
        $body = @{
            Title = "Test Chat $(Get-Date -Format 'HH:mm:ss')"
        } | ConvertTo-Json
        
        $newConv = Invoke-RestMethod -Uri "$apiBase/api/conversations" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
        Write-Host "? Created: $($newConv.title)" -ForegroundColor Green
        Write-Host "  ID: $($newConv.id)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Now start UI and you should see this conversation!" -ForegroundColor Cyan
    }
} catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure API is running on https://localhost:7000" -ForegroundColor Yellow
    Write-Host "Check Visual Studio output window for errors" -ForegroundColor Yellow
}

Write-Host ""
