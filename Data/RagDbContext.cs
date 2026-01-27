using Microsoft.EntityFrameworkCore;
using RagAgentApi.Models.PostgreSQL;
using Pgvector.EntityFrameworkCore;

namespace RagAgentApi.Data;

/// <summary>
/// Database context for PostgreSQL with pgvector support
/// </summary>
public class RagDbContext : DbContext
{
    public RagDbContext(DbContextOptions<RagDbContext> options) : base(options)
    {
    }

    // DbSets
  public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentChunk> DocumentChunks { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<AgentExecution> AgentExecutions { get; set; }
    public DbSet<AgentType> AgentTypes { get; set; }
    public DbSet<UrlAgentMapping> UrlAgentMappings { get; set; }
    
    // Demo Services
    public DbSet<DemoExecution> DemoExecutions { get; set; }
    public DbSet<DemoTestData> DemoTestData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
  modelBuilder.HasPostgresExtension("vector");

        // Configure Document entity
        modelBuilder.Entity<Document>(entity =>
        {
       entity.HasIndex(e => e.UrlHash)
       .IsUnique()
      .HasDatabaseName("IX_Documents_UrlHash");

 entity.HasIndex(e => e.Url)
          .HasDatabaseName("IX_Documents_Url");

       entity.HasIndex(e => e.Status)
      .HasDatabaseName("IX_Documents_Status");

      entity.HasIndex(e => e.ScrapedAt)
          .HasDatabaseName("IX_Documents_ScrapedAt");

   entity.Property(e => e.Metadata)
    .HasColumnType("jsonb");
        });

        // Configure DocumentChunk entity
        modelBuilder.Entity<DocumentChunk>(entity =>
      {
         entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex })
  .IsUnique()
              .HasDatabaseName("IX_DocumentChunks_DocumentId_ChunkIndex");

   // Vector index for cosine similarity
    entity.HasIndex(e => e.Embedding)
      .HasMethod("ivfflat")
       .HasOperators("vector_cosine_ops")
            .HasDatabaseName("IX_DocumentChunks_Embedding_Cosine");

    entity.Property(e => e.Embedding)
    .HasColumnType("vector(1536)");

        entity.Property(e => e.ChunkMetadata)
         .HasColumnType("jsonb");

    // Foreign key relationship
         entity.HasOne(e => e.Document)
      .WithMany(d => d.Chunks)
                  .HasForeignKey(e => e.DocumentId)
       .OnDelete(DeleteBehavior.Cascade);
 });

        // Configure Conversation entity
 modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_Conversations_UserId");

        entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_Conversations_Status");

            entity.HasIndex(e => e.CreatedAt)
         .HasDatabaseName("IX_Conversations_CreatedAt");

            entity.Property(e => e.Metadata)
      .HasColumnType("jsonb");
   });

        // Configure Message entity
        modelBuilder.Entity<Message>(entity =>
        {
      entity.HasIndex(e => e.ConversationId)
     .HasDatabaseName("IX_Messages_ConversationId");

       entity.HasIndex(e => e.Role)
   .HasDatabaseName("IX_Messages_Role");

   entity.HasIndex(e => e.CreatedAt)
    .HasDatabaseName("IX_Messages_CreatedAt");

      // Vector index for query embeddings
       entity.HasIndex(e => e.QueryEmbedding)
   .HasMethod("ivfflat")
          .HasOperators("vector_cosine_ops")
       .HasDatabaseName("IX_Messages_QueryEmbedding_Cosine");

      entity.Property(e => e.QueryEmbedding)
     .HasColumnType("vector(1536)");

        entity.Property(e => e.Sources)
       .HasColumnType("jsonb");

       entity.Property(e => e.Metadata)
       .HasColumnType("jsonb");

            // Foreign key relationship
            entity.HasOne(e => e.Conversation)
      .WithMany(c => c.Messages)
     .HasForeignKey(e => e.ConversationId)
       .OnDelete(DeleteBehavior.Cascade);
    });

        // Configure AgentExecution entity
        modelBuilder.Entity<AgentExecution>(entity =>
      {
  entity.HasIndex(e => e.ThreadId)
         .HasDatabaseName("IX_AgentExecutions_ThreadId");

         entity.HasIndex(e => e.AgentName)
     .HasDatabaseName("IX_AgentExecutions_AgentName");

    entity.HasIndex(e => e.Status)
    .HasDatabaseName("IX_AgentExecutions_Status");

          entity.HasIndex(e => e.StartedAt)
    .HasDatabaseName("IX_AgentExecutions_StartedAt");

            entity.Property(e => e.InputData)
       .HasColumnType("jsonb");

  entity.Property(e => e.OutputData)
 .HasColumnType("jsonb");

    entity.Property(e => e.Metrics)
       .HasColumnType("jsonb");

         // Self-referencing relationship
  entity.HasOne(e => e.ParentExecution)
     .WithMany(e => e.ChildExecutions)
              .HasForeignKey(e => e.ParentExecutionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure AgentType entity
 modelBuilder.Entity<AgentType>(entity =>
        {
   entity.HasIndex(e => e.Name)
   .IsUnique()
  .HasDatabaseName("IX_AgentTypes_Name");

 entity.HasIndex(e => e.IsActive)
       .HasDatabaseName("IX_AgentTypes_IsActive");

       entity.Property(e => e.Capabilities)
          .HasColumnType("jsonb");

   entity.Property(e => e.ScraperConfig)
          .HasColumnType("jsonb");

  entity.Property(e => e.ChunkerConfig)
 .HasColumnType("jsonb");

            entity.Property(e => e.AgentPipeline)
                  .HasColumnType("jsonb");
        });

   // Configure UrlAgentMapping entity
        modelBuilder.Entity<UrlAgentMapping>(entity =>
  {
         entity.HasIndex(e => e.Pattern)
.HasDatabaseName("IX_UrlAgentMappings_Pattern");

         entity.HasIndex(e => e.Priority)
          .HasDatabaseName("IX_UrlAgentMappings_Priority");

    entity.HasIndex(e => e.IsActive)
          .HasDatabaseName("IX_UrlAgentMappings_IsActive");

     entity.HasIndex(e => new { e.Pattern, e.Priority })
       .HasDatabaseName("IX_UrlAgentMappings_Pattern_Priority");

 // Foreign key relationship
   entity.HasOne(e => e.AgentType)
      .WithMany(at => at.UrlMappings)
      .HasForeignKey(e => e.AgentTypeId)
         .OnDelete(DeleteBehavior.Cascade);
      });

        // Configure DemoExecution entity
        modelBuilder.Entity<DemoExecution>(entity =>
        {
            entity.HasIndex(e => e.DemoType)
                .HasDatabaseName("IX_DemoExecutions_DemoType");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_DemoExecutions_CreatedAt");

            entity.HasIndex(e => new { e.DemoType, e.CreatedAt })
                .HasDatabaseName("IX_DemoExecutions_DemoType_CreatedAt");

            entity.Property(e => e.ResultData)
                .HasColumnType("jsonb");
        });

        // Configure DemoTestData entity
        modelBuilder.Entity<DemoTestData>(entity =>
        {
            entity.HasIndex(e => e.DemoType)
                .HasDatabaseName("IX_DemoTestData_DemoType");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_DemoTestData_CreatedAt");

            entity.HasIndex(e => e.ContentHash)
                .HasDatabaseName("IX_DemoTestData_ContentHash");
        });
  }
}