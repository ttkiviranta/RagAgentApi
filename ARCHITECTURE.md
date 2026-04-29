# Architecture: Qwen 2.5 Model Support

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Blazor UI (RagAgentUI)                      │
│                    Chat Interface / User Interaction                │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                         SignalR (Real-time)
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    ChatHub (RagAgentApi.Hubs)                       │
│              Receives messages, routes to services                  │
└────────────┬────────────────────────────────────────┬───────────────┘
             │                                        │
             ▼                                        ▼
    ┌─────────────────────┐         ┌────────────────────────┐
    │ PostgresQueryService │         │ LlmService (Router)    │
    │  Vector Search       │         │  Config-Driven Router  │
    │  (Embeddings)        │         │  Selects Provider      │
    └─────────────────────┘         └────────┬───────────────┘
             │                                │
             │                    ┌───────────┴──────────────┐
             │                    │                          │
             ▼                    ▼                          ▼
    ┌──────────────────┐  ┌──────────────────┐  ┌───────────────────┐
    │ AzureOpenAIService│  │ AzureOpenAIService│  │OpenAICompatibleLlm│
    │   (Embeddings)   │  │  (Chat - fallback)│  │ Service (Qwen)    │
    │                  │  │                   │  │                   │
    │ Azure SDK       │  │ Azure SDK        │  │ HttpClient        │
    │ text-embedding- │  │ gpt-35-turbo     │  │ to cloud endpoint │
    │ ada-002         │  │                   │  │                   │
    └──────────────────┘  └──────────────────┘  └───────────────────┘
             │                    │                          │
             │                    │                    ┌─────┴─────┐
             │                    │                    │           │
             ▼                    ▼                    ▼           ▼
         Azure          Azure         Groq      Together   Fireworks
        Vectorial      Cognitive   api.groq.com api.together  api.fireworks
        Search        Services               .xyz            ai
        Service        (gpt-35)
```

## Provider Selection Logic

```
Configuration (appsettings.json)
        ↓
  LlmProviders:Default = ?
        ↓
    ┌───┴─────────────────┐
    │                     │
    ▼                     ▼
"AzureOpenAI"      "OpenAICompatible"
    │                     │
    ▼                     ▼
AzureOpenAIService  OpenAICompatibleLlmService
    │                     │
    ├─ Embeddings         ├─ Groq
    └─ Chat Completions   ├─ Together
                          ├─ Fireworks
                          └─ OpenRouter
```

## Message Flow: Chat with Qwen

```
1. User sends message
   └─> ChatHub.StreamQuery()

2. Get embedding
   └─> AzureOpenAIService.GetEmbeddingAsync()
       └─> Azure Cognitive Services

3. Vector search
   └─> PostgresQueryService.SearchAsync()
       └─> PostgreSQL with pgvector

4. Route to LLM
   └─> LlmService decides provider
       ├─ If Default = "AzureOpenAI"
       │  └─> AzureOpenAIService.GetChatCompletionStreamAsync()
       │      └─> Azure Cognitive Services
       │
       └─ If Default = "OpenAICompatible"
          └─> OpenAICompatibleLlmService.GetChatCompletionStreamAsync()
              └─> Cloud endpoint (Groq/Together/etc)
                  └─> Qwen 2.5 Model

5. Stream response chunks
   └─> Yield chunks to ChatHub

6. Send to client
   └─> SignalR: Clients.Caller.SendAsync("ReceiveChunk")
       └─> Blazor UI displays in real-time
```

## Request/Response Flow: OpenAI-Compatible

```
OpenAICompatibleLlmService
        │
        ├─ Build ChatCompletionRequest
        │  ├─ Model: "qwen-2.5-7b"
        │  ├─ Messages:
        │  │  ├─ Role: "system", Content: [system prompt]
        │  │  └─ Role: "user", Content: [user query + context]
        │  ├─ MaxTokens: 8192
        │  ├─ Temperature: 0.7
        │  └─ Stream: true
        │
        ├─ Send HTTP POST
        │  └─> https://api.groq.com/openai/v1/chat/completions
        │      Headers:
        │      - Authorization: Bearer {ApiKey}
        │      - Content-Type: application/json
        │
        ├─ Receive SSE Stream
        │  ├─ data: {"choices":[{"delta":{"content":"Hello"}}]}
        │  ├─ data: {"choices":[{"delta":{"content":" world"}}]}
        │  └─ data: [DONE]
        │
        └─ Yield content chunks
           ├─ "Hello"
           ├─ " world"
           └─ [end]
```

## Configuration Hierarchy

```
Default Configuration (appsettings.json)
        │
        ├─ Environment-specific (appsettings.{env}.json)
        │
        └─ Environment Variables
           ├─ LlmProviders__Default
           ├─ LlmProviders__OpenAICompatible__BaseUrl
           ├─ LlmProviders__OpenAICompatible__ApiKey
           └─ LlmProviders__OpenAICompatible__ModelName
```

## Class Diagram

```
┌────────────────────────────────────────┐
│    LlmService (Unified Router)         │
├────────────────────────────────────────┤
│ - _azureOpenAI: IAzureOpenAIService    │
│ - _openAICompatible: IOpenAICompat...  │
│ - _configuration: IConfiguration       │
├────────────────────────────────────────┤
│ + GetChatCompletionAsync()             │
│ + GetChatCompletionStreamAsync()       │
│ - GetActiveProvider()                  │
└────────────────────────────────────────┘
         │                        │
         ▼                        ▼
┌──────────────────────┐  ┌────────────────────────┐
│IAzureOpenAIService   │  │IOpenAICompatibleLlm    │
├──────────────────────┤  ├────────────────────────┤
│ + GetEmbedding()     │  │ + GetChatCompletion()  │
│ + GetChatCompletion()│  │ + GetChatCompletionSt()│
│ + Stream async       │  └────────────────────────┘
└──────────────────────┘            ▲
         │                          │
         ▼                          ▼
   AzureOpenAI...        OpenAICompatibleLlmService
   Implementation             Implementation
```

## Dependency Injection Graph

```
Program.cs
   │
   ├─ AddSingleton<IAzureOpenAIService, AzureOpenAIService>()
   │  │
   │  └─> Uses: IConfiguration, ILogger
   │
   ├─ AddSingleton<IOpenAICompatibleLlmService, OpenAICompatibleLlmService>()
   │  │
   │  └─> Uses: IConfiguration, ILogger, HttpClient
   │
   ├─ AddSingleton<LlmService>()
   │  │
   │  └─> Uses: IAzureOpenAIService, IOpenAICompatibleLlmService
   │
   └─ AddSingleton<LlmServiceFactory>()
      │
      └─> Uses: IServiceProvider, IConfiguration, ILogger
```

## Error Handling Flow

```
GetChatCompletionAsync()
   │
   ├─ Send HTTP Request
   │  │
   │  ├─ Success (200)
   │  │  └─> Parse response, return content
   │  │
   │  └─ Failure
   │     └─> HttpRequestException / RequestFailedException
   │        │
   │        ├─ Attempt < maxRetries (3)
   │        │  └─> Delay (2s, 4s, 8s) and retry
   │        │
   │        └─ Attempt >= maxRetries
   │           └─> Log error, throw exception
   │
   └─ JSON Parse Error
      └─> Log warning, skip chunk, continue
```

## Stream Processing

```
HTTP SSE Stream
   │
   ├─ "data: {delta_json}"
   │  └─> ParseStreamDelta()
   │      ├─ Extract "choices[0].delta.content"
   │      └─ Yield string to consumer
   │
   ├─ ": keepalive" (comment)
   │  └─> Skip
   │
   ├─ "data: [DONE]"
   │  └─> Break loop, end stream
   │
   └─ Empty line
      └─> Skip
```

## Configuration Resolution

```
appsettings.json
   ├─ LlmProviders:
   │  ├─ Default: "OpenAICompatible"
   │  └─ OpenAICompatible:
   │     ├─ BaseUrl: "https://api.groq.com/..."
   │     ├─ ApiKey: "gsk_..."
   │     ├─ ModelName: "qwen-2.5-7b"
   │     ├─ MaxTokens: 8192
   │     └─ Temperature: 0.7
   │
   └─> IConfiguration.GetSection("LlmProviders")
       └─> Bind to OpenAICompatibleConfig class
           └─> Used by OpenAICompatibleLlmService
```

## Alternative Providers Comparison

```
Provider      | Free | Speed | Quality | Setup | Models
──────────────┼──────┼───────┼─────────┼───────┼────────────────
Groq          | Yes  | ⚡⚡⚡  | Good    | Easy  | Qwen, Mixtral
Together      | Yes  | ⚡⚡   | Better  | Easy  | Many models
Fireworks     | Ltd  | ⚡⚡   | Good    | Easy  | Many models
OpenRouter    | Yes  | ⚡    | Varied  | Easy  | 150+ models

All implement standard OpenAI chat.completions API
```

## Deployment Options

```
Development
   └─> Groq (free, fast)
       └─> appsettings.Development.json

Staging
   └─> Groq or Together
       └─> Environment variables

Production
   ├─> Multiple providers (via load balancer)
   │
   └─> Primary: Groq/Together
       Fallback: Azure OpenAI (built-in)
```

## Monitoring Points

```
Request Flow Logging:
   ├─ [LlmService] Using provider: {provider}
   ├─ [OpenAICompatibleLlm] Getting chat completion
   ├─ [OpenAICompatibleLlm] Starting streamed completion
   ├─ [OpenAICompatibleLlm] Got completion response with {tokens}
   └─ [OpenAICompatibleLlm] Error: {error_message}

Metrics to track:
   ├─ Response latency (ms)
   ├─ Token usage (per request)
   ├─ Error rate (%)
   ├─ API quota usage
   └─ Cost per query ($)
```

## Security Architecture

```
User Request
   │
   ├─ ChatHub (HTTPS/SignalR)
   │
   └─> LlmService
       │
       ├─ Azure OpenAI (via Azure SDK, Credentials)
       │
       └─ OpenAICompatibleLlmService
          │
          └─> Cloud Endpoint (HTTPS, Bearer Token)
              │
              └─> LLM Model (in provider's secure environment)
```

This architecture ensures:
- 🔐 No LLM models locally stored
- 🔒 All connections encrypted (HTTPS)
- 🔑 API keys in secure configuration
- 🚀 Easy provider switching
- ⚡ Real-time streaming support
- 🔄 Automatic error handling & retry logic
