using Microsoft.EntityFrameworkCore;
using RagAgentApi.Data;
using RagAgentApi.Models;
using RagAgentApi.Models.PostgreSQL;

namespace RagAgentApi.Tests;

/// <summary>
/// Test-specific DbContext that ignores JsonDocument and Vector properties
/// which are not supported by the InMemory provider
/// </summary>
public class TestDbContext : RagDbContext
{
    public TestDbContext(DbContextOptions<RagDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Skip base OnModelCreating to avoid PostgreSQL-specific configurations
        // (pgvector extension, jsonb column types, ivfflat indexes)

        // Configure Document entity - ignore JsonDocument properties
        modelBuilder.Entity<Document>(entity =>
        {
            entity.Ignore(e => e.Metadata);
        });

        // Configure DocumentChunk entity - ignore Vector and JsonDocument properties
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.Ignore(e => e.Embedding);
            entity.Ignore(e => e.ChunkMetadata);
            
            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Chunks)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Conversation entity - ignore JsonDocument properties
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.Ignore(e => e.Metadata);
        });

        // Configure Message entity - ignore Vector and JsonDocument properties
        modelBuilder.Entity<Message>(entity =>
        {
            entity.Ignore(e => e.QueryEmbedding);
            entity.Ignore(e => e.Sources);
            entity.Ignore(e => e.Metadata);

            entity.HasOne(e => e.Conversation)
                  .WithMany(c => c.Messages)
                  .HasForeignKey(e => e.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AgentExecution entity - ignore JsonDocument properties
        modelBuilder.Entity<AgentExecution>(entity =>
        {
            entity.Ignore(e => e.InputData);
            entity.Ignore(e => e.OutputData);
            entity.Ignore(e => e.Metrics);

            entity.HasOne(e => e.ParentExecution)
                  .WithMany(e => e.ChildExecutions)
                  .HasForeignKey(e => e.ParentExecutionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure AgentType entity - ignore JsonDocument properties
        modelBuilder.Entity<AgentType>(entity =>
        {
            entity.Ignore(e => e.Capabilities);
            entity.Ignore(e => e.ScraperConfig);
            entity.Ignore(e => e.ChunkerConfig);
            entity.Ignore(e => e.AgentPipeline);
        });

        // Configure UrlAgentMapping entity
        modelBuilder.Entity<UrlAgentMapping>(entity =>
        {
            entity.HasOne(e => e.AgentType)
                  .WithMany(at => at.UrlMappings)
                  .HasForeignKey(e => e.AgentTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure DemoExecution entity (no JsonDocument properties to ignore)
        modelBuilder.Entity<DemoExecution>();

        // Configure DemoTestData entity (no JsonDocument properties to ignore)
        modelBuilder.Entity<DemoTestData>();

        // Configure ErrorLog entity (no JsonDocument properties to ignore)
        modelBuilder.Entity<ErrorLog>();
    }
}
