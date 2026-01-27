# Demo Services Data Repository Pattern

## Overview

The Demo Services now support a **flexible, configurable Repository Pattern** for test data sourcing and result persistence. This allows you to choose between local file storage, PostgreSQL persistence, or both simultaneously.

## Architecture

```
???????????????????????????????????????????
?        DemoServices                      ?
?  (Classification, TimeSeries, etc.)     ?
???????????????????????????????????????????
               ?
        ???????????????????????
        ?  ITestDataRepository ? (Interface)
        ???????????????????????
               ?
        ???????????????????????????????????
        ?                                  ?
  ?????????????????????????    ?????????????????????????
  ? LocalFileRepository   ?    ? PostgresRepository    ?
  ? (demos/ directory)    ?    ? (PostgreSQL tables)   ?
  ?????????????????????????    ?????????????????????????
```

## Configuration

### Enable Repository in appsettings.json

```json
{
  "DemoSettings": {
    "DataSource": "local",  // "local" or "postgres"
    "LocalDataPath": "demos/",
    "PostgresConnectionString": "Host=localhost;Port=5433;Database=ragagentdb;Username=postgres;Password=YOUR_PASSWORD",
    "SaveResults": true,
    "ResultRetentionDays": 30
  }
}
```

### Available Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `DataSource` | `local` | `local` = files, `postgres` = database |
| `LocalDataPath` | `demos/` | Base path for test data files |
| `PostgresConnectionString` | - | Connection string for PostgreSQL |
| `SaveResults` | `true` | Whether to save demo results |
| `ResultRetentionDays` | `30` | Days to keep results in database |

## Repository Implementations

### LocalFileRepository

**Stores data in local file system**

```csharp
// appsettings.json
{
  "DemoSettings": {
    "DataSource": "local"
  }
}
```

**File Structure:**
```
demos/
??? classification/
?   ??? data/
?   ?   ??? classification_training.csv
?   ??? results/
?       ??? result_20250127_090000.json
?       ??? result_20250127_091500.json
??? time-series/
?   ??? data/
?   ?   ??? timeseries_data.csv
?   ??? results/
??? image-processing/
?   ??? data/
?   ?   ??? test_image.png
?   ??? results/
??? audio-processing/
    ??? data/
    ?   ??? test_audio.wav
    ??? results/
```

**Advantages:**
- ? No database overhead
- ? Fast local access
- ? Easy file management
- ? Good for development/testing

**Disadvantages:**
- ? No central history
- ? Limited query capabilities
- ? File system dependent

### PostgresRepository

**Stores data in PostgreSQL database**

```csharp
// appsettings.json
{
  "DemoSettings": {
    "DataSource": "postgres",
    "PostgresConnectionString": "Host=localhost;Port=5433;Database=ragagentdb;..."
  }
}
```

**Database Tables:**

#### DemoExecution
```sql
CREATE TABLE "DemoExecutions" (
    "Id" uuid PRIMARY KEY,
    "DemoType" text NOT NULL,
    "Success" boolean NOT NULL,
    "Message" text,
    "ResultData" jsonb,
    "ExecutionTimeMs" bigint,
    "CreatedAt" timestamp with time zone
);
CREATE INDEX "IX_DemoExecutions_DemoType" ON "DemoExecutions"("DemoType");
CREATE INDEX "IX_DemoExecutions_CreatedAt" ON "DemoExecutions"("CreatedAt");
```

#### DemoTestData
```sql
CREATE TABLE "DemoTestData" (
    "Id" uuid PRIMARY KEY,
    "DemoType" text NOT NULL,
    "FileName" text,
    "FilePath" text,
    "FileSizeBytes" bigint,
    "ContentHash" text,
    "CreatedAt" timestamp with time zone,
    "UpdatedAt" timestamp with time zone
);
CREATE INDEX "IX_DemoTestData_DemoType" ON "DemoTestData"("DemoType");
CREATE INDEX "IX_DemoTestData_ContentHash" ON "DemoTestData"("ContentHash");
```

**Advantages:**
- ? Centralized storage
- ? Full query capabilities
- ? History tracking
- ? Aggregation & analytics
- ? Better for production

**Disadvantages:**
- ? Requires PostgreSQL
- ? Slightly slower than files
- ? Database overhead

### Hybrid Approach (Recommended)

Use **both** simultaneously for maximum flexibility:

1. **Local files** for fast access during development
2. **PostgreSQL** for persistent history and analytics

**Implementation:**
- Create a composite repository that uses both
- Local files serve as cache/backup
- Database provides audit trail

## Usage

### Automatic Injection

```csharp
public class MyDemoService
{
    private readonly ITestDataRepository _repository;

    public MyDemoService(ITestDataRepository repository)
    {
        _repository = repository;
    }

    public async Task RunDemo()
    {
        // Get test data (from configured source)
        var data = await _repository.GetTestDataAsync("classification");
        
        // Do processing...
        var result = ProcessData(data);
        
        // Save result (if configured)
        await _repository.SaveDemoResultAsync("classification", result);
    }
}
```

### Query Results

**From LocalFileRepository:**
```csharp
var results = await _repository.GetDemoResultsAsync("classification", count: 10);
foreach (var result in results)
{
    Console.WriteLine($"{result.DemoType}: {result.Message} ({result.ExecutionTimeMs})");
}
```

**From PostgresRepository:**
```sql
-- Query demo execution history
SELECT 
    "DemoType",
    COUNT(*) as "ExecutionCount",
    AVG("ExecutionTimeMs") as "AvgExecutionTime",
    COUNT(CASE WHEN "Success" THEN 1 END) as "SuccessCount"
FROM "DemoExecutions"
WHERE "DemoType" = 'classification'
  AND "CreatedAt" > NOW() - INTERVAL '7 days'
GROUP BY "DemoType"
ORDER BY "ExecutionCount" DESC;
```

## Migration from Files to Database

### Step 1: Enable PostgreSQL
```json
{
  "DemoSettings": {
    "DataSource": "postgres"
  }
}
```

### Step 2: Run Migration
```bash
dotnet ef database update
```

### Step 3: Keep Local Files
- Existing local files remain as backup
- New results stored in database
- Can query both if needed

## Best Practices

### For Development
```json
{
  "DemoSettings": {
    "DataSource": "local",
    "SaveResults": false
  }
}
```
- Fast iteration
- No database overhead
- Good for testing

### For Production
```json
{
  "DemoSettings": {
    "DataSource": "postgres",
    "SaveResults": true,
    "ResultRetentionDays": 90
  }
}
```
- Persistent storage
- Full audit trail
- Analytics ready

### For CI/CD
```json
{
  "DemoSettings": {
    "DataSource": "local",
    "SaveResults": false
  }
}
```
- No external dependencies
- Reproducible tests
- Quick feedback loops

## Monitoring & Maintenance

### Database Cleanup

```sql
-- Remove old demo results (keep last 30 days)
DELETE FROM "DemoExecutions"
WHERE "CreatedAt" < NOW() - INTERVAL '30 days';

-- Archive results to separate table (optional)
INSERT INTO "DemoExecutions_Archive"
SELECT * FROM "DemoExecutions"
WHERE "CreatedAt" < NOW() - INTERVAL '90 days';
```

### Performance Monitoring

```sql
-- Slowest demos
SELECT 
    "DemoType",
    "ExecutionTimeMs",
    "CreatedAt"
FROM "DemoExecutions"
WHERE "ExecutionTimeMs" > 5000
ORDER BY "ExecutionTimeMs" DESC
LIMIT 10;

-- Success rate
SELECT 
    "DemoType",
    COUNT(CASE WHEN "Success" THEN 1 END) * 100.0 / COUNT(*) as "SuccessRate"
FROM "DemoExecutions"
GROUP BY "DemoType";
```

## Future Enhancements

- [ ] Azure Blob Storage repository
- [ ] Elasticsearch for result indexing
- [ ] GraphQL API for result queries
- [ ] Automatic result cleanup service
- [ ] Result caching layer
- [ ] Multi-tenancy support

## Testing

### Unit Tests Example

```csharp
[Fact]
public async Task LocalFileRepository_GetTestData_ReturnsCorrectContent()
{
    // Arrange
    var logger = new Mock<ILogger<LocalFileRepository>>();
    var config = new Mock<IConfiguration>();
    config.Setup(c => c["DemoSettings:LocalDataPath"]).Returns("demos/");
    
    var repository = new LocalFileRepository(logger.Object, config.Object);
    
    // Act
    var data = await repository.GetTestDataAsync("classification");
    
    // Assert
    Assert.NotEmpty(data);
    Assert.Contains("positive", data);
}
```

## Support & Troubleshooting

### Issue: Data not found
```
[LocalFileRepository] Test data not found for classification at demos/classification/data/...
```

**Solution:**
1. Run demo to generate data: `POST /api/demo/generate-testdata?demoType=classification`
2. Check file exists: `ls demos/classification/data/`
3. Verify DemoSettings.LocalDataPath in appsettings.json

### Issue: PostgreSQL connection failed
```
[PostgresRepository] Error retrieving test data for classification
```

**Solution:**
1. Check PostgreSQL is running
2. Verify connection string in appsettings.json
3. Run migrations: `dotnet ef database update`
4. Check database permissions

### Issue: Results not being saved
**Solution:**
- Check `DemoSettings.SaveResults` is `true`
- Verify repository implementation in DI container
- Check logs for exceptions

## Resources

- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [Repository Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
