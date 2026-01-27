-- Quick Demo Data Check
-- Shows if tables have data and displays samples

-- ============================================================
-- 1. TABLE SIZES & ROW COUNTS
-- ============================================================
SELECT 
    'DemoExecutions' as "Table",
    COUNT(*) as "Row Count",
    CASE 
        WHEN COUNT(*) = 0 THEN 'EMPTY'
        ELSE 'HAS DATA'
    END as "Status"
FROM "DemoExecutions"

UNION ALL

SELECT 
    'DemoTestData' as "Table",
    COUNT(*) as "Row Count",
    CASE 
        WHEN COUNT(*) = 0 THEN 'EMPTY'
        ELSE 'HAS DATA'
    END as "Status"
FROM "DemoTestData";

-- ============================================================
-- 2. SAMPLE DATA FROM DemoExecutions (LIMIT 5)
-- ============================================================
SELECT 
    'DEMO EXECUTIONS SAMPLE' as "INFO",
    "DemoType",
    "Success",
    "Message",
    "ExecutionTimeMs",
    "CreatedAt"::text as "Date"
FROM "DemoExecutions"
ORDER BY "CreatedAt" DESC
LIMIT 5;

-- ============================================================
-- 3. SAMPLE DATA FROM DemoTestData (LIMIT 5)
-- ============================================================
SELECT 
    'DEMO TEST DATA SAMPLE' as "INFO",
    "DemoType",
    "FileName",
    "FileSizeBytes",
    "CreatedAt"::text as "Date"
FROM "DemoTestData"
ORDER BY "CreatedAt" DESC
LIMIT 5;

-- ============================================================
-- 4. QUICK STATS
-- ============================================================
SELECT 
    'EXECUTIONS STATS' as "Metric",
    COUNT(*) as "Value"
FROM "DemoExecutions"

UNION ALL

SELECT 
    'SUCCESS RATE %' as "Metric",
    ROUND((SUM(CASE WHEN "Success" THEN 1 ELSE 0 END)::numeric / COUNT(*) * 100), 1)::text as "Value"
FROM "DemoExecutions"
WHERE COUNT(*) > 0

UNION ALL

SELECT 
    'FAILED COUNT' as "Metric",
    COUNT(*) as "Value"
FROM "DemoExecutions"
WHERE NOT "Success";
