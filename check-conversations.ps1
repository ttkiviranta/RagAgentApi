# Quick check for conversations in PostgreSQL database

Write-Host "=== Checking PostgreSQL Database ===" -ForegroundColor Cyan
Write-Host ""

# Check Docker container
Write-Host "1. Checking Docker container..." -ForegroundColor Yellow
$container = docker ps --filter "name=postgres-rag" --format "{{.Status}}"
if ($container) {
    Write-Host "   PostgreSQL container: $container" -ForegroundColor Green
} else {
    Write-Host "   ERROR: PostgreSQL container not running!" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Count conversations
Write-Host "2. Counting conversations..." -ForegroundColor Yellow
$sql = "SELECT COUNT(*) FROM ""Conversations"""
$result = docker exec postgres-rag psql -U postgres -d ragagent -t -A -c $sql

if ($LASTEXITCODE -eq 0) {
    Write-Host "   Total conversations: $result" -ForegroundColor Green
    
    if ($result -eq "0") {
        Write-Host ""
        Write-Host "   No conversations found - database is empty!" -ForegroundColor Yellow
        Write-Host "   This is why UI shows empty list." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   Solution: Start API and UI, then create your first conversation." -ForegroundColor Cyan
    }
} else {
    Write-Host "   ERROR: Failed to query database" -ForegroundColor Red
}
Write-Host ""

# Show recent conversations if any exist
if ($result -gt "0") {
    Write-Host "3. Recent conversations:" -ForegroundColor Yellow
    $sql2 = "SELECT ""Title"", ""MessageCount"", ""CreatedAt"" FROM ""Conversations"" ORDER BY ""LastMessageAt"" DESC LIMIT 3"
    docker exec postgres-rag psql -U postgres -d ragagent -c $sql2
}

Write-Host ""
Write-Host "=== Check Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Start RagAgentApi in Visual Studio (F5)" -ForegroundColor Gray
Write-Host "  2. Start RagAgentUI in another VS instance or terminal" -ForegroundColor Gray
Write-Host "  3. Click 'New Chat' button to create first conversation" -ForegroundColor Gray
