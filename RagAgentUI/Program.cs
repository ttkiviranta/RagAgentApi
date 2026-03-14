using RagAgentUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Application Insights for telemetry and error tracking
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// HttpClient for API with extended timeout for long-running operations
builder.Services.AddHttpClient<RagApiClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(15); // Extended timeout for deep web scraping
});

// SignalR ChatHub service
builder.Services.AddScoped<ChatHubService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<RagAgentUI.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();