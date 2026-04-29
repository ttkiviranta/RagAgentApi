# LLM Provider Configuration Guide

## Overview
The RAG Agent API supports multiple LLM providers for chat completions:
- **Azure OpenAI** (default) - Azure-hosted GPT models
- **OpenAI-Compatible** - Cloud endpoints like Groq, Together, Fireworks, OpenRouter (Qwen 2.5 and other compatible models)

## Configuration

### Default Provider
Set in `appsettings.json`:
```json
{
  "LlmProviders": {
    "Default": "AzureOpenAI"  // or "OpenAICompatible"
  }
}
```

### Azure OpenAI Configuration
```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://your-resource-name.openai.azure.com/",
      "Key": "your-azure-openai-key-here",
      "EmbeddingDeployment": "text-embedding-ada-002",
      "ChatDeployment": "gpt-35-turbo",
      "ApiVersion": "2024-02-15-preview",
      "MaxTokens": 8192,
      "Temperature": 0.7
    }
  }
}
```

### OpenAI-Compatible Configuration (Qwen 2.5 Example)
For Qwen 2.5 models via cloud endpoints:

```json
{
  "LlmProviders": {
    "Default": "OpenAICompatible",
    "OpenAICompatible": {
      "BaseUrl": "https://api.groq.com/openai/v1/chat/completions",
      "ApiKey": "your-api-key-here",
      "ModelName": "qwen-2.5-7b",
      "MaxTokens": 8192,
      "Temperature": 0.7
    }
  }
}
```

## Supported Cloud Endpoints

### Groq (Recommended for Qwen)
- **BaseUrl**: `https://api.groq.com/openai/v1/chat/completions`
- **Models**: `qwen-2.5-7b`, `qwen-2.5-turbo`, etc.
- **Get API Key**: https://console.groq.com

### Together AI
- **BaseUrl**: `https://api.together.xyz/v1/chat/completions`
- **Models**: `meta-llama/Meta-Llama-3-8B-Instruct`, various Qwen models
- **Get API Key**: https://www.together.ai

### Fireworks AI
- **BaseUrl**: `https://api.fireworks.ai/inference/v1/chat/completions`
- **Models**: Multiple open source models including Qwen
- **Get API Key**: https://fireworks.ai

### OpenRouter
- **BaseUrl**: `https://openrouter.ai/api/v1/chat/completions`
- **Models**: Extensive catalog including Qwen models
- **Get API Key**: https://openrouter.ai

## Usage in Code

The system automatically selects the provider based on configuration:

```csharp
// In ChatHub or other services:
private readonly IAzureOpenAIService _openAI;  // Still available for embeddings
private readonly IOpenAICompatibleLlmService _qwenLlm;  // For Qwen/alternative models

// Or use the factory:
var llmFactory = serviceProvider.GetRequiredService<LlmServiceFactory>();
var chatCompletion = await llmFactory.GetLlmService() switch
{
    IOpenAICompatibleLlmService qwen => await qwen.GetChatCompletionAsync(prompt, context),
    IAzureOpenAIService azure => await azure.GetChatCompletionAsync(prompt, context),
    _ => throw new InvalidOperationException("Unknown LLM provider")
};
```

## Migration Steps

1. **Get an API Key** from one of the supported cloud providers
2. **Update `appsettings.json`**:
   ```json
   "LlmProviders": {
     "Default": "OpenAICompatible",
     "OpenAICompatible": {
       "BaseUrl": "your-endpoint-url",
       "ApiKey": "your-api-key",
       "ModelName": "qwen-2.5-7b"
     }
   }
   ```
3. **Restart the application**
4. **Test** via chat interface or API

## Notes

- **Embeddings**: Still uses Azure OpenAI service (not changed)
- **Chat Completions**: Respects the `Default` provider setting
- **OpenAI-Compatible Protocol**: Uses standard OpenAI chat.completions format
- **Stream Support**: Both providers support streaming responses
- **Error Handling**: Automatic retries with exponential backoff
- **No Local Installation**: Cloud endpoints require no local LLM setup

## Troubleshooting

- **401 Unauthorized**: Check API key is correct and not expired
- **Connection Timeout**: Verify BaseUrl is correct for the provider
- **Model Not Found**: Ensure ModelName is valid for the selected endpoint
- **Rate Limited**: Check API quota/rate limits with your provider
