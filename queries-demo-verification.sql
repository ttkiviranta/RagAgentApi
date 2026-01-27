-- PostgreSQL Demo Data Verification Queries
-- Run these queries in pgAdmin or psql to verify demo data seeding

-- ============================================================
-- 1. Check if demo tables exist and have data
-- ============================================================

-- Count demo executions by type
SELECT 
    "DemoType",
    COUNT(*) as "ExecutionCount",
    COUNT(CASE WHEN "Success" THEN 1 END) as "SuccessCount",
    COUNT(CASE WHEN NOT "Success" THEN 1 END) as "FailureCount",
    MIN("CreatedAt") as "FirstRun",
    MAX("CreatedAt") as "LastRun"
FROM "DemoExecutions"
GROUP BY "DemoType"
ORDER BY "LastRun" DESC;

-- ============================================================
-- 2. View all recent demo executions (last 24 hours)
-- ============================================================

SELECT 
    "Id",
    "DemoType",
    "Success",
    "Message",
    "ExecutionTimeMs",
    "CreatedAt"
FROM "DemoExecutions"
WHERE "CreatedAt" > NOW() - INTERVAL '24 hours'
ORDER BY "CreatedAt" DESC
LIMIT 20;

-- ============================================================
-- 3. Performance analysis - average execution times
-- ============================================================

SELECT 
    "DemoType",
    COUNT(*) as "RunCount",
    AVG("ExecutionTimeMs")::numeric(10,2) as "AvgExecutionMs",
    MIN("ExecutionTimeMs") as "FastestMs",
    MAX("ExecutionTimeMs") as "SlowestMs",
    STDDEV("ExecutionTimeMs")::numeric(10,2) as "StdDeviation"
FROM "DemoExecutions"
GROUP BY "DemoType"
ORDER BY "AvgExecutionMs" DESC;

-- ============================================================
-- 4. Success rate analysis
-- ============================================================

SELECT 
    "DemoType",
    COUNT(*) as "TotalRuns",
    COUNT(CASE WHEN "Success" THEN 1 END) as "SuccessfulRuns",
    ROUND(
        COUNT(CASE WHEN "Success" THEN 1 END) * 100.0 / COUNT(*),
        2
    ) as "SuccessRate_%"
FROM "DemoExecutions"
GROUP BY "DemoType"
ORDER BY "SuccessRate_%" DESC;

-- ============================================================
-- 5. View demo result data (JSON)
-- ============================================================

-- View results for a specific demo type
SELECT 
    "DemoType",
    "ExecutionTimeMs",
    "ResultData",
    "CreatedAt"
FROM "DemoExecutions"
WHERE "DemoType" = 'classification'  -- Change to desired demo type
ORDER BY "CreatedAt" DESC
LIMIT 5;

-- ============================================================
-- 6. Check demo test data metadata
-- ============================================================

SELECT 
    "DemoType",
    "FileName",
    "FileSizeBytes",
    "ContentHash",
    "CreatedAt",
    "UpdatedAt"
FROM "DemoTestData"
ORDER BY "DemoType", "UpdatedAt" DESC;

-- ============================================================
-- 7. Timeline analysis - executions per hour
-- ============================================================

SELECT 
    "DemoType",
    DATE_TRUNC('hour', "CreatedAt") as "Hour",
    COUNT(*) as "ExecutionCount",
    COUNT(CASE WHEN "Success" THEN 1 END) as "Successes"
FROM "DemoExecutions"
WHERE "CreatedAt" > NOW() - INTERVAL '24 hours'
GROUP BY "DemoType", DATE_TRUNC('hour', "CreatedAt")
ORDER BY "Hour" DESC, "DemoType";

-- ============================================================
-- 8. Extract JSON data from results (example)
-- ============================================================

-- Example: Extract classification accuracy
SELECT 
    "DemoType",
    "ExecutionTimeMs",
    ("ResultData"->>'model_accuracy') as "ModelAccuracy",
    ("ResultData"->>'total_samples') as "TotalSamples",
    "CreatedAt"
FROM "DemoExecutions"
WHERE "DemoType" = 'classification'
  AND "ResultData" IS NOT NULL
ORDER BY "CreatedAt" DESC
LIMIT 10;

-- ============================================================
-- 9. Database statistics
-- ============================================================

-- Check table sizes
SELECT 
    'DemoExecutions' as "TableName",
    COUNT(*) as "RowCount",
    PG_SIZE_PRETTY(PG_TOTAL_RELATION_SIZE('"DemoExecutions"'::regclass)) as "TotalSize"
FROM "DemoExecutions"

UNION ALL

SELECT 
    'DemoTestData' as "TableName",
    COUNT(*) as "RowCount",
    PG_SIZE_PRETTY(PG_TOTAL_RELATION_SIZE('"DemoTestData"'::regclass)) as "TotalSize"
FROM "DemoTestData";

-- ============================================================
-- 10. Cleanup - Delete old demo data (optional)
-- ============================================================

-- Delete demo executions older than 30 days
-- DELETE FROM "DemoExecutions"
-- WHERE "CreatedAt" < NOW() - INTERVAL '30 days';

-- Delete all demo executions
-- DELETE FROM "DemoExecutions";

-- Reset demo executions (CAUTION!)
-- TRUNCATE TABLE "DemoExecutions" CASCADE;
