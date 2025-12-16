# RAG Agent API - Endpoint Documentation for Vue 3 UI

**Version:** 1.0  
**Base URL:** `https://localhost:7000`  
**Vue UI:** `http://localhost:5173` (CORS already configured)  
**Last Updated:** December 13, 2025

---

## ?? Table of Contents

1. [Quick Start](#quick-start)
2. [Conversations API](#conversations-api)
3. [SignalR Real-Time Chat](#signalr-real-time-chat)
4. [RAG Operations](#rag-operations)
5. [Analytics](#analytics)
6. [TypeScript Types](#typescript-types)
7. [Vue 3 Setup Guide](#vue-3-setup-guide)

---

## ?? Quick Start

### CORS Configuration
The API is already configured to accept requests from Vue UI at `http://localhost:5173`.

### Base Setup
```bash
# Install SignalR client for real-time chat
npm install @microsoft/signalr

# Install Axios for HTTP requests (optional)
npm install axios
```

---

## ?? Conversations API

### 1. Get All Conversations

**GET** `/api/conversations`

Retrieves all conversations ordered by most recent activity.

**Response (200 OK):**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Conversation 2025-12-13 14:30",
    "createdAt": "2025-12-13T14:30:00Z",
    "lastMessageAt": "2025-12-13T14:45:00Z",
    "messageCount": 5
  }
]
```

**Vue 3 Example:**
```typescript
const conversations = ref<ConversationDto[]>([]);

async function fetchConversations() {
  const response = await fetch('https://localhost:7000/api/conversations');
  conversations.value = await response.json();
}
```

---

### 2. Get Conversation History

**GET** `/api/conversations/{id}`

Retrieves all messages for a specific conversation.

**Parameters:**
- `id` (path, UUID) - Conversation identifier

**Response (200 OK):**
```json
[
  {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "role": "user",
    "content": "What is PostgreSQL?",
    "createdAt": "2025-12-13T14:30:00Z",
    "sources": null
  },
  {
    "id": "8d0f7780-8536-51ef-a05c-f18gd2g01bf8",
    "role": "assistant",
    "content": "PostgreSQL is a powerful, open source database...",
    "createdAt": "2025-12-13T14:30:15Z",
    "sources": [
      {
        "url": "https://example.com/postgres-doc",
        "content": "PostgreSQL documentation excerpt...",
        "relevanceScore": 0.92
      }
    ]
  }
]
```

**Vue 3 Example:**
```typescript
const messages = ref<MessageDto[]>([]);

async function loadHistory(conversationId: string) {
  const response = await fetch(
    `https://localhost:7000/api/conversations/${conversationId}`
  );
  messages.value = await response.json();
}
```

---

### 3. Create Conversation

**POST** `/api/conversations`

Creates a new conversation with optional title.

**Request Body:**
```json
{
  "title": "My Custom Conversation"  // Optional
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "My Custom Conversation",
  "createdAt": "2025-12-13T14:30:00Z"
}
```

**Vue 3 Example:**
```typescript
async function createConversation(title?: string) {
  const response = await fetch('https://localhost:7000/api/conversations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title })
  });
  return await response.json();
}
```

**Notes:**
- If `title` is omitted, auto-generates: `"Conversation {DateTime}"`
- Initial status is `"active"`
- Initial `messageCount` is `0`

---

## ?? SignalR Real-Time Chat

### ChatHub: `/chathub`

Real-time streaming of AI responses (word-by-word like ChatGPT).

**Important:** All JSON responses use camelCase property names (e.g., `relevanceScore`, not `RelevanceScore`).

### RAG Modes

The API supports two modes (configured in `appsettings.json` under `RagSettings:Mode`):

#### 1. **Hybrid Mode** (Default: `"hybrid"`)
- **With documents:** Uses document context (RAG) - prefixed with "?? Vastaus dokumenttien perusteella:"
- **Without documents:** Falls back to ChatGPT general knowledge - prefixed with "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:"
- **Best for:** Interactive chatbot that always helps users

#### 2. **Strict Mode** (`"strict"`)
- **With documents:** Uses document context (RAG)
- **Without documents:** Returns error message asking to ingest documents first
- **Best for:** Applications requiring only fact-based answers from documents

### Connection Setup

```typescript
// composables/useChatHub.ts
import { ref } from 'vue';
import * as signalR from '@microsoft/signalr';

export function useChatHub() {
  const isConnected = ref(false);
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://localhost:7000/chathub', {
      withCredentials: true
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

  async function connect() {
    try {
      await connection.start();
      isConnected.value = true;
      console.log('SignalR Connected');
    } catch (err) {
      console.error('SignalR Connection Error:', err);
      isConnected.value = false;
    }
  }

  async function disconnect() {
    await connection.stop();
    isConnected.value = false;
  }

  return {
    connection,
    isConnected,
    connect,
    disconnect
  };
}
```

---

### Method: `StreamQuery`

Streams AI-generated responses in real-time.

**Parameters:**
- `query` (string) - User's question
- `conversationId` (UUID) - Target conversation ID

**Behavior:**
- If no documents are found in the database (empty vector store), returns a helpful message asking user to ingest documents first
- If documents are found but relevance is too low (< 50% similarity), AI will state it cannot find relevant information
- The AI will ONLY use information from retrieved documents, never general knowledge

**Client Events:**

#### `ReceiveChunk`
Emitted for each word/token generated.

```typescript
connection.on('ReceiveChunk', (chunk: string) => {
  currentMessage.value += chunk;
});
```

#### `ReceiveComplete`
Emitted when streaming completes.

```typescript
connection.on('ReceiveComplete', () => {
  isStreaming.value = false;
  // Reload conversation history
  loadHistory(conversationId);
});
```

#### `ReceiveError`
Emitted on error.

```typescript
connection.on('ReceiveError', (error: string) => {
  console.error('Streaming error:', error);
  errorMessage.value = error;
});
```

---

### Complete Vue 3 Component Example

```vue
<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useChatHub } from '@/composables/useChatHub';

const { connection, isConnected, connect, disconnect } = useChatHub();
const query = ref('');
const conversationId = ref('');
const streamingMessage = ref('');
const isStreaming = ref(false);

onMounted(async () => {
  await connect();
  
  connection.on('ReceiveChunk', (chunk: string) => {
    streamingMessage.value += chunk;
  });
  
  connection.on('ReceiveComplete', () => {
    isStreaming.value = false;
    streamingMessage.value = '';
  });
  
  connection.on('ReceiveError', (error: string) => {
    console.error(error);
    isStreaming.value = false;
  });
});

onUnmounted(() => {
  disconnect();
});

async function sendQuery() {
  if (!query.value.trim() || !conversationId.value) return;
  
  isStreaming.value = true;
  streamingMessage.value = '';
  
  try {
    await connection.invoke('StreamQuery', query.value, conversationId.value);
  } catch (err) {
    console.error('Failed to send query:', err);
    isStreaming.value = false;
  }
  
  query.value = '';
}
</script>

<template>
  <div class="chat-container">
    <div v-if="!isConnected" class="warning">
      ?? Real-time mode unavailable
    </div>
    
    <div v-if="streamingMessage" class="streaming-message">
      {{ streamingMessage }}
      <span v-if="isStreaming" class="cursor">?</span>
    </div>
    
    <form @submit.prevent="sendQuery">
      <input v-model="query" placeholder="Ask a question..." />
      <button type="submit" :disabled="!isConnected || isStreaming">
        Send
      </button>
    </form>
  </div>
</template>
```

---

## ?? RAG Operations

### 4. Ingest Document (Enhanced)

**POST** `/api/rag/ingest-enhanced`

Ingests content from URL using dynamic agent selection (GitHub, YouTube, arXiv, news, web).

**Request Body:**
```json
{
  "url": "https://github.com/microsoft/semantic-kernel",
  "chunkSize": 1000,
  "chunkOverlap": 200
}
```

**Response (200 OK):**
```json
{
  "thread_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "message": "Content ingested successfully",
  "agent_type": "GitHubApiAgent",
  "pipeline_agents": [
    "GitHubApiAgent",
    "ChunkerAgent",
    "EmbeddingAgent",
    "PostgresStorageAgent"
  ],
  "pipeline_length": 4,
  "chunks_processed": 47,
  "document_id": "doc_123456",
  "execution_time_ms": 3450,
  "enhanced_processing": true
}
```

**Supported Content Types:**
- **GitHub:** `github.com/*` - Repository analysis, README extraction
- **YouTube:** `youtube.com/*`, `youtu.be/*` - Video transcripts
- **arXiv:** `arxiv.org/*` - Academic papers, PDF processing
- **News:** Finnish news sites (yle.fi, hs.fi, etc.)
- **Generic Web:** Any HTML page

**Vue 3 Example:**
```typescript
async function ingestDocument(url: string) {
  const response = await fetch('https://localhost:7000/api/rag/ingest-enhanced', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      url,
      chunkSize: 1000,
      chunkOverlap: 200
    })
  });
  
  if (!response.ok) {
    throw new Error('Ingestion failed');
  }
  
  return await response.json();
}
```

**Validation:**
- `chunkOverlap` must be < `chunkSize / 2`
- `url` must be valid HTTP/HTTPS URL

---

### 5. Query Content (Non-Streaming Fallback)

**POST** `/api/rag/query`

Queries the RAG system without streaming. **Note:** This endpoint now supports **hybrid mode** and uses PostgreSQL vector search.

**Recommended:** Use SignalR `/chathub` for better user experience with real-time streaming.

**Request Body:**
```json
{
  "query": "What are the key features of Semantic Kernel?",
  "topK": 5
}
```

**Response (200 OK):**

*With documents (Hybrid/Strict mode):*
```json
{
  "query": "What are the key features of Semantic Kernel?",
  "answer": "?? Vastaus dokumenttien perusteella:\n\nSemantic Kernel is an SDK that integrates LLMs...",
  "sources": [
    {
      "url": "https://github.com/microsoft/semantic-kernel",
      "content": "Semantic Kernel (SK) is a lightweight SDK...",
      "relevanceScore": 0.94
    }
  ],
  "source_count": 5,
  "processing_time_ms": 1250
}
```

*Without documents (Hybrid mode only):*
```json
{
  "query": "What is Jyväskylä population?",
  "answer": "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:\n\nJyväskylä on Suomen...",
  "sources": [],
  "source_count": 0,
  "processing_time_ms": 850
}
```

*Without documents (Strict mode):*
```json
{
  "query": "What is Jyväskylä population?",
  "answer": "Kontekstissa ei ole tietoa tähän kysymykseen. Varmista että olet ensin ladannut dokumentteja järjestelmään käyttämällä 'Ingest Document' -toimintoa.",
  "sources": [],
  "source_count": 0,
  "processing_time_ms": 450
}
```

**Vue 3 Example:**
```typescript
async function queryWithFallback(query: string) {
  const response = await fetch('https://localhost:7000/api/rag/query', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query, topK: 5 })
  });
  
  return await response.json();
}
```

---

## ?? Analytics

### 7. Get Agent Statistics

**GET** `/api/agentagentanalytics`

Retrieves execution metrics for all agents.

**Query Parameters:**
- `days` (optional, integer) - Time range in days (default: 7)

**Response (200 OK):**
```json
{
  "stats": [
    {
      "agentName": "OrchestratorAgent",
      "executionCount": 142,
      "avgDurationMs": 1520.5,
      "successRate": 0.95
    },
    {
      "agentName": "GitHubApiAgent",
      "executionCount": 87,
      "avgDurationMs": 2340.2,
      "successRate": 0.89
    }
  ]
}
```

**Vue 3 Example:**
```typescript
const stats = ref<AgentStatDto[]>([]);

async function fetchStats(days: number = 7) {
  const response = await fetch(
    `https://localhost:7000/api/agentagentanalytics?days=${days}`
  );
  const data = await response.json();
  stats.value = data.stats;
}
```

---

## ?? TypeScript Types

```typescript
// types/api.ts

// Conversation Types
export interface ConversationDto {
  id: string;
  title: string;
  createdAt: string;
  lastMessageAt: string;
  messageCount: number;
}

export interface CreateConversationRequest {
  title?: string;
}

export interface CreateConversationResponse {
  id: string;
  title: string;
  createdAt: string;
}

// Message Types
export interface MessageDto {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
  sources?: SourceDto[];
}

export interface SourceDto {
  url: string;
  content: string;
  relevanceScore: number;
}

// RAG Types
export interface IngestRequest {
  url: string;
  chunkSize?: number;
  chunkOverlap?: number;
}

export interface IngestResponse {
  thread_id: string;
  message: string;
  agent_type: string;
  pipeline_agents: string[];
  pipeline_length: number;
  chunks_processed: number;
  document_id: string;
  execution_time_ms: number;
  enhanced_processing: boolean;
}

export interface QueryRequest {
  query: string;
  topK?: number;
}

export interface QueryResponse {
  query: string;
  answer: string;
  sources: SourceDto[];
  source_count: number;
  processing_time_ms: number;
}

// Analytics Types
export interface AgentStatDto {
  agentName: string;
  executionCount: number;
  avgDurationMs: number;
  successRate: number;
}

export interface AgentStatsResponse {
  stats: AgentStatDto[];
}

// Health Check Types
export interface HealthCheckResponse {
  status: 'healthy' | 'degraded';
  services: {
    azure_search: 'ok' | 'error';
    azure_openai: 'ok' | 'error';
  };
  timestamp: string;
  version: string;
}
```

---

## ?? Vue 3 Setup Guide

### 1. Project Structure

```
vue-rag-ui/
??? src/
?   ??? components/
?   ?   ??? ChatMessage.vue
?   ?   ??? ConversationList.vue
?   ?   ??? IngestForm.vue
?   ??? composables/
?   ?   ??? useChatHub.ts
?   ?   ??? useApi.ts
?   ??? types/
?   ?   ??? api.ts
?   ??? views/
?   ?   ??? ChatView.vue
?   ?   ??? IngestView.vue
?   ?   ??? AnalyticsView.vue
?   ??? App.vue
?   ??? main.ts
??? vite.config.ts
??? package.json
```

---

### 2. Vite Configuration

```typescript
// vite.config.ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false
      },
      '/chathub': {
        target: 'https://localhost:7000',
        changeOrigin: true,
        secure: false,
        ws: true  // Enable WebSocket for SignalR
      }
    }
  }
})
```

---

### 3. API Client Composable

```typescript
// composables/useApi.ts
import { ref } from 'vue';
import type { 
  ConversationDto, 
  MessageDto, 
  CreateConversationRequest,
  IngestRequest,
  QueryRequest 
} from '@/types/api';

export function useApi() {
  const baseUrl = 'https://localhost:7000';
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  async function request<T>(
    endpoint: string, 
    options?: RequestInit
  ): Promise<T> {
    isLoading.value = true;
    error.value = null;
    
    try {
      const response = await fetch(`${baseUrl}${endpoint}`, {
        headers: {
          'Content-Type': 'application/json',
          ...options?.headers
        },
        ...options
      });
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      return await response.json();
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Unknown error';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  // Conversations
  const getConversations = () => 
    request<ConversationDto[]>('/api/conversations');
  
  const getConversationHistory = (id: string) =>
    request<MessageDto[]>(`/api/conversations/${id}`);
  
  const createConversation = (data: CreateConversationRequest) =>
    request('/api/conversations', {
      method: 'POST',
      body: JSON.stringify(data)
    });

  // RAG
  const ingestDocument = (data: IngestRequest) =>
    request('/api/rag/ingest-enhanced', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  
  const query = (data: QueryRequest) =>
    request('/api/rag/query', {
      method: 'POST',
      body: JSON.stringify(data)
    });

  // Analytics
  const getAgentStats = (days: number = 7) =>
    request(`/api/agentagentanalytics?days=${days}`);

  return {
    isLoading,
    error,
    getConversations,
    getConversationHistory,
    createConversation,
    ingestDocument,
    query,
    getAgentStats
  };
}
```

---

## ?? Error Handling

### Standard Error Response
```json
{
  "error": "Error description",
  "details": ["Specific error 1"],
  "timestamp": "2025-12-13T14:30:00Z",
  "thread_id": "optional-thread-id"
}
```

### Common HTTP Status Codes
- `200 OK` - Success
- `400 Bad Request` - Invalid input
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error
- `503 Service Unavailable` - Dependency unavailable

---

## ?? Support & Resources

- **Swagger UI:** `https://localhost:7000` (development)
- **Health Endpoint:** `/health`
- **GitHub Repository:** [RagAgentApi](https://github.com/ttkiviranta/RagAgentApi)
- **Documentation:** See README.md and SETUP.md

---

**Generated:** December 13, 2025  
**API Version:** 1.0  
**Documentation Version:** 1.0
