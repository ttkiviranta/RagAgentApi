using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Data;
using Microsoft.EntityFrameworkCore;

namespace RagAgentApi.Controllers;

/// <summary>
/// Database test controller for verifying PostgreSQL connection
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DatabaseTestController : ControllerBase
{
    private readonly RagDbContext _context;
    private readonly ILogger<DatabaseTestController> _logger;

    public DatabaseTestController(RagDbContext context, ILogger<DatabaseTestController> logger)
    {
     _context = context;
        _logger = logger;
  }

    /// <summary>
    /// Test PostgreSQL connection and pgvector extension
    /// </summary>
    [HttpGet("connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            _logger.LogInformation("Testing PostgreSQL connection...");

            // Test basic connection
         var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
          {
         return StatusCode(500, new { error = "Cannot connect to PostgreSQL database" });
   }

    // Get database info
 var connectionString = _context.Database.GetConnectionString();
            var databaseName = _context.Database.GetDbConnection().Database;

// Test pgvector extension
    var vectorExtension = await _context.Database
   .SqlQueryRaw<string>("SELECT extname FROM pg_extension WHERE extname = 'vector'")
    .FirstOrDefaultAsync();

      // Count tables
       var tableCount = await _context.Database
      .SqlQueryRaw<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'")
     .FirstOrDefaultAsync();

            // Test tables exist
      var documentsExists = await _context.Database
           .SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'Documents')")
    .FirstOrDefaultAsync();

          var chunksExists = await _context.Database
.SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'DocumentChunks')")
     .FirstOrDefaultAsync();

          var result = new
{
           status = "connected",
 database = databaseName,
    pgvector_enabled = vectorExtension == "vector",
  table_count = tableCount,
       tables = new
      {
      documents = documentsExists,
        document_chunks = chunksExists,
    conversations = await _context.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'Conversations')").FirstOrDefaultAsync(),
       messages = await _context.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'Messages')").FirstOrDefaultAsync(),
          agent_executions = await _context.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'AgentExecutions')").FirstOrDefaultAsync(),
           agent_types = await _context.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'AgentTypes')").FirstOrDefaultAsync(),
            url_agent_mappings = await _context.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'UrlAgentMappings')").FirstOrDefaultAsync()
     },
  timestamp = DateTimeOffset.UtcNow
       };

   _logger.LogInformation("PostgreSQL connection test successful");
            return Ok(result);
  }
        catch (Exception ex)
   {
            _logger.LogError(ex, "PostgreSQL connection test failed");
  return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test vector operations with pgvector
    /// </summary>
    [HttpGet("vector-test")]
    public async Task<IActionResult> TestVectorOperations()
    {
        try
        {
      _logger.LogInformation("Testing pgvector operations...");

            // Simplest possible vector test
 using var command = _context.Database.GetDbConnection().CreateCommand();
       command.CommandText = "SELECT '[1,2,3]'::vector <-> '[1,2,4]'::vector";
      
            if (command.Connection?.State != System.Data.ConnectionState.Open)
  {
        await _context.Database.OpenConnectionAsync();
            }
   
            var result_value = await command.ExecuteScalarAsync();
    var distance = Convert.ToDouble(result_value);

       var result = new
    {
     status = "vector_operations_working",
       test_distance = distance,
          note = "Distance between [1,2,3] and [1,2,4]",
         explanation = "Lower distance means more similar vectors",
    timestamp = DateTimeOffset.UtcNow
       };

       _logger.LogInformation("pgvector operations test successful");
       return Ok(result);
        }
        catch (Exception ex)
  {
   _logger.LogError(ex, "pgvector operations test failed");
      return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
 }

    // Helper class for vector test result
    public class VectorTestResult
    {
        public double distance { get; set; }
    }

/// <summary>
    /// Get database statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetDatabaseStats()
    {
        try
        {
   var stats = new
  {
           documents = await _context.Documents.CountAsync(),
           document_chunks = await _context.DocumentChunks.CountAsync(),
 conversations = await _context.Conversations.CountAsync(),
         messages = await _context.Messages.CountAsync(),
           agent_executions = await _context.AgentExecutions.CountAsync(),
    agent_types = await _context.AgentTypes.CountAsync(),
                url_agent_mappings = await _context.UrlAgentMappings.CountAsync(),
           timestamp = DateTimeOffset.UtcNow
       };

      return Ok(stats);
        }
        catch (Exception ex)
        {
       _logger.LogError(ex, "Failed to get database statistics");
  return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }
}