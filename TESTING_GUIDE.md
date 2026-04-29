# Testing Guide: Qwen 2.5 Model Support

## ⚠️ Important Notice

This feature has been implemented but **has NOT yet been tested in production**. 
Complete end-to-end testing is required before deploying to production environments.

## 📋 Testing Checklist

### 1. Basic Configuration Testing (5 minutes)

- [ ] Application starts without errors with default config (Azure OpenAI)
- [ ] Application starts without errors with Qwen config
- [ ] Configuration loading works via environment variables
- [ ] Default falls back to Azure OpenAI when not specified

**How to test**:
```bash
# Test default config
dotnet run

# Test with Qwen via environment variables
set LlmProviders__Default=OpenAICompatible
set LlmProviders__OpenAICompatible__BaseUrl=https://api.groq.com/openai/v1/chat/completions
set LlmProviders__OpenAICompatible__ApiKey=gsk_YOUR_TEST_KEY
dotnet run
```

### 2. Chat Interface Testing (10 minutes)

- [ ] Open Blazor UI chat interface
- [ ] Send simple message without documents (general knowledge mode)
- [ ] Verify response is generated correctly
- [ ] Verify response appears in real-time (streaming works)
- [ ] Check logs for provider selection message

**Expected log entry**:
```
[LlmService] Using OpenAI-compatible provider for streamed chat completion
[OpenAICompatibleLlm] Starting streamed chat completion
```

### 3. RAG Testing (10 minutes)

- [ ] Upload a test document
- [ ] Send message with query relevant to document
- [ ] Verify response uses document context (should cite source)
- [ ] Check sources appear correctly
- [ ] Verify streaming works with context

**Expected**:
- Response includes citations from document
- Sources are listed at bottom of response
- Response streams in real-time

### 4. Error Handling Testing (10 minutes)

#### Test Invalid API Key
- [ ] Set invalid API key in config
- [ ] Send chat message
- [ ] Verify 401 error handling
- [ ] Check error is logged appropriately
- [ ] Check user sees error message

#### Test Invalid Endpoint
- [ ] Set wrong BaseUrl
- [ ] Send chat message
- [ ] Verify connection error handling
- [ ] Check error is logged

#### Test Rate Limiting (if applicable)
- [ ] Send multiple requests rapidly
- [ ] Verify retry logic engages
- [ ] Check exponential backoff works
- [ ] Verify eventual success or proper error

### 5. Performance Testing (15 minutes)

#### Latency Measurements
- [ ] Measure response time for small query (< 100 tokens)
  - **Expected**: Groq 100-200ms, Together 200-500ms
- [ ] Measure response time for large query (> 1000 tokens)
- [ ] Compare with Azure OpenAI performance
- [ ] Document baseline metrics

#### Streaming Performance
- [ ] Verify chunks arrive in reasonable intervals
- [ ] Check CPU usage during streaming
- [ ] Check memory usage during long responses
- [ ] Verify no memory leaks after multiple requests

### 6. Provider-Specific Testing

#### Groq Testing
- [ ] Test with qwen-2.5-7b model
- [ ] Test with qwen-2.5-turbo model
- [ ] Test with mixtral-8x7b-32768 model
- [ ] Verify free tier quota is sufficient

#### Together Testing
- [ ] Test with qwen-2.5-32b model
- [ ] Verify pricing calculations are accurate
- [ ] Check API quota usage

#### Other Providers
- [ ] Test Fireworks if applicable
- [ ] Test OpenRouter if applicable

### 7. Concurrent Request Testing (10 minutes)

- [ ] Open multiple chat windows/tabs
- [ ] Send concurrent chat requests
- [ ] Verify all requests complete successfully
- [ ] Check no request interference
- [ ] Monitor API rate limits

### 8. Fallback Testing (10 minutes)

- [ ] Configure both Azure OpenAI and Qwen
- [ ] Start with Qwen provider
- [ ] Change config to Azure OpenAI
- [ ] Restart application
- [ ] Verify Azure OpenAI is used
- [ ] Test reverse switching

### 9. System Integration Testing (20 minutes)

- [ ] Test with full RAG pipeline:
  - [ ] Document ingestion
  - [ ] Vector embeddings (should still use Azure)
  - [ ] Vector search
  - [ ] Chat completion with Qwen
- [ ] Test conversation history
- [ ] Test multi-turn conversations
- [ ] Test conversation persistence

### 10. Logging and Monitoring (10 minutes)

- [ ] Check debug logs show provider selection
- [ ] Verify token usage is logged
- [ ] Check error logs for issues
- [ ] Monitor Application Insights (if configured)
- [ ] Verify no sensitive data in logs

## 🧪 Detailed Test Scenarios

### Scenario 1: Simple Chat Query
```
Steps:
1. Open chat interface
2. Type: "Explain machine learning in 2 sentences"
3. Observe response streams in real-time
4. Check logs for provider

Expected:
- Response appears character by character
- Complete response within 5 seconds
- Provider logged correctly
```

### Scenario 2: Document-Based Query
```
Steps:
1. Upload document (PDF or TXT)
2. Type query about document content
3. Observe response with citations
4. Check sources listed

Expected:
- Response cites document
- Sources appear at bottom
- Stream completes within 10 seconds
```

### Scenario 3: Multi-turn Conversation
```
Steps:
1. Send: "What is Kubernetes?"
2. Send: "Explain Pods"
3. Send: "How do they relate?"
4. Observe context carried through

Expected:
- Each response appears
- Conversation flows naturally
- All responses use same provider
```

### Scenario 4: Error Recovery
```
Steps:
1. Set invalid API key
2. Send chat message
3. Observe error handling
4. Correct API key
5. Send chat message again

Expected:
- Clear error on first attempt
- Recovery works on second attempt
- No app crash
```

## 📊 Test Results Template

```markdown
# Qwen 2.5 Model Support - Test Results

## Configuration Testing
- Default config: ✅/❌
- Qwen config: ✅/❌
- Environment variables: ✅/❌

## Chat Interface Testing
- Simple message: ✅/❌
- Document-based chat: ✅/❌
- Streaming: ✅/❌
- Response quality: ✅/❌

## Error Handling
- Invalid API key: ✅/❌
- Connection timeout: ✅/❌
- Rate limiting: ✅/❌
- Recovery: ✅/❌

## Performance
- Groq latency (ms): _____
- Together latency (ms): _____
- Streaming chunks/sec: _____
- Memory usage (MB): _____

## Provider Testing
- Groq: ✅/❌
- Together: ✅/❌
- Fireworks: ✅/❌
- OpenRouter: ✅/❌

## Issues Found
1. [List any issues]

## Sign-Off
Tested by: ___________
Date: ___________
Status: Ready for Prod / Needs Work
```

## 🔍 Known Limitations & Considerations

### Not Yet Tested
- [ ] Production-scale load (100+ concurrent requests)
- [ ] Very large documents (> 10MB)
- [ ] Extended conversation history (> 100 messages)
- [ ] All possible error scenarios
- [ ] Different network conditions
- [ ] Different operating systems (if deploying cross-platform)

### Provider-Specific Considerations
- **Groq**: Free tier has request limits - monitor quota
- **Together**: Requires payment - set up cost limits
- **Fireworks**: May have quota restrictions
- **OpenRouter**: Dynamic routing adds latency

### Security Considerations
- **API Keys**: Ensure never exposed in logs
- **Prompt Injection**: Test malicious input handling
- **Data Privacy**: Review provider privacy policy
- **Compliance**: Check GDPR/data handling requirements

## 📝 Test Report Template

When testing is complete, create TEST_REPORT.md with:

```markdown
# Qwen 2.5 Model Support - Test Report

## Executive Summary
[Overall status and recommendations]

## Test Coverage
- Configuration: [%]
- Functionality: [%]
- Error Handling: [%]
- Performance: [%]

## Issues Found
### Critical
- [List critical issues]

### Major
- [List major issues]

### Minor
- [List minor issues]

## Performance Metrics
- Average latency: [ms]
- Peak latency: [ms]
- Memory usage: [MB]
- Error rate: [%]

## Recommendations
1. [Before production use]
2. [Suggested improvements]

## Sign-Off
Tested by: ___________
Date: ___________
Approved for production: ✅/❌
```

## 🚀 Production Deployment Checklist

Only proceed to production after:

- [ ] All critical tests pass
- [ ] No blocker issues remain
- [ ] Performance meets requirements
- [ ] Security review completed
- [ ] Documentation reviewed
- [ ] Rollback procedure documented
- [ ] Monitoring/alerting configured
- [ ] Team trained on new feature
- [ ] Test report approved by lead
- [ ] Deployment plan created

## 📞 Support & Troubleshooting

### During Testing
- Check logs: `dotnet run` output or Application Insights
- Verify config: Check appsettings.json values
- Test endpoint: Use curl to verify API connectivity
- Contact provider support: For provider-specific issues

### Common Issues
| Issue | Cause | Fix |
|-------|-------|-----|
| 401 Unauthorized | Bad API key | Verify key from provider |
| Connection timeout | Wrong BaseUrl | Check config value |
| Model not found | Invalid ModelName | List available models |
| Empty response | Response parsing error | Check logs, try different model |

## Next Steps

1. Run this test checklist
2. Document results in TEST_REPORT.md
3. Fix any issues found
4. Get sign-off from team lead
5. Deploy to production
6. Monitor performance

---

**Last Updated**: 2024  
**Status**: Ready for Testing  
**Estimated Testing Time**: 2-3 hours  
**Recommended Testers**: Backend Dev + DevOps/SRE
