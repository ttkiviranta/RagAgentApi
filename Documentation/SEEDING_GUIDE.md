# PostgreSQL Demo Data Seeding Guide

## Overview

This guide explains how to seed demo data into PostgreSQL using the configured Repository Pattern.

## Prerequisites

1. **API Running**: Start the API with PostgreSQL as the data source
   ```bash
   # In appsettings.json:
   "DemoSettings": {
     "DataSource": "postgres"
   }
   ```

2. **PostgreSQL Running**: Ensure PostgreSQL is accessible
   ```bash
   # Docker example
   docker ps | grep ragagentdb
   ```

3. **Migrations Applied**: Database tables must be created
   ```bash
   dotnet ef database update
   ```

## Method 1: PowerShell Script (Recommended)

### Quick Start

```powershell
# Run seeding script
./seed-postgres-demos.ps1

# Or specify custom API URL
./seed-postgres-demos.ps1 -ApiUrl "https://localhost:7000"
```

### What It Does

1. ? Validates API connectivity
2. ? Generates test data for all 4 demos
3. ? Runs each demo once
4. ? Stores results in PostgreSQL automatically
5. ? Reports statistics and results

### Output Example

```
======================================================================
  PostgreSQL Demo Data Seeding
======================================================================

?? Checking API health...
? API is responding
   Available demos: classification, time-series, image, audio

?? Database Seeding Plan:
   1. Generate test data for all demos
   2. Run all demos and store results in PostgreSQL
   3. Verify data was persisted

======================================================================
  Phase 1: Generating Test Data
======================================================================

?? Generating test data for: classification
   ? Success: Test data generated successfully...

... (similar for other demos)

======================================================================
  Phase 2: Running Demos (storing in PostgreSQL)
======================================================================

?? Running demo: classification
   ? Success: Classification demo completed successfully
   ??  Execution time: 2ms

... (similar for other demos)

?? Statistics:
   Total executions: 8
   Successful: 8
   Failed: 0

? Seeding completed successfully!
```

## Method 2: Manual API Calls

### Step 1: Generate Test Data

```bash
# Classification
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=classification"

# Time-Series
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=time-series"

# Image Processing
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=image"

# Audio Processing
curl -X POST "https://localhost:7000/api/demo/generate-testdata?demoType=audio"
```

### Step 2: Run Demos

```bash
# Classification
curl -X POST "https://localhost:7000/api/demo/run?demoType=classification"

# Time-Series
curl -X POST "https://localhost:7000/api/demo/run?demoType=time-series"

# Image Processing
curl -X POST "https://localhost:7000/api/demo/run?demoType=image"

# Audio Processing
curl -X POST "https://localhost:7000/api/demo/run?demoType=audio"
```

## Method 3: SQL Queries (Direct Database)

### Insert Test Execution Result Manually

```sql
INSERT INTO "DemoExecutions" 
("Id", "DemoType", "Success", "Message", "ResultData", "ExecutionTimeMs", "CreatedAt")
VALUES (
    gen_random_uuid(),
    'classification',
    true,
    'Manual test data',
    '{"total_samples": 20, "model_accuracy": "92.00%"}',
    2,
    NOW()
);
```

## Verification

### Check Data Was Stored

```sql
-- View all demo executions
SELECT * FROM "DemoExecutions" ORDER BY "CreatedAt" DESC;

-- Count by demo type
SELECT "DemoType", COUNT(*) FROM "DemoExecutions" GROUP BY "DemoType";

-- Check specific demo
SELECT * FROM "DemoExecutions" WHERE "DemoType" = 'classification';
```

### Using SQL Script

```powershell
# Run verification queries
psql -h localhost -p 5433 -U postgres -d ragagentdb -f queries-demo-verification.sql
```

## Configuration Options

### appsettings.json

```json
{
  "DemoSettings": {
    "DataSource": "postgres",
    "SaveResults": true,
    "ResultRetentionDays": 30
  }
}
```

| Setting | Default | Purpose |
|---------|---------|---------|
| `DataSource` | `local` | Set to `postgres` to use database |
| `SaveResults` | `true` | Save demo results to database |
| `ResultRetentionDays` | `30` | Auto-delete old results after N days |

## Troubleshooting

### Issue: "Database does not exist"

**Solution:**
```bash
# Ensure migrations are applied
dotnet ef database update
```

### Issue: "Connection string is invalid"

**Solution:**
```bash
# Check appsettings.json PostgresConnectionString
# Verify PostgreSQL is running on the correct port (5433)
# Test connection: psql -h localhost -p 5433 -U postgres
```

### Issue: "Permission denied"

**Solution:**
```sql
-- In PostgreSQL, grant permissions
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;
```

### Issue: "DemoExecutions table not found"

**Solution:**
```bash
# Recreate migrations and apply
dotnet ef migrations add AddDemoServices
dotnet ef database update
```

## Advanced Usage

### Seeding Multiple Times

```powershell
# Run seeding 3 times with 2-second delay
for ($i = 1; $i -le 3; $i++) {
    Write-Host "Seeding run $i..."
    ./seed-postgres-demos.ps1
    Start-Sleep -Seconds 2
}
```

### Performance Testing

```powershell
# Measure execution time
Measure-Command {
    ./seed-postgres-demos.ps1
}
```

### Extract Results to CSV

```sql
COPY (
    SELECT "DemoType", "Success", "ExecutionTimeMs", "CreatedAt"
    FROM "DemoExecutions"
    WHERE "CreatedAt" > NOW() - INTERVAL '24 hours'
)
TO '/tmp/demo_results.csv' WITH CSV HEADER;
```

## Monitoring & Analytics

### Real-time Stats

```sql
-- Success rate (last hour)
SELECT 
    "DemoType",
    COUNT(CASE WHEN "Success" THEN 1 END) * 100.0 / COUNT(*) as "SuccessRate_%"
FROM "DemoExecutions"
WHERE "CreatedAt" > NOW() - INTERVAL '1 hour'
GROUP BY "DemoType";
```

### Performance Trends

```sql
-- Average execution time by hour
SELECT 
    DATE_TRUNC('hour', "CreatedAt") as "Hour",
    AVG("ExecutionTimeMs")::numeric(10,2) as "AvgMs"
FROM "DemoExecutions"
GROUP BY DATE_TRUNC('hour', "CreatedAt")
ORDER BY "Hour" DESC;
```

## Best Practices

### Development
```powershell
# Use local data source for speed
# "DemoSettings": { "DataSource": "local" }
./test-demo-api.ps1
```

### Testing
```powershell
# Use PostgreSQL for persistence
# "DemoSettings": { "DataSource": "postgres" }
./seed-postgres-demos.ps1
```

### Production
```powershell
# Run seeding on schedule (e.g., daily)
# Use cron or Task Scheduler
# Monitor via SQL queries

# Example: Weekly seeding
# Add to Task Scheduler or cron:
# 0 2 * * 0 pwsh -File "C:\path\to\seed-postgres-demos.ps1"
```

## Cleanup

### Delete Old Data

```sql
-- Delete demo executions older than 30 days
DELETE FROM "DemoExecutions"
WHERE "CreatedAt" < NOW() - INTERVAL '30 days';

-- Vacuum to reclaim space
VACUUM "DemoExecutions";
```

### Archive Results

```sql
-- Move old results to archive table (if exists)
INSERT INTO "DemoExecutions_Archive"
SELECT * FROM "DemoExecutions"
WHERE "CreatedAt" < NOW() - INTERVAL '90 days';

DELETE FROM "DemoExecutions"
WHERE "CreatedAt" < NOW() - INTERVAL '90 days';
```

## Support

- Check `Documentation/DEMO_REPOSITORY_PATTERN.md` for architecture details
- Review `queries-demo-verification.sql` for more SQL examples
- See `seed-postgres-demos.ps1` source code for customization

## Next Steps

1. ? Run `./seed-postgres-demos.ps1`
2. ? Verify data with SQL queries
3. ? Set up automated scheduling (optional)
4. ? Monitor performance trends
5. ? Integrate with Vue UI for visualization
