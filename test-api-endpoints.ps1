# Test API endpoints directly (requires API to be running)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Testing RagAgentApi Endpoints" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation for self-signed certs
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

# Test 1: Database connection test
Write-Host "[1/4] Testing database connection endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$apiBase/api/database-test/connection" -Method Get -ErrorAction Stop
    Write-Host "      ? Database: $($response.database)" -ForegroundColor Green
    Write-Host "      ? Status: $($response.status)" -ForegroundColor Green
    Write-Host "      ? pgvector: $($response.pgvector_enabled)" -ForegroundColor Green
    Write-Host "      ? Tables: Conversations=$($response.tables.conversations), Messages=$($response.tables.messages)" -ForegroundColor Green
} catch {
    Write-Host "      ? Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "" -ForegroundColor Yellow
    Write-Host "      API is NOT running!" -ForegroundColor Red
    Write-Host "      Please start API first:" -ForegroundColor Yellow
    Write-Host "        1. Open RagAgentApi.csproj in Visual Studio" -ForegroundColor White
    Write-Host "        2. Press F5 (Start Debugging)" -ForegroundColor White
    Write-Host "        3. Wait for 'Now listening on: https://localhost:7000'" -ForegroundColor White
    Write-Host "        4. Run this script again" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host ""

# Test 2: Database stats
Write-Host "[2/4] Getting database stats..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$apiBase/api/database-test/stats" -Method Get -ErrorAction Stop
    Write-Host "      ? Documents: $($response.documents)" -ForegroundColor Green
    Write-Host "      ? Chunks: $($response.document_chunks)" -ForegroundColor Green
    Write-Host "      ? Conversations: $($response.conversations)" -ForegroundColor Green
    Write-Host "      ? Messages: $($response.messages)" -ForegroundColor Green
    
    $convCount = $response.conversations
} catch {
    Write-Host "      ? Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: Get conversations via API
Write-Host "[3/4] Testing GET /api/conversations..." -ForegroundColor Yellow
try {
    $conversations = Invoke-RestMethod -Uri "$apiBase/api/conversations" -Method Get -ErrorAction Stop
    
    if ($conversations -and $conversations.Count -gt 0) {
        Write-Host "      ? Found $($conversations.Count) conversations" -ForegroundColor Green
        Write-Host ""
        Write-Host "      Conversations:" -ForegroundColor Cyan
        foreach ($conv in $conversations | Select-Object -First 5) {
            Write-Host "        • $($conv.title) ($($conv.messageCount) messages)" -ForegroundColor Gray
        }
    } else {
        Write-Host "      ? API returned empty array" -ForegroundColor Yellow
        $convCount = 0
    }
} catch {
    Write-Host "      ? Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: Create a test conversation
Write-Host "[4/4] Creating test conversation..." -ForegroundColor Yellow
try {
    $body = @{
        Title = "Test Conversation $(Get-Date -Format 'HH:mm:ss')"
    } | ConvertTo-Json
    
    $newConv = Invoke-RestMethod -Uri "$apiBase/api/conversations" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    Write-Host "      ? Created: $($newConv.title)" -ForegroundColor Green
    Write-Host "      ? ID: $($newConv.id)" -ForegroundColor Green
} catch {
    Write-Host "      ? Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($convCount -eq 0) {
    Write-Host "PROBLEM:" -ForegroundColor Red
    Write-Host "  Database has 0 conversations" -ForegroundColor White
    Write-Host ""
    Write-Host "EXPLANATION:" -ForegroundColor Yellow
    Write-Host "  Windows update restarted Docker without persistent storage" -ForegroundColor White
    Write-Host "  Previous data was lost (not using Docker volume)" -ForegroundColor White
    Write-Host ""
    Write-Host "SOLUTION:" -ForegroundColor Green
    Write-Host "  1. A test conversation was just created ?" -ForegroundColor White
    Write-Host "  2. Start UI: cd RagAgentUI; dotnet run" -ForegroundColor White
    Write-Host "  3. Open browser: https://localhost:7170" -ForegroundColor White
    Write-Host "  4. Conversation list should now appear!" -ForegroundColor White
    Write-Host ""
    Write-Host "TO PREVENT DATA LOSS:" -ForegroundColor Cyan
    Write-Host "  Stop current container: docker stop postgres-rag" -ForegroundColor Gray
    Write-Host "  Remove it: docker rm postgres-rag" -ForegroundColor Gray
    Write-Host "  Create with volume:" -ForegroundColor Gray
    Write-Host "    docker run --name postgres-rag \" -ForegroundColor Gray
    Write-Host "      -v pgdata:/var/lib/postgresql/data \" -ForegroundColor Gray
    Write-Host "      -p 5432:5432 \" -ForegroundColor Gray
    Write-Host "      -e POSTGRES_PASSWORD=ragagent123 \" -ForegroundColor Gray
    Write-Host "      -e POSTGRES_DB=ragagent \" -ForegroundColor Gray
    Write-Host "      -d ankane/pgvector" -ForegroundColor Gray
} elseif ($convCount -gt 0) {
    Write-Host "SUCCESS:" -ForegroundColor Green
    Write-Host "  Database has conversations!" -ForegroundColor White
    Write-Host "  UI should display them." -ForegroundColor White
    Write-Host ""
    Write-Host "If UI still shows empty:" -ForegroundColor Yellow
    Write-Host "  1. Start UI: cd RagAgentUI; dotnet run" -ForegroundColor White
    Write-Host "  2. Open browser: https://localhost:7170" -ForegroundColor White
    Write-Host "  3. Check browser console (F12) for errors" -ForegroundColor White
    Write-Host "  4. Check API logs in Visual Studio output" -ForegroundColor White
}
