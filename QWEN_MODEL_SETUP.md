# Qwen 2.5 Model Support - Implementation Guide

## Overview

RAG Agent API now supports **Qwen 2.5** models without local installation through OpenAI-compatible cloud endpoints like Groq, Together, Fireworks, and OpenRouter.

## Architecture

### New Components

1. **IOpenAICompatibleLlmService** - Interface for OpenAI-compatible LLM providers
2. **OpenAICompatibleLlmService** - Implementation using HttpClient for cloud APIs
3. **LlmService** - Unified router that selects between Azure OpenAI and OpenAI-compatible providers
4. **LlmServiceFactory** - Factory for explicit provider selection
5. **LlmProviderType** - Enum for provider types

### Configuration

The system uses a configuration-driven provider selection:

```json
{
  "LlmProviders": {
    "Default": "AzureOpenAI",  // or "OpenAICompatible"
    "OpenAICompatible": {
      "BaseUrl": "https://api.groq.com/openai/v1/chat/completions",
      "ApiKey": "gsk_...",
      "ModelName": "qwen-2.5-7b",
      "MaxTokens": 8192,
      "Temperature": 0.7
    }
  }
}
```

## Setup Instructions

### Step 1: Choose a Cloud Provider

#### Option A: Groq (Recommended for Qwen)
1. Visit https://console.groq.com
2. Sign up for free account
3. Create an API key
4. Available models: `qwen-2.5-7b`, `qwen-2.5-turbo`, `mixtral-8x7b-32768`, etc.

#### Option B: Together AI
1. Visit https://www.together.ai
2. Create account and generate API key
3. BaseUrl: `https://api.together.xyz/v1/chat/completions`
4. Browse available models: https://docs.together.ai/reference/inference-api

#### Option C: Fireworks AI
1. Visit https://fireworks.ai
2. Create account and get API key
3. BaseUrl: `https://api.fireworks.ai/inference/v1/chat/completions`
4. Models: https://docs.fireworks.ai/overview

#### Option D: OpenRouter
1. Visit https://openrouter.ai
2. Create account and generate API key
3. BaseUrl: `https://openrouter.ai/api/v1/chat/completions`
4. Largest model catalog

### Step 2: Update Configuration

**appsettings.json:**
```json
{
  "LlmProviders": {
    "Default": "OpenAICompatible",
    "OpenAICompatible": {
      "BaseUrl": "https://api.groq.com/openai/v1/chat/completions",
      "ApiKey": "gsk_YOUR_API_KEY",
      "ModelName": "qwen-2.5-7b",
      "MaxTokens": 8192,
      "Temperature": 0.7
    }
  }
}
```

### Step 3: Restart Application

The application will automatically:
- Load the configuration
- Create OpenAICompatibleLlmService instance
- Route chat requests through configured provider

## Usage in Code

### Default Usage (No Changes Required)

ChatHub continues to work without modifications. The `LlmService` automatically routes:

```csharp
// In ChatHub or any service:
private readonly IAzureOpenAIService _openAI;

// For embeddings (still uses Azure OpenAI):
var embedding = await _openAI.GetEmbeddingAsync(text);

// For chat - routes to configured provider:
await foreach (var chunk in _openAI.GetChatCompletionStreamAsync(query, context))
{
    // Receives streamed response
}
```

### Explicit Provider Selection

Use LlmService for automatic routing:

```csharp
private readonly LlmService _llmService;

// Automatically uses configured provider
var response = await _llmService.GetChatCompletionAsync(prompt, context);

// Streaming:
await foreach (var chunk in _llmService.GetChatCompletionStreamAsync(prompt, context))
{
    await SendChunk(chunk);
}
```

### Direct Provider Access

For specific provider selection:

```csharp
private readonly LlmServiceFactory _factory;

// Get specific provider
var qwenService = _factory.GetOpenAICompatibleService();
var response = await qwenService.GetChatCompletionAsync(prompt, context);

// Or Azure OpenAI specifically
var azureService = _factory.GetAzureOpenAIService();
var embedding = await azureService.GetEmbeddingAsync(text);
```

## API Compatibility

The OpenAI-compatible service implements the same interface as Azure OpenAI:

```csharp
Task<string> GetChatCompletionAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default);
IAsyncEnumerable<string> GetChatCompletionStreamAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default);
```

Both providers:
- Accept same message format (system + user)
- Support streaming responses
- Include automatic retry logic with exponential backoff
- Handle errors consistently
- Log operations at debug/info level

## Available Qwen Models

| Model | Provider | Context | Parameters | Use Case |
|-------|----------|---------|-----------|----------|
| qwen-2.5-7b | Groq | 32k | 7B | General chat, RAG |
| qwen-2.5-turbo | Groq | 32k | ~7B | Faster responses |
| qwen-2.5-32b | Together, Fireworks | 32k | 32B | Complex reasoning |
| qwen-2.5-72b | Together, Fireworks | 32k | 72B | Advanced tasks |
| qwen-2.5-coder | Groq, Together | 32k | 7B | Code generation |

## Features

✅ **Cloud Endpoints** - No local LLM setup required  
✅ **OpenAI Protocol** - Standard chat.completions format  
✅ **Streaming Support** - Real-time response streaming  
✅ **Error Handling** - Automatic retries with exponential backoff  
✅ **Configuration-Driven** - Switch providers via JSON config  
✅ **Logging** - Full debug/info logging for troubleshooting  
✅ **Backward Compatible** - Existing code works unchanged  
✅ **Flexible** - Easy to add new compatible providers  

## System Prompt

Both providers receive the same system prompt structure:

### With Context (RAG Mode)
```
You are a helpful AI assistant. You have access to some context documents below.

INSTRUCTIONS:
1. Check if context contains relevant information
2. If YES: Answer based on context and cite it
3. If NO: Start with "[General knowledge]" and use your knowledge
4. Always respond in user's language
5. Be concise and helpful

Context: [provided documents]

Now answer the user's question...
```

### Without Context (General Knowledge Mode)
```
You are a helpful AI assistant. Answer based on your general knowledge.
Be concise, accurate, and helpful.
IMPORTANT: Always respond in the same language as the user's question.
```

## Troubleshooting

### 401 Unauthorized
- **Cause**: Invalid or expired API key
- **Fix**: Verify key in configuration and provider console
- **Check**: Paste key directly from provider website

### Connection Timeout
- **Cause**: Incorrect BaseUrl or network issue
- **Fix**: Test endpoint with curl:
  ```bash
  curl -X POST https://api.groq.com/openai/v1/chat/completions \
    -H "Authorization: Bearer YOUR_KEY" \
    -H "Content-Type: application/json" \
    -d '{"model":"qwen-2.5-7b","messages":[{"role":"user","content":"test"}]}'
  ```

### Model Not Found
- **Cause**: ModelName not available on provider
- **Fix**: Check provider's model list and update `ModelName` in config
- **Example**: Some providers may use `qwen2.5-7b` instead of `qwen-2.5-7b`

### Rate Limited (429)
- **Cause**: Exceeded API quota
- **Fix**: Check provider console for usage, upgrade plan, or wait
- **Retry**: System automatically retries with exponential backoff

### No Response / Stream Empty
- **Cause**: Provider API response parsing issue
- **Fix**: Check logs for JSON parsing errors
- **Debug**: Verify response format matches OpenAI spec

## Performance Considerations

### Latency
- Groq: ~100-200ms (fastest)
- Together: ~200-500ms
- Fireworks: ~300-800ms
- OpenRouter: ~500-1500ms (varies by upstream)

### Cost
- Groq: Free tier available
- Together: $0.001/1k tokens (typical)
- Fireworks: Various pricing
- OpenRouter: Dynamic routing by model

### Recommendations

**For Production RAG:**
- Use Groq with qwen-2.5-7b (best latency-cost)
- Or Together with larger models for better quality

**For Development:**
- Use Groq free tier (unlimited calls)
- Or OpenRouter for model variety

## Migration from Azure OpenAI

No code changes required! Simply:

1. Update `LlmProviders.Default` to `"OpenAICompatible"`
2. Add provider configuration
3. Restart application
4. All chat completions automatically use new provider
5. Embeddings still use Azure OpenAI

Rollback is as simple as changing `Default` back to `"AzureOpenAI"`.

## Monitoring

### Logs to Check
```
[OpenAICompatibleLlm] Using OpenAI-compatible provider for chat completion
[OpenAICompatibleLlm] Getting chat completion for prompt of length {Length}
[OpenAICompatibleLlm] Starting streamed chat completion
[OpenAICompatibleLlm] Got completion response with {TokenCount} tokens
```

### Metrics
- Response times per provider
- Token usage by model
- Error rates and retry counts
- Provider API availability

## Security Notes

- **API Keys**: Store in environment variables or secure configuration, never in source code
- **HTTPS**: All providers use HTTPS (enforced)
- **Data**: Prompts sent to provider servers (check provider privacy policy)
- **Sensitive Data**: Don't send PII through unreviewed providers

## Future Enhancements

- [ ] Load balancing across multiple providers
- [ ] Fallback to secondary provider on failure
- [ ] Token counting before sending to optimize costs
- [ ] Request caching for repeated queries
- [ ] Provider-specific optimization (timeout tuning, retry strategies)

## Support & Documentation

- **Groq**: https://console.groq.com/docs
- **Together**: https://docs.together.ai
- **Fireworks**: https://docs.fireworks.ai
- **OpenRouter**: https://openrouter.ai/docs

## Related Files

- `RagAgentApi/Services/IOpenAICompatibleLlmService.cs` - Interface
- `RagAgentApi/Services/OpenAICompatibleLlmService.cs` - Implementation
- `RagAgentApi/Services/LlmService.cs` - Unified router
- `RagAgentApi/Services/LlmServiceFactory.cs` - Factory
- `appsettings.json` - Configuration
- `Program.cs` - Dependency injection setup
