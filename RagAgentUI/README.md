# RAG Agent UI

Modern Blazor Web App user interface for the RAG Agent API - providing real-time conversational AI with document context.

## 🎯 Features

- **Real-time Chat**: SignalR-powered streaming responses (word-by-word like ChatGPT)
- **Conversation Management**: Create, view, and switch between multiple conversations
- **Document Ingestion**: Upload and process documents via URL
- **Agent Analytics**: View performance metrics and statistics
- **Responsive Design**: Tailwind CSS for modern, mobile-friendly UI

## 🛠️ Technology Stack

- **.NET 8** - Blazor Web App with Interactive Server components
- **SignalR** - Real-time bidirectional communication
- **Tailwind CSS** - Utility-first styling
- **HttpClient** - REST API communication

## 📋 Prerequisites

- .NET 8 SDK
- Running RagAgentApi instance (port 7000)

## 🚀 Getting Started

### 1. Configuration

Edit `appsettings.json`:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7000"
  }
}
```

**Important**: Ensure the `BaseUrl` points to your running RagAgentApi instance.

### 2. Install Dependencies
```bash
cd RagAgentUI
dotnet restore
```

### 3. Run the Application

#### Development (HTTPS):
```bash
dotnet run --launch-profile https
```

The UI will be available at: **https://localhost:7170**

#### Development (HTTP):
```bash
dotnet run
```

The UI will be available at: **http://localhost:5001**

### 4. Trust Development Certificate (First Time)
```bash
dotnet dev-certs https --trust
```

Click "Yes" when prompted to avoid browser certificate warnings.

## 📁 Project Structure
```
RagAgentUI/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor       # Main application layout
│   │   └── NavMenu.razor          # Navigation menu
│   ├── Pages/
│   │   ├── Home.razor             # Chat interface
│   │   ├── Ingest.razor           # Document ingestion
│   │   └── Analytics.razor        # Agent performance metrics
│   └── App.razor                  # Root component
├── Services/
│   ├── RagApiClient.cs            # HTTP API client
│   └── ChatHubService.cs          # SignalR client for real-time chat
├── Models/
│   ├── ConversationDto.cs
│   ├── MessageDto.cs
│   ├── AgentStatsResponse.cs
│   └── ...
├── wwwroot/
│   └── css/
│       └── app.css                # Tailwind styles
├── appsettings.json               # Configuration
└── Program.cs                     # Application startup
```

## 🔧 Configuration

### API Settings

`appsettings.json`:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7000"  // RagAgentApi URL
  }
}
```

### Launch Profiles

`Properties/launchSettings.json`:
- **https**: Runs on https://localhost:7170
- **http**: Runs on http://localhost:5001

## 🎨 Key Features

### 1. Chat Interface (Home.razor)

- Real-time streaming responses via SignalR
- Conversation history with sidebar
- Message persistence
- Source citations for answers

**Usage:**
1. Click "New Chat" to start
2. Type your question
3. Get AI-generated answers with context from ingested documents

### 2. Document Ingestion (Ingest.razor)

- Ingest documents from URLs
- Real-time progress tracking
- Multi-agent pipeline visualization

**Supported Sources:**
- Web pages
- Wikipedia articles
- arXiv papers
- YouTube transcripts
- GitHub repositories

### 3. Analytics (Analytics.razor)

- Agent execution statistics
- Success rates
- Average response times
- Performance metrics

## 🔌 API Integration

### HTTP Client (RagApiClient)
```csharp
// Query conversation
var response = await ApiClient.QueryAsync(conversationId, "Your question");

// Create conversation
var conversation = await ApiClient.CreateConversationAsync();

// Ingest document
await ApiClient.IngestDocumentAsync(url);
```

### SignalR Client (ChatHubService)
```csharp
// Connect to hub
await ChatHub.ConnectAsync();

// Stream query
await ChatHub.StreamQueryAsync(query, conversationId);

// Handle streaming chunks
ChatHub.OnMessageChunkReceived += async (chunk) => 
{
    // Display chunk in real-time
};
```

## 🐛 Troubleshooting

### "Cannot connect to API"

**Check:**
1. RagAgentApi is running on port 7000
2. `appsettings.json` has correct BaseUrl
3. CORS is configured in API to allow UI origin

### "SignalR not connected"

**Check:**
1. API has SignalR configured: `app.MapHub<ChatHub>("/chathub")`
2. CORS allows credentials: `.AllowCredentials()`
3. Both API and UI use HTTPS (or both HTTP)

### Browser Console Errors

**Mixed Content (HTTP/HTTPS):**
- Ensure both API and UI use the same protocol
- Recommended: Use HTTPS for both in development

## 📦 Building for Production
```bash
dotnet publish -c Release -o ./publish
```

**Production Settings:**
- Update `appsettings.json` with production API URL
- Configure proper CORS origins
- Set up HTTPS certificates
- Enable response compression

## 🔐 Security Notes

- Always use HTTPS in production
- API keys should never be in client-side code
- CORS should be properly configured to allow only trusted origins
- SignalR connections require `.AllowCredentials()` in CORS

## 📚 Learn More

- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)
- [Tailwind CSS](https://tailwindcss.com/)



