# Test PostgreSQL connection and query conversations

Write-Host "=== Testing PostgreSQL Database Connection ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Database connection
Write-Host "1. Testing database connection..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "https://localhost:7000/api/database-test/connection" -Method Get -SkipCertificateCheck
    Write-Host "? Database connection: $($response.status)" -ForegroundColor Green
    Write-Host "  - Database: $($response.database)" -ForegroundColor Gray
    Write-Host "  - pgvector enabled: $($response.pgvector_enabled)" -ForegroundColor Gray
    Write-Host "  - Table count: $($response.table_count)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Database connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 2: Database stats
Write-Host "2. Getting database statistics..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "https://localhost:7000/api/database-test/stats" -Method Get -SkipCertificateCheck
    Write-Host "? Database stats retrieved" -ForegroundColor Green
    Write-Host "  - Documents: $($response.documents)" -ForegroundColor Gray
    Write-Host "  - Chunks: $($response.document_chunks)" -ForegroundColor Gray
    Write-Host "  - Conversations: $($response.conversations)" -ForegroundColor Gray
    Write-Host "  - Messages: $($response.messages)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed to get stats: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 3: Get conversations via API
Write-Host "3. Testing GET /api/conversations..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "https://localhost:7000/api/conversations" -Method Get -SkipCertificateCheck
    Write-Host "? API responded successfully" -ForegroundColor Green
    
    if ($response) {
        $count = ($response | Measure-Object).Count
        Write-Host "  - Found $count conversations" -ForegroundColor Gray
        
        if ($count -gt 0) {
            Write-Host ""
            Write-Host "  First conversation:" -ForegroundColor Gray
            Write-Host "    ID: $($response[0].id)" -ForegroundColor Gray
            Write-Host "    Title: $($response[0].title)" -ForegroundColor Gray
            Write-Host "    Messages: $($response[0].messageCount)" -ForegroundColor Gray
            Write-Host "    Created: $($response[0].createdAt)" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "  - Response is empty or null" -ForegroundColor Yellow
    }
    Write-Host ""
}
catch {
    Write-Host "? Failed to get conversations: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "  Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

# Test 4: Create a test conversation
Write-Host "4. Testing POST /api/conversations (create new)..." -ForegroundColor Yellow
try {
    $body = @{
        Title = "Test Conversation $(Get-Date -Format 'HH:mm:ss')"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "https://localhost:7000/api/conversations" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck
    Write-Host "? Created test conversation" -ForegroundColor Green
    Write-Host "  - ID: $($response.id)" -ForegroundColor Gray
    Write-Host "  - Title: $($response.title)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed to create conversation: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 5: Check Docker container
Write-Host "5. Checking Docker PostgreSQL container..." -ForegroundColor Yellow
try {
    $container = docker ps --filter "name=postgres" --format "{{.Names}}: {{.Status}}"
    if ($container) {
        Write-Host "? Docker container found: $container" -ForegroundColor Green
    }
    else {
        Write-Host "? No PostgreSQL container running" -ForegroundColor Red
        Write-Host "  Run: docker run --name postgres-rag -p 5432:5432 -e POSTGRES_PASSWORD=yourpassword -d ankane/pgvector" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "? Docker not available or error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
