using RagAgentUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient for API
builder.Services.AddHttpClient<RagApiClient>();

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