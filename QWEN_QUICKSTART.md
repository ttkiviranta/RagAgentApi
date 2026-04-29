# Quick Start: Using Qwen 2.5 Models

Get up and running with Qwen 2.5 in 5 minutes.

## 1. Get an API Key (2 minutes)

**Choose one provider:**

### Groq (Recommended)
- Go to https://console.groq.com/keys
- Sign up (free)
- Copy your API key
- Qwen models available: `qwen-2.5-7b`, `qwen-2.5-turbo`

### Together AI
- Go to https://www.together.ai
- Sign up and create API key
- Base URL: `https://api.together.xyz/v1/chat/completions`

### Fireworks AI
- Go to https://fireworks.ai
- Create account, generate API key
- Base URL: `https://api.fireworks.ai/inference/v1/chat/completions`

## 2. Configure the Application (1 minute)

**Edit `appsettings.json` or environment variables:**

```json
{
  "LlmProviders": {
    "Default": "OpenAICompatible",
    "OpenAICompatible": {
      "BaseUrl": "https://api.groq.com/openai/v1/chat/completions",
      "ApiKey": "gsk_YOUR_API_KEY_HERE",
      "ModelName": "qwen-2.5-7b",
      "MaxTokens": 8192,
      "Temperature": 0.7
    }
  }
}
```

Or via environment variables:
```bash
set LlmProviders__Default=OpenAICompatible
set LlmProviders__OpenAICompatible__BaseUrl=https://api.groq.com/openai/v1/chat/completions
set LlmProviders__OpenAICompatible__ApiKey=gsk_YOUR_API_KEY
set LlmProviders__OpenAICompatible__ModelName=qwen-2.5-7b
```

## 3. Restart Application (30 seconds)

```bash
# Stop current instance
# Start application
```

## 4. Test in Chat (1 minute)

1. Open the Blazor UI
2. Create a new conversation
3. Ask a question
4. Verify response comes from Qwen (check logs)

## Done! 🎉

The system is now using Qwen 2.5 models for all chat completions.

## Verify It's Working

### Check Logs
```
[LlmService] Using OpenAI-compatible provider for streamed chat completion
[OpenAICompatibleLlm] Starting streamed chat completion
[OpenAICompatibleLlm] Got completion response with XXX tokens
```

### No Changes to Your Code!
- ChatHub works unchanged ✅
- Agents work unchanged ✅
- API endpoints work unchanged ✅
- Only configuration changed ✅

## Models Available

| Provider | Model | Speed | Quality | Cost |
|----------|-------|-------|---------|------|
| Groq | `qwen-2.5-7b` | ⚡ Fast | ✓ Good | Free |
| Groq | `qwen-2.5-turbo` | ⚡⚡ Very Fast | ✓ Good | Free |
| Groq | `mixtral-8x7b-32768` | ⚡ Fast | ✓✓ Better | Free |
| Together | `qwen-2.5-32b` | Normal | ✓✓✓ Excellent | $ |
| Together | `meta-llama/Meta-Llama-3-70B` | Normal | ✓✓✓ Excellent | $$ |

## Quick Reference

### Switch Provider
```json
{
  "LlmProviders": {
    "Default": "AzureOpenAI"  // Back to Azure
  }
}
```

### Change Model
```json
{
  "LlmProviders": {
    "OpenAICompatible": {
      "ModelName": "mixtral-8x7b-32768"  // Try Mixtral
    }
  }
}
```

### Adjust Temperature (Creativity)
```json
{
  "LlmProviders": {
    "OpenAICompatible": {
      "Temperature": 0.3  // More deterministic (0.0-1.0)
    }
  }
}
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| 401 Error | Check API key in configuration |
| No response | Verify BaseUrl is correct |
| Slow responses | Try different time or provider |
| Model not found | Check available models for provider |

## Next Steps

- Read full setup guide: `QWEN_MODEL_SETUP.md`
- View detailed config options: `LLM_PROVIDER_CONFIGURATION.md`
- Check implementation details: `QWEN_2.5_MODEL_SUPPORT.md`

## Support

- Groq Issues: https://console.groq.com/docs
- Together Issues: https://docs.together.ai
- Fireworks Issues: https://docs.fireworks.ai
- OpenRouter Issues: https://openrouter.ai/docs
