-- Demo Tables Content Listing Script
-- List all demo executions and test data from PostgreSQL
-- Run this in pgAdmin or psql to see demo data

-- ============================================================
-- 1. DEMO EXECUTIONS - All Results
-- ============================================================
SELECT 
    '[DEMO EXECUTIONS]' as "Section",
    "DemoType",
    "Success",
    "Message",
    "ExecutionTimeMs",
    "CreatedAt"
FROM "DemoExecutions"
ORDER BY "CreatedAt" DESC;

-- ============================================================
-- 2. DEMO TEST DATA - Metadata
-- ============================================================
SELECT 
    '[DEMO TEST DATA]' as "Section",
    "DemoType",
    "FileName",
    "FileSizeBytes",
    "CreatedAt",
    "UpdatedAt"
FROM "DemoTestData"
ORDER BY "UpdatedAt" DESC;

-- ============================================================
-- 3. Summary Statistics
-- ============================================================
SELECT 
    '[SUMMARY]' as "Section",
    'DemoExecutions' as "Table",
    COUNT(*) as "Total Records",
    COUNT(CASE WHEN "Success" THEN 1 END) as "Successful",
    COUNT(CASE WHEN NOT "Success" THEN 1 END) as "Failed",
    MIN("CreatedAt") as "Oldest",
    MAX("CreatedAt") as "Newest"
FROM "DemoExecutions"

UNION ALL

SELECT 
    '[SUMMARY]' as "Section",
    'DemoTestData' as "Table",
    COUNT(*) as "Total Records",
    COUNT(*) as "Successful",
    0 as "Failed",
    MIN("CreatedAt") as "Oldest",
    MAX("UpdatedAt") as "Newest"
FROM "DemoTestData";

-- ============================================================
-- 4. Demo Type Statistics
-- ============================================================
SELECT 
    '[BY DEMO TYPE]' as "Section",
    "DemoType",
    COUNT(*) as "Runs",
    SUM(CASE WHEN "Success" THEN 1 ELSE 0 END) as "Success Count",
    ROUND(AVG("ExecutionTimeMs")::numeric, 2) as "Avg Time (ms)",
    MIN("ExecutionTimeMs") as "Min Time",
    MAX("ExecutionTimeMs") as "Max Time"
FROM "DemoExecutions"
GROUP BY "DemoType"
ORDER BY "DemoType";

-- ============================================================
-- 5. Recent Executions (Last 10)
-- ============================================================
SELECT 
    '[RECENT EXECUTIONS]' as "Section",
    "DemoType",
    "Success",
    "Message",
    "ExecutionTimeMs",
    "CreatedAt"
FROM "DemoExecutions"
ORDER BY "CreatedAt" DESC
LIMIT 10;

-- ============================================================
-- 6. Success Rate by Demo Type
-- ============================================================
SELECT 
    '[SUCCESS RATE]' as "Section",
    "DemoType",
    COUNT(*) as "Total Runs",
    SUM(CASE WHEN "Success" THEN 1 ELSE 0 END) as "Successful Runs",
    ROUND(
        (SUM(CASE WHEN "Success" THEN 1 ELSE 0 END)::numeric * 100.0 / COUNT(*)),
        2
    ) as "Success Rate %"
FROM "DemoExecutions"
GROUP BY "DemoType"
ORDER BY "Success Rate %" DESC;

-- ============================================================
-- 7. Execution Timeline (Hourly)
-- ============================================================
SELECT 
    '[TIMELINE]' as "Section",
    DATE_TRUNC('hour', "CreatedAt") as "Hour",
    COUNT(*) as "Total Runs",
    SUM(CASE WHEN "Success" THEN 1 ELSE 0 END) as "Successful"
FROM "DemoExecutions"
WHERE "CreatedAt" > NOW() - INTERVAL '24 hours'
GROUP BY DATE_TRUNC('hour', "CreatedAt")
ORDER BY "Hour" DESC;

-- ============================================================
-- 8. JSON Results Sample (First Classification Result)
-- ============================================================
SELECT 
    '[JSON RESULTS SAMPLE - Classification]' as "Section",
    "DemoType",
    "ExecutionTimeMs",
    "ResultData"
FROM "DemoExecutions"
WHERE "DemoType" = 'classification'
ORDER BY "CreatedAt" DESC
LIMIT 1;

-- ============================================================
-- 9. All Failures (if any)
-- ============================================================
SELECT 
    '[FAILURES]' as "Section",
    "DemoType",
    "Message",
    "CreatedAt"
FROM "DemoExecutions"
WHERE NOT "Success"
ORDER BY "CreatedAt" DESC;

-- ============================================================
-- 10. Database Storage Usage
-- ============================================================
SELECT 
    '[STORAGE]' as "Section",
    'DemoExecutions' as "Table",
    PG_SIZE_PRETTY(PG_TOTAL_RELATION_SIZE('"DemoExecutions"'::regclass)) as "Size"

UNION ALL

SELECT 
    '[STORAGE]' as "Section",
    'DemoTestData' as "Table",
    PG_SIZE_PRETTY(PG_TOTAL_RELATION_SIZE('"DemoTestData"'::regclass)) as "Size";
