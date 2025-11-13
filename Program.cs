using RagAgentApi.Agents;
using RagAgentApi.Services;
using RagAgentApi.Filters;
using System.Reflection;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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

// Agents - Scoped for request lifecycle
builder.Services.AddScoped<OrchestratorAgent>();
builder.Services.AddScoped<ScraperAgent>();
builder.Services.AddScoped<ChunkerAgent>();
builder.Services.AddScoped<EmbeddingAgent>();
builder.Services.AddScoped<StorageAgent>();
builder.Services.AddScoped<QueryAgent>();

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

// CORS configuration
builder.Services.AddCors(options =>
{
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

app.UseCors();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Startup background task to initialize search index
_ = Task.Run(async () =>
{
    try
    {
        var scope = app.Services.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<IAzureSearchService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
      
        logger.LogInformation("Initializing Azure Search index on startup...");
        await searchService.CreateOrUpdateIndexAsync();
        logger.LogInformation("Azure Search index initialized successfully");
    }
    catch (Exception ex)
 {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
     logger.LogWarning(ex, "Failed to initialize Azure Search index on startup - will retry on first use");
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