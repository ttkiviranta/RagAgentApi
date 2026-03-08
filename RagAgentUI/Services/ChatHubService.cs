using Microsoft.AspNetCore.SignalR.Client;

namespace RagAgentUI.Services;

public class ChatHubService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private HubConnection? _hubConnection;

    public event Func<string, Task>? OnMessageChunkReceived;
    public event Func<string, Task>? OnSourcesReceived;  // ← NEW: Sources real-time
    public event Func<Task>? OnMessageComplete;
    public event Func<string, Task>? OnError;

    public ChatHubService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

public async Task ConnectAsync()
    {
        _hubConnection = new HubConnectionBuilder()
 .WithUrl(_configuration["ApiSettings:SignalRHub"] ?? "http://localhost:5000/chathub")
.WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string>("ReceiveChunk", async (chunk) =>
 {
   Console.WriteLine($"[ChatHubService] ReceiveChunk event fired: {chunk.Length} chars");
   if (OnMessageChunkReceived != null)
       await OnMessageChunkReceived.Invoke(chunk);
   else
       Console.WriteLine("[ChatHubService] OnMessageChunkReceived is null!");
   });

        _hubConnection.On<string>("ReceiveSources", async (sourcesJson) =>  // ← NEW
        {
            Console.WriteLine($"[ChatHubService] ReceiveSources event fired: {sourcesJson.Length} chars");
            if (OnSourcesReceived != null)
                await OnSourcesReceived.Invoke(sourcesJson);
            else
                Console.WriteLine("[ChatHubService] OnSourcesReceived is null!");
        });

        _hubConnection.On("ReceiveComplete", async () =>
        {
         Console.WriteLine("[ChatHubService] ReceiveComplete event fired");
         if (OnMessageComplete != null)
   await OnMessageComplete.Invoke();
        });

        _hubConnection.On<string>("ReceiveError", async (error) =>
        {
   Console.WriteLine($"[ChatHubService] ReceiveError event fired: {error}");
   if (OnError != null)
       await OnError.Invoke(error);
        });

        try
        {
            Console.WriteLine("[ChatHubService] Starting connection...");
await _hubConnection.StartAsync();
            Console.WriteLine("[ChatHubService] Connection started successfully");
        }
    catch (Exception ex)
        {
            Console.WriteLine($"SignalR connection failed: {ex.Message}");
   }
    }
    
    public async Task StreamQueryAsync(string query, Guid conversationId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
      {
            try
  {
              await _hubConnection.SendAsync("StreamQuery", query, conversationId);
            }
         catch (Exception ex)
     {
                if (OnError != null)
       await OnError.Invoke($"Failed to send message: {ex.Message}");
            }
   }
   else
     {
      if (OnError != null)
                await OnError.Invoke("SignalR connection not available. Using fallback mode.");
   }
    }
    
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    
    public async ValueTask DisposeAsync()
 {
        if (_hubConnection is not null)
        {
    await _hubConnection.DisposeAsync();
        }
    }
}