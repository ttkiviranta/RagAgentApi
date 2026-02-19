# Agent-to-Agent (A2A) Pipeline Demo

Demonstrating real-time agent communication and orchestration in a multi-agent RAG system.

## 🔄 Overview

The A2A Pipeline Demo showcases how multiple AI agents communicate sequentially in the RAG pipeline:

```
User Input
    ↓
[OrchestratorAgent] - Coordinates pipeline execution
    ↓
[ScraperAgent] - Extracts content from URLs
    ↓
[ChunkerAgent] - Splits content into semantic chunks
    ↓
[EmbeddingAgent] - Generates vector embeddings
    ↓
[PostgresStorageAgent] - Stores documents and embeddings
    ↓
[PostgresQueryAgent] - Performs semantic search
    ↓
Results with agent metrics and timing
```

## 🚀 Features

### Protocol Features
- **Message Routing**: Agents route messages through the system with proper validation
- **Status Tracking**: Each message tracks its lifecycle (Pending → Processing → Completed/Failed)
- **Execution Metrics**: Individual agent execution times and error handling
- **Conversation History**: Complete message history for audit and debugging
- **Agent Registry**: Dynamic agent registration and capability discovery

### Demo Capabilities
- **Real-time Visualization**: See agents communicating in action
- **Performance Metrics**: Track execution times at each step
- **Error Handling**: Graceful error recovery and reporting
- **Extensible Architecture**: Easy to add new agents to the pipeline

## 📊 Pipeline Steps

### Step 1: Scraper Agent
- **From**: OrchestratorAgent
- **To**: ScraperAgent
- **Task**: Extract content from GitHub repository
- **Output**: Raw content data

### Step 2: Chunker Agent
- **From**: ScraperAgent
- **To**: ChunkerAgent
- **Task**: Split content into semantic chunks
- **Output**: Array of text chunks with overlap

### Step 3: Embedding Agent
- **From**: ChunkerAgent
- **To**: EmbeddingAgent
- **Task**: Generate vector embeddings for chunks
- **Output**: 1536-dimensional vectors

### Step 4: Storage Agent
- **From**: EmbeddingAgent
- **To**: PostgresStorageAgent
- **Task**: Persist documents and vectors to database
- **Output**: Indexed embeddings in PostgreSQL

### Step 5: Query Agent
- **From**: OrchestratorAgent
- **To**: PostgresQueryAgent
- **Task**: Perform semantic search on stored vectors
- **Output**: Top-K similar documents

## 🎮 Usage

### Via Web UI (Recommended)

1. Navigate to `https://localhost:7000/demos/a2a`
2. Click **"▶ Run A2A Pipeline"** button
3. Watch real-time agent communication
4. Click **"🤖 Load Agents"** to see registered agents
5. Expand detailed steps to inspect messages

### Via REST API

#### Run A2A Pipeline Demo
```bash
curl -X POST "https://localhost:7000/api/demo/run?demoType=a2a-pipeline" \
  -H "Content-Type: application/json"
```

**Response:**
```json
{
  "demoType": "a2a-pipeline",
  "success": true,
  "message": "Agent-to-Agent pipeline executed successfully",
  "data": {
    "conversationId": "550e8400-e29b-41d4-a716-446655440000",
    "totalSteps": 5,
    "totalMessages": 5,
    "pipelineSteps": [
      {
        "stepNumber": 1,
        "fromAgent": "OrchestratorAgent",
        "toAgent": "ScraperAgent",
        "messageType": "scrape",
        "description": "Extract content from: https://github.com/microsoft/semantic-kernel",
        "statusCode": 200,
        "executionTimeMs": 145,
        "messageCount": 1
      },
      // ... more steps
    ],
    "summary": {
      "totalExecutionTimeMs": 850,
      "averageStepTimeMs": 170,
      "agentsInvolved": 6
    }
  },
  "executionTimeMs": "850ms"
}
```

#### Get Registered Agents
```bash
curl "https://localhost:7000/api/a2a/agents"
```

**Response:**
```json
[
  {
    "id": "orch-001",
    "name": "OrchestratorAgent",
    "type": "coordinator",
    "description": "Orchestrates pipeline and routes tasks",
    "capabilities": ["routing", "orchestration", "task-distribution"],
    "registeredAt": "2024-01-15T10:30:00Z"
  },
  // ... more agents
]
```

#### Send Direct Message
```bash
curl -X POST "https://localhost:7000/api/a2a/send-message" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "550e8400-e29b-41d4-a716-446655440000",
    "fromAgentName": "OrchestratorAgent",
    "toAgentName": "ScraperAgent",
    "messageType": "scrape",
    "content": "Extract content from URL",
    "payload": {
      "url": "https://example.com",
      "timeout": 5000
    }
  }'
```

#### Get Message History
```bash
curl "https://localhost:7000/api/a2a/conversations/{conversationId}/messages"
```

## 🏗️ Architecture

### A2AProtocolService
Core protocol implementation managing agent communication.

```csharp
public interface IA2AProtocolService
{
    Task<A2AMessage> SendMessageAsync(A2AMessage message);
    Task<List<A2AMessage>> GetMessageHistoryAsync(string conversationId);
    Task RegisterAgentAsync(A2AAgent agent);
    Task<List<A2AAgent>> GetRegisteredAgentsAsync();
}
```

### A2ADemoService
Orchestrates the pipeline demo execution.

```csharp
public class A2ADemoService : IDemoService
{
    public async Task<DemoResult> RunDemoAsync()
    {
        // Execute 5 sequential pipeline steps
        // Track execution metrics
        // Return comprehensive results
    }
}
```

### A2APipeline.razor
Blazor component for real-time visualization.

**Features:**
- Live agent monitoring
- Pipeline execution visualization
- Detailed step inspection
- Message history viewing
- Performance metrics display

## 📈 Performance Metrics

Each pipeline step captures:
- **Execution Time**: Time from message send to response
- **Message Count**: Number of messages exchanged
- **Status Code**: Success (200) or failure status
- **Payload Data**: Message content and responses

### Example Metrics
```
Step 1 (Orchestrator → Scraper):     145ms
Step 2 (Scraper → Chunker):          152ms
Step 3 (Chunker → Embedding):        198ms
Step 4 (Embedding → Storage):        215ms
Step 5 (Orchestrator → QueryAgent):  140ms

Total Execution Time:                 850ms
Average Step Time:                    170ms
```

## 🔧 Adding New Agents

To add a new agent to the pipeline:

1. **Create Agent Instance**
```csharp
var newAgent = new A2AAgent
{
    Name = "CustomAgent",
    Type = "processor",
    Description = "My custom agent",
    Capabilities = new List<string> { "capability1", "capability2" }
};
```

2. **Register Agent**
```csharp
await protocolService.RegisterAgentAsync(newAgent);
```

3. **Update A2ADemoService**
```csharp
var step = await ExecutePipelineStep(
    conversationId,
    "PreviousAgent",
    "CustomAgent",
    "custom-task",
    "Do something custom",
    new { /* payload */ }
);
```

## 🐛 Troubleshooting

### Pipeline Executes But Shows No Results
- Check browser console for errors
- Verify API is running on correct port
- Check network tab for failed requests

### Agents Not Appearing
- Click "🤖 Load Agents" button
- Verify A2A services are registered in Program.cs
- Check API logs for registration errors

### Slow Pipeline Execution
- Check system resources (CPU, memory)
- Verify database connection
- Review Azure OpenAI service quotas

### Message Routing Errors
- Verify agent names match exactly (case-sensitive)
- Check agent registration in A2AProtocolService
- Review API response for detailed error messages

## 📚 Related Documentation

- [DEMO_SERVICES.md](./DEMO_SERVICES.md) - Overview of all demo services
- [README.md](../README.md) - Main project documentation
- [API Documentation](https://localhost:7000) - Swagger UI

## 🔐 Security Considerations

- A2A messages are stored in memory (not persistent by default)
- Implement database persistence for production use
- Add authentication/authorization for agent registration
- Validate message payloads before processing
- Monitor for suspicious agent behavior

## 🚀 Future Enhancements

- [ ] Persistent message storage with PostgreSQL
- [ ] Agent authentication and authorization
- [ ] Message encryption for sensitive payloads
- [ ] Agent load balancing across multiple instances
- [ ] Real-time WebSocket updates for pipeline visualization
- [ ] Agent performance analytics and monitoring
- [ ] Parallel agent execution for non-sequential tasks
- [ ] Agent negotiation protocol for dynamic routing

## 💡 Use Cases

1. **Educational**: Learn how multi-agent systems work
2. **Debugging**: Trace message flow through pipeline
3. **Performance Analysis**: Identify bottlenecks in processing
4. **Agent Development**: Test new agents before production
5. **Monitoring**: Track agent health and communication
