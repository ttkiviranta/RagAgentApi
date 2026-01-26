# Vue UI - Conversation Management Implementation Prompt

## Context
I have a RAG Agent API with Blazor UI that has working conversation management features. I need to implement the same features in my Vue.js UI.

## Current State
- ? Vue UI has working query/chat functionality
- ? API endpoints exist and work (tested with Blazor UI)
- ? Vue UI needs conversation list, history, and management

## API Endpoints Available

### 1. Get All Conversations
```http
GET https://localhost:7000/api/conversations
Response: Array of ConversationDto
```

**ConversationDto:**
```typescript
interface ConversationDto {
  id: string;           // GUID
  title: string | null;
  createdAt: string;    // ISO date
  lastMessageAt: string;
  messageCount: number;
}
```

### 2. Get Conversation History
```http
GET https://localhost:7000/api/conversations/{id}
Response: Array of MessageDto
```

**MessageDto:**
```typescript
interface MessageDto {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
  sources: SourceDto[] | null;
}

interface SourceDto {
  url: string;
  content: string;
  relevanceScore: number;
}
```

### 3. Create New Conversation
```http
POST https://localhost:7000/api/conversations
Content-Type: application/json
Body: { "title": "Optional Title" }

Response: CreateConversationResponse
```

**CreateConversationResponse:**
```typescript
interface CreateConversationResponse {
  id: string;
  title: string;
  createdAt: string;
}
```

## Required Features

### 1. Conversation List Sidebar
- Display all conversations ordered by lastMessageAt (newest first)
- Show conversation title and message count
- Highlight selected/active conversation
- "New Chat" button at top
- Click conversation to load its history

### 2. Conversation History View
- Load and display messages for selected conversation
- Show user messages (right side, blue)
- Show assistant messages (left side, gray)
- Display timestamps
- Show source citations if available
- Auto-scroll to bottom when new messages arrive

### 3. New Conversation Dialog/Modal
- Input field for optional conversation title
- "Create" and "Cancel" buttons
- If title empty, API generates automatic title
- After creation, switch to new conversation

### 4. Integration with Existing Query System
- When user sends a query, associate it with current conversation
- If no conversation selected, create new one automatically
- Refresh conversation list after messages sent

## Technical Requirements

### State Management (Pinia Store)
```typescript
// stores/conversationStore.ts
interface ConversationState {
  conversations: ConversationDto[];
  currentConversationId: string | null;
  messages: MessageDto[];
  isLoading: boolean;
  error: string | null;
}

// Actions needed:
- fetchConversations()
- loadConversation(id: string)
- createConversation(title?: string)
- refreshCurrentConversation()
```

### API Service
```typescript
// services/conversationApi.ts
class ConversationService {
  async getConversations(): Promise<ConversationDto[]>
  async getConversationHistory(id: string): Promise<MessageDto[]>
  async createConversation(title?: string): Promise<CreateConversationResponse>
}
```

### Components Structure
```
src/
??? components/
?   ??? conversation/
?   ?   ??? ConversationList.vue      // Sidebar with all conversations
?   ?   ??? ConversationItem.vue      // Single conversation in list
?   ?   ??? MessageList.vue           // Display messages
?   ?   ??? MessageBubble.vue         // Single message (user/assistant)
?   ?   ??? NewConversationDialog.vue // Modal for creating new chat
?   ??? ...
??? stores/
?   ??? conversationStore.ts          // Pinia store
??? services/
    ??? conversationApi.ts             // API calls
```

## UI/UX Design Guidelines

### Layout
```
???????????????????????????????????????????
?  Sidebar (25%)     ?  Main Chat (75%)   ?
?                    ?                     ?
?  [+ New Chat]      ?  ????????????????? ?
?                    ?  ?  Chat Header  ? ?
?  ????????????      ?  ????????????????? ?
?  ?Conv 1    ?      ?                     ?
?  ?2 msgs    ?      ?  ????????????????? ?
?  ????????????      ?  ?  User: Hello  ? ?
?                    ?  ????????????????? ?
?  ????????????      ?  ????????????????? ?
?  ?Conv 2    ???????????  Bot: Hi!     ? ?
?  ?5 msgs    ?      ?  ????????????????? ?
?  ????????????      ?                     ?
?                    ?  ????????????????? ?
?                    ?  ?  Input Box    ? ?
?                    ?  ????????????????? ?
???????????????????????????????????????????
```

### Styling (Tailwind CSS)
- Sidebar: `bg-gray-100 dark:bg-gray-800`
- Selected conversation: `bg-blue-100 dark:bg-blue-900`
- User messages: `bg-blue-500 text-white` (right aligned)
- Assistant messages: `bg-gray-200 text-gray-900` (left aligned)
- Timestamps: `text-xs text-gray-500`

## Implementation Steps

1. **Create TypeScript interfaces** for all DTOs
2. **Create API service** (conversationApi.ts) with axios
3. **Create Pinia store** (conversationStore.ts)
4. **Create ConversationList component** (sidebar)
5. **Create MessageList component** (chat display)
6. **Create NewConversationDialog component** (modal)
7. **Update main chat view** to integrate conversations
8. **Test all features** with running API

## Example Code Structure

### API Service (conversationApi.ts)
```typescript
import axios from 'axios';

const API_BASE = 'https://localhost:7000';

export const conversationApi = {
  async getAll(): Promise<ConversationDto[]> {
    const { data } = await axios.get(`${API_BASE}/api/conversations`);
    return data;
  },
  
  async getHistory(id: string): Promise<MessageDto[]> {
    const { data } = await axios.get(`${API_BASE}/api/conversations/${id}`);
    return data;
  },
  
  async create(title?: string): Promise<CreateConversationResponse> {
    const { data } = await axios.post(`${API_BASE}/api/conversations`, { title });
    return data;
  }
};
```

### Pinia Store (conversationStore.ts)
```typescript
import { defineStore } from 'pinia';
import { conversationApi } from '@/services/conversationApi';

export const useConversationStore = defineStore('conversation', {
  state: (): ConversationState => ({
    conversations: [],
    currentConversationId: null,
    messages: [],
    isLoading: false,
    error: null
  }),
  
  actions: {
    async fetchConversations() {
      this.isLoading = true;
      try {
        this.conversations = await conversationApi.getAll();
      } catch (error) {
        this.error = 'Failed to load conversations';
        console.error(error);
      } finally {
        this.isLoading = false;
      }
    },
    
    async loadConversation(id: string) {
      this.currentConversationId = id;
      this.isLoading = true;
      try {
        this.messages = await conversationApi.getHistory(id);
      } catch (error) {
        this.error = 'Failed to load conversation history';
        console.error(error);
      } finally {
        this.isLoading = false;
      }
    },
    
    async createConversation(title?: string) {
      try {
        const newConv = await conversationApi.create(title);
        await this.fetchConversations(); // Refresh list
        await this.loadConversation(newConv.id); // Load new conversation
        return newConv;
      } catch (error) {
        this.error = 'Failed to create conversation';
        console.error(error);
        throw error;
      }
    }
  },
  
  getters: {
    currentConversation: (state) => 
      state.conversations.find(c => c.id === state.currentConversationId),
    
    sortedConversations: (state) =>
      [...state.conversations].sort((a, b) => 
        new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime()
      )
  }
});
```

## Integration with Existing Query System

When user sends a query:
1. Check if `currentConversationId` exists
2. If not, call `createConversation()` first
3. Send query with `conversationId` to existing query endpoint
4. After response, call `fetchConversations()` to update message counts

## Testing Checklist

- [ ] Conversations load on app start
- [ ] Clicking conversation loads its messages
- [ ] "New Chat" button creates new conversation
- [ ] Messages display correctly (user vs assistant)
- [ ] Timestamps are formatted nicely
- [ ] Source citations display when available
- [ ] Auto-scroll works when new messages arrive
- [ ] Conversation list updates after sending message
- [ ] Dark mode works for all components
- [ ] Responsive design works on mobile

## Notes
- API is already running and tested with Blazor UI
- Use same axios instance as existing query functionality
- Follow Vue 3 Composition API style
- Use TypeScript for type safety
- Use Tailwind CSS for styling
- Consider SignalR integration later for real-time updates

## Questions to Consider
- Should conversations be auto-saved or require explicit save?
- Should there be conversation delete/archive functionality?
- Should conversation titles be editable?
- Should there be conversation search/filter?

Please implement these features following Vue 3 best practices, TypeScript types, and Tailwind CSS styling consistent with the existing Vue UI codebase.
