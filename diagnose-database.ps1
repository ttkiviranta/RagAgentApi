# Complete database diagnosis script
# Run this in a separate PowerShell window (not VS terminal)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PostgreSQL Database Diagnosis" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check Docker
Write-Host "[1/5] Checking Docker container..." -ForegroundColor Yellow
try {
    $containerInfo = docker ps --filter "name=postgres-rag" --format "{{.Names}}: {{.Status}}" 2>$null
    if ($containerInfo) {
        Write-Host "      ? $containerInfo" -ForegroundColor Green
    } else {
        Write-Host "      ? Container not running!" -ForegroundColor Red
        Write-Host "      Run: docker start postgres-rag" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "      ? Docker error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Check if database exists
Write-Host "[2/5] Checking database..." -ForegroundColor Yellow
try {
    $dbCheck = docker exec postgres-rag psql -U postgres -lqt 2>$null | Select-String "ragagent"
    if ($dbCheck) {
        Write-Host "      ? Database 'ragagent' exists" -ForegroundColor Green
    } else {
        Write-Host "      ? Database 'ragagent' not found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "      ? Error checking database" -ForegroundColor Red
    exit 1
}

# Step 3: Check tables
Write-Host "[3/5] Checking tables..." -ForegroundColor Yellow
try {
    $tableCount = docker exec postgres-rag psql -U postgres -d ragagent -t -A -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';" 2>$null
    Write-Host "      ? Found $tableCount tables" -ForegroundColor Green
} catch {
    Write-Host "      ? Error checking tables" -ForegroundColor Red
}

# Step 4: Count conversations
Write-Host "[4/5] Counting conversations..." -ForegroundColor Yellow
try {
    $convCount = docker exec postgres-rag psql -U postgres -d ragagent -t -A -c 'SELECT COUNT(*) FROM "Conversations";' 2>$null
    
    if ($convCount -match '^\d+$') {
        if ([int]$convCount -gt 0) {
            Write-Host "      ? Found $convCount conversations" -ForegroundColor Green
            
            # Show sample
            Write-Host ""
            Write-Host "      Sample conversations:" -ForegroundColor Cyan
            docker exec postgres-rag psql -U postgres -d ragagent -c 'SELECT "Id", "Title", "MessageCount" FROM "Conversations" ORDER BY "CreatedAt" DESC LIMIT 3;' 2>$null
        } else {
            Write-Host "      ? Database is empty (0 conversations)" -ForegroundColor Yellow
            Write-Host "      This explains why UI shows no list!" -ForegroundColor Yellow
        }
    } else {
        Write-Host "      ? Could not count conversations" -ForegroundColor Red
    }
} catch {
    Write-Host "      ? Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 5: Count messages
Write-Host "[5/5] Counting messages..." -ForegroundColor Yellow
try {
    $msgCount = docker exec postgres-rag psql -U postgres -d ragagent -t -A -c 'SELECT COUNT(*) FROM "Messages";' 2>$null
    
    if ($msgCount -match '^\d+$') {
        Write-Host "      Found $msgCount messages" -ForegroundColor Gray
    }
} catch {
    Write-Host "      Could not count messages" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Diagnosis Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($convCount -match '^\d+$' -and [int]$convCount -eq 0) {
    Write-Host ""
    Write-Host "PROBLEM IDENTIFIED:" -ForegroundColor Red
    Write-Host "  • Docker container is running" -ForegroundColor White
    Write-Host "  • Database exists with correct schema" -ForegroundColor White
    Write-Host "  • BUT: No conversations in database" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "LIKELY CAUSE:" -ForegroundColor Yellow
    Write-Host "  Windows update restarted Docker without persistent volume" -ForegroundColor White
    Write-Host "  Previous data was stored in container, not persisted" -ForegroundColor White
    Write-Host ""
    Write-Host "SOLUTION:" -ForegroundColor Green
    Write-Host "  1. Start API (F5 in Visual Studio)" -ForegroundColor White
    Write-Host "  2. Start UI (separate VS or: cd RagAgentUI; dotnet run)" -ForegroundColor White
    Write-Host "  3. Click 'New Chat' button" -ForegroundColor White
    Write-Host "  4. Conversation list will appear!" -ForegroundColor White
    Write-Host ""
    Write-Host "TO PREVENT DATA LOSS IN FUTURE:" -ForegroundColor Cyan
    Write-Host "  Run: docker run --name postgres-rag-persistent \" -ForegroundColor Gray
    Write-Host "       -v pgdata:/var/lib/postgresql/data \" -ForegroundColor Gray
    Write-Host "       -p 5432:5432 -e POSTGRES_PASSWORD=ragagent123 \" -ForegroundColor Gray
    Write-Host "       -e POSTGRES_DB=ragagent -d ankane/pgvector" -ForegroundColor Gray
} elseif ($convCount -match '^\d+$' -and [int]$convCount -gt 0) {
    Write-Host ""
    Write-Host "STATUS: OK" -ForegroundColor Green
    Write-Host "  Database has conversations" -ForegroundColor White
    Write-Host ""
    Write-Host "If UI still shows empty:" -ForegroundColor Yellow
    Write-Host "  1. Check API is running (https://localhost:7000)" -ForegroundColor White
    Write-Host "  2. Check UI logs for errors" -ForegroundColor White
    Write-Host "  3. Check browser console (F12) for errors" -ForegroundColor White
}

Write-Host ""
