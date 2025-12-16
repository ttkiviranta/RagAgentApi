# Vue UI Implementation Status & Guide

## ?? Current Status

### ? What Works
- **REST API (`/api/rag/query`)**: Now supports hybrid mode with PostgreSQL
- **Conversations API**: Full CRUD operations
- **Ingest API**: Document ingestion with progress tracking
- **Analytics API**: Agent performance metrics

### ?? What Needs Implementation
- **SignalR Real-Time Chat**: Documented but not yet implemented in Vue UI
- **Streaming Responses**: Currently uses HTTP fallback instead of WebSocket

---

## ?? Quick Fix: Using HTTP Fallback

The `/api/rag/query` endpoint now supports hybrid mode, so Vue UI will work correctly even without SignalR.

### Current Behavior

#### With Hybrid Mode (Default)
**Without documents:**
```json
{
  "answer": "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:\n\nJyväskylä on Suomen...",
  "sources": [],
  "relevanceScore": []
}
```

**With documents:**
```json
{
  "answer": "?? Vastaus dokumenttien perusteella:\n\nPostgreSQL on...",
  "sources": [
    {"url": "...", "relevanceScore": 0.95}
  ]
}
```

### Testing Vue UI (HTTP Mode)

1. **Start API:**
```bash
cd RagAgentApi
dotnet run
```

2. **Check API health:**
```bash
curl https://localhost:7000/api/rag/health
```

3. **Test query without documents:**
```bash
curl -X POST "https://localhost:7000/api/rag/query" \
  -H "Content-Type: application/json" \
  -d '{"query":"mikä on Jyväskylän pääkaupunki?","topK":5}'
```

Expected: Answer from general knowledge with ?? prefix

4. **Ingest a document:**
```bash
curl -X POST "https://localhost:7000/api/rag/ingest-enhanced" \
  -H "Content-Type: application/json" \
  -d '{"url":"https://fi.wikipedia.org/wiki/Jyväskylä","chunkSize":1000,"chunkOverlap":200}'
```

5. **Test query with documents:**
```bash
curl -X POST "https://localhost:7000/api/rag/query" \
  -H "Content-Type: application/json" \
  -d '{"query":"mikä on Jyväskylän asukasluku?","topK":5}'
```

Expected: Answer from documents with ?? prefix

---

## ?? Implementing SignalR in Vue UI (Recommended)

### Why SignalR?
- ? Real-time streaming (word-by-word like ChatGPT)
- ? Better user experience
- ? Lower latency
- ? Automatic reconnection

### Implementation Steps

#### 1. Install Dependencies
```bash
npm install @microsoft/signalr
```

#### 2. Create SignalR Composable

**File: `src/composables/useChatHub.ts`**
```typescript
import { ref } from 'vue';
import * as signalR from '@microsoft/signalr';

export function useChatHub() {
  const isConnected = ref(false);
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://localhost:7000/chathub', {
      withCredentials: true,
      skipNegotiation: false
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  async function connect() {
    if (connection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await connection.start();
      isConnected.value = true;
      console.log('[SignalR] Connected successfully');
    } catch (err) {
      console.error('[SignalR] Connection failed:', err);
      isConnected.value = false;
      throw err;
    }
  }

  async function disconnect() {
    if (connection.state === signalR.HubConnectionState.Connected) {
      await connection.stop();
      isConnected.value = false;
      console.log('[SignalR] Disconnected');
    }
  }

  // Auto-reconnect handler
  connection.onreconnecting((error) => {
    console.warn('[SignalR] Reconnecting...', error);
    isConnected.value = false;
  });

  connection.onreconnected(() => {
    console.log('[SignalR] Reconnected');
    isConnected.value = true;
  });

  connection.onclose((error) => {
    console.error('[SignalR] Connection closed', error);
    isConnected.value = false;
  });

  return {
    connection,
    isConnected,
    connect,
    disconnect
  };
}
```

#### 3. Update Chat Component

**File: `src/views/ChatView.vue`**
```vue
<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useChatHub } from '@/composables/useChatHub';
import { useApi } from '@/composables/useApi';

const { connection, isConnected, connect, disconnect } = useChatHub();
const api = useApi();

const query = ref('');
const conversationId = ref<string | null>(null);
const streamingMessage = ref('');
const isStreaming = ref(false);
const messages = ref<any[]>([]);

onMounted(async () => {
  // Connect to SignalR
  try {
    await connect();
  } catch (err) {
    console.warn('SignalR not available, will use HTTP fallback');
  }

  // Setup event handlers
  connection.on('ReceiveChunk', (chunk: string) => {
    streamingMessage.value += chunk;
  });

  connection.on('ReceiveComplete', async () => {
    isStreaming.value = false;
    streamingMessage.value = '';
    
    // Reload conversation history
    if (conversationId.value) {
      await loadHistory(conversationId.value);
    }
  });

  connection.on('ReceiveError', (error: string) => {
    console.error('Streaming error:', error);
    isStreaming.value = false;
    streamingMessage.value = '';
    alert(`Error: ${error}`);
  });

  // Load or create conversation
  await initConversation();
});

onUnmounted(() => {
  disconnect();
});

async function initConversation() {
  const conversations = await api.getConversations();
  
  if (conversations.length > 0) {
    conversationId.value = conversations[0].id;
    await loadHistory(conversations[0].id);
  } else {
    const newConv = await api.createConversation();
    conversationId.value = newConv.id;
  }
}

async function loadHistory(id: string) {
  messages.value = await api.getConversationHistory(id);
}

async function sendQuery() {
  if (!query.value.trim() || !conversationId.value) return;

  const userQuery = query.value;
  query.value = '';

  // Add user message optimistically
  messages.value.push({
    id: crypto.randomUUID(),
    role: 'user',
    content: userQuery,
    createdAt: new Date().toISOString(),
    sources: null
  });

  isStreaming.value = true;
  streamingMessage.value = '';

  try {
    if (isConnected.value) {
      // Use SignalR streaming
      await connection.invoke('StreamQuery', userQuery, conversationId.value);
    } else {
      // Fallback to HTTP
      console.warn('Using HTTP fallback (SignalR not connected)');
      const response = await api.query({ query: userQuery, topK: 5 });
      
      messages.value.push({
        id: crypto.randomUUID(),
        role: 'assistant',
        content: response.answer,
        createdAt: new Date().toISOString(),
        sources: response.sources
      });
      
      isStreaming.value = false;
    }
  } catch (err) {
    console.error('Failed to send query:', err);
    isStreaming.value = false;
    alert(`Error sending query: ${err}`);
  }
}
</script>

<template>
  <div class="chat-container">
    <!-- Connection Status -->
    <div v-if="!isConnected" class="warning">
      ?? Real-time mode unavailable - using fallback
    </div>
    <div v-else class="success">
      ? Connected (real-time streaming enabled)
    </div>

    <!-- Messages -->
    <div class="messages">
      <div v-for="msg in messages" :key="msg.id" 
           :class="['message', msg.role]">
        <div class="content">{{ msg.content }}</div>
        
        <!-- Sources -->
        <div v-if="msg.sources?.length" class="sources">
          <h4>?? Sources:</h4>
          <div v-for="(source, idx) in msg.sources" :key="idx" class="source">
            <a :href="source.url" target="_blank">{{ source.url }}</a>
            <span class="score">{{ Math.round(source.relevanceScore * 100) }}% match</span>
          </div>
        </div>
      </div>

      <!-- Streaming Message -->
      <div v-if="streamingMessage" class="message assistant streaming">
        <div class="content">{{ streamingMessage }}</div>
        <span v-if="isStreaming" class="cursor">?</span>
      </div>
    </div>

    <!-- Input -->
    <form @submit.prevent="sendQuery" class="input-form">
      <input 
        v-model="query"
        :disabled="isStreaming"
        placeholder="Ask a question..."
        type="text"
      />
      <button type="submit" :disabled="isStreaming || !query.trim()">
        {{ isStreaming ? 'Sending...' : 'Send' }}
      </button>
    </form>
  </div>
</template>

<style scoped>
.warning {
  background: #fff3cd;
  color: #856404;
  padding: 10px;
  border-radius: 4px;
  margin-bottom: 10px;
}

.success {
  background: #d4edda;
  color: #155724;
  padding: 10px;
  border-radius: 4px;
  margin-bottom: 10px;
}

.message {
  margin: 10px 0;
  padding: 10px;
  border-radius: 8px;
}

.message.user {
  background: #007bff;
  color: white;
  text-align: right;
}

.message.assistant {
  background: #f1f1f1;
  color: black;
}

.message.streaming .cursor {
  animation: blink 1s infinite;
}

@keyframes blink {
  0%, 50% { opacity: 1; }
  51%, 100% { opacity: 0; }
}

.sources {
  margin-top: 10px;
  font-size: 0.9em;
  border-top: 1px solid #ccc;
  padding-top: 10px;
}

.source {
  display: flex;
  justify-content: space-between;
  margin: 5px 0;
}

.score {
  color: #666;
  font-size: 0.85em;
}

.input-form {
  display: flex;
  gap: 10px;
  margin-top: 20px;
}

input {
  flex: 1;
  padding: 10px;
  border: 1px solid #ccc;
  border-radius: 4px;
}

button {
  padding: 10px 20px;
  background: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

button:disabled {
  background: #ccc;
  cursor: not-allowed;
}
</style>
```

---

## ?? Troubleshooting

### SignalR Connection Issues

**Problem:** `SignalR Connection Error: Failed to start connection`

**Solutions:**
1. Check CORS settings in API `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueUI", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR!
    });
});
```

2. Verify SignalR hub mapping:
```csharp
app.MapHub<ChatHub>("/chathub");
```

3. Check browser console for specific errors

### NaN% Relevance Scores

**Problem:** Sources show "NaN%" instead of percentages

**Status:** ? **FIXED** - API now uses camelCase JSON serialization

**Verification:**
```typescript
// Should see: relevanceScore (not RelevanceScore)
console.log(source.relevanceScore); // 0.95
```

### No Response from API

**Problem:** Query returns no answer or "kontekstissa ei ole tietoa"

**Solutions:**
1. **Check if documents are ingested:**
```sql
SELECT COUNT(*) FROM document_chunks WHERE embedding IS NOT NULL;
```

2. **Verify hybrid mode is enabled:**
```json
{
  "RagSettings": {
    "Mode": "hybrid"
  }
}
```

3. **Test with curl:**
```bash
curl -X POST "https://localhost:7000/api/rag/query" \
  -H "Content-Type: application/json" \
  -d '{"query":"test question","topK":5}'
```

---

## ?? Additional Resources

- **Full API Documentation**: `Documentation/API_Endpoints.md`
- **Configuration Guide**: `Documentation/CONFIGURATION.md`
- **Blazor UI Reference**: `RagAgentUI/` (working implementation example)

---

**Last Updated:** December 13, 2025  
**Status:** HTTP fallback ? working, SignalR ?? needs implementation
