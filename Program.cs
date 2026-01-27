using RagAgentApi.Agents;
using RagAgentApi.Services;
using RagAgentApi.Services.DemoServices;
using RagAgentApi.Filters;
using RagAgentApi.Data;
using RagAgentApi.Hubs;
using System.Reflection;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add configuration sources
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });
builder.Services.AddEndpointsApiExplorer();

// Add SignalR
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// PostgreSQL DbContext
builder.Services.AddDbContext<RagDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL"),
        o => o.UseVector()
    ));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RAG Agent API",
        Version = "v1",
        Description = "Multi-agent RAG system using Azure AI services for document ingestion and intelligent querying",
        Contact = new OpenApiContact
        {
            Name = "RAG Agent API",
            Email = "support@ragagentapi.com"
        }
    });

    // Include XML comments for better API documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add security definition for future authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

// Add HTTP client for web scraping
builder.Services.AddHttpClient();

// Azure Services - Singleton for connection reuse
builder.Services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
builder.Services.AddSingleton<IAzureSearchService, AzureSearchService>();

// PostgreSQL Services
builder.Services.AddScoped<PostgresQueryService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<AgentSelectorService>();
builder.Services.AddScoped<AgentFactory>();
builder.Services.AddScoped<DatabaseSeedService>();

// Agents - Scoped for request lifecycle
builder.Services.AddScoped<OrchestratorAgent>();
builder.Services.AddScoped<ScraperAgent>();
builder.Services.AddScoped<ChunkerAgent>();
builder.Services.AddScoped<EmbeddingAgent>();
builder.Services.AddScoped<StorageAgent>(); // Original Azure-based
builder.Services.AddScoped<PostgresStorageAgent>(); // New PostgreSQL-based
builder.Services.AddScoped<QueryAgent>(); // Original Azure-based
builder.Services.AddScoped<PostgresQueryAgent>(); // New PostgreSQL-based

// Specialized Agents (placeholders)
builder.Services.AddScoped<GitHubApiAgent>();
builder.Services.AddScoped<YouTubeTranscriptAgent>();
builder.Services.AddScoped<ArxivScraperAgent>();
builder.Services.AddScoped<NewsArticleScraperAgent>();

// Demo Services - Scoped for demo functionality
builder.Services.AddScoped<ClassificationDemoService>();
builder.Services.AddScoped<TimeSeriesDemoService>();
builder.Services.AddScoped<ImageProcessingDemoService>();
builder.Services.AddScoped<AudioProcessingDemoService>();

// Demo Data Repository - Configurable data source
var demoDataSource = builder.Configuration.GetValue<string>("DemoSettings:DataSource", "local").ToLower();
if (demoDataSource == "postgres")
{
    builder.Services.AddScoped<ITestDataRepository, PostgresRepository>();
}
else
{
    builder.Services.AddScoped<ITestDataRepository, LocalFileRepository>();
}

// Orchestration - Singleton for thread state management
builder.Services.AddSingleton<AgentOrchestrationService>();

// Telemetry Filter - Scoped (only if Application Insights is configured)
var appInsightsConnectionString = builder.Configuration.GetSection("ApplicationInsights:ConnectionString").Value;
if (!string.IsNullOrEmpty(appInsightsConnectionString) && appInsightsConnectionString != "USER_WILL_PROVIDE")
{
    builder.Services.AddScoped<AgentTelemetryFilter>();
}

// Background service for context cleanup
builder.Services.AddHostedService<ContextCleanupService>();

// Application Insights telemetry
if (!string.IsNullOrEmpty(appInsightsConnectionString) && appInsightsConnectionString != "USER_WILL_PROVIDE")
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

// Logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!string.IsNullOrEmpty(appInsightsConnectionString) && appInsightsConnectionString != "USER_WILL_PROVIDE")
{
    builder.Logging.AddApplicationInsights(
        configureTelemetryConfiguration: (config) =>
        {
            config.ConnectionString = appInsightsConnectionString;
        },
        configureApplicationInsightsLoggerOptions: (options) => { });
}

// Set minimum log level
builder.Logging.SetMinimumLevel(LogLevel.Information);

// CORS configuration for Blazor UI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorUI", policy =>
    {
        policy.WithOrigins(
                  "https://localhost:7170",  // Blazor UI
                  "http://localhost:5173"    // Vue UI
               ) 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });

    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG Agent API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger at root
        options.DocumentTitle = "RAG Agent API";
        options.DisplayRequestDuration();
    });
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazorUI");

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub"); // SignalR hub mapping

// Add health check endpoint
app.MapHealthChecks("/health");

// Startup background task to initialize search index
_ = Task.Run(async () =>
{
    try
    {
        var scope = app.Services.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<IAzureSearchService>();
        var seedService = scope.ServiceProvider.GetRequiredService<DatabaseSeedService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Initializing Azure Search index on startup...");
        await searchService.CreateOrUpdateIndexAsync();
        logger.LogInformation("Azure Search index initialized successfully");

        logger.LogInformation("Seeding agent types and URL mappings...");
        await seedService.SeedAgentTypesAsync();
        logger.LogInformation("Agent types seeding completed successfully");
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to initialize services on startup - will retry on first use");
    }
});

// Log application startup
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("RAG Agent API starting up...");
startupLogger.LogInformation("Swagger UI available at: {SwaggerUrl}",
    app.Environment.IsDevelopment() ? "https://localhost:7000" : "");

app.Run();

/// <summary>
/// Background service to periodically clean up old agent contexts
/// </summary>
public class ContextCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContextCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _maxContextAge = TimeSpan.FromDays(1);

    public ContextCleanupService(IServiceScopeFactory scopeFactory, ILogger<ContextCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrationService = scope.ServiceProvider.GetRequiredService<AgentOrchestrationService>();

                var cleanedCount = orchestrationService.CleanupOldContexts(_maxContextAge);

                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old agent contexts", cleanedCount);
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during context cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Retry after 5 minutes
            }
        }
    }
}