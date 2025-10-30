import type {
  ChatHistorySearchRequest,
  ChatHistorySearchResult,
  AgentChatMessage,
  UserChatMessage,
  AgentConversationSummary,
  UserConversationSummary
} from '../types';

const API_URL = import.meta.env.VITE_API_URL || 'https://waisa-poc-api-hv2lph4y32udy.azurewebsites.net';

export const chatHistoryApi = {
  // Agent Chat History
  async getAgentConversations(agentId: string, maxResults = 50): Promise<AgentConversationSummary[]> {
    const response = await fetch(
      `${API_URL}/api/ChatHistory/agent/${agentId}/conversations?maxResults=${maxResults}`
    );
    if (!response.ok) throw new Error('Failed to fetch agent conversations');
    return response.json();
  },

  async getAgentConversation(agentId: string, conversationId: string): Promise<AgentChatMessage[]> {
    const response = await fetch(
      `${API_URL}/api/ChatHistory/agent/${agentId}/conversation/${conversationId}`
    );
    if (!response.ok) throw new Error('Failed to fetch agent conversation');
    return response.json();
  },

  async searchAgentChat(
    agentId: string,
    request: ChatHistorySearchRequest
  ): Promise<ChatHistorySearchResult<AgentChatMessage>> {
    const response = await fetch(
      `${API_URL}/api/ChatHistory/agent/${agentId}/search`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      }
    );
    if (!response.ok) throw new Error('Failed to search agent chat');
    return response.json();
  },

  // User Chat History
  async getUserConversations(userId: string, maxResults = 50): Promise<UserConversationSummary[]> {
    const response = await fetch(
      `${API_URL}/api/ChatHistory/user/${userId}/conversations?maxResults=${maxResults}`
    );
    if (!response.ok) throw new Error('Failed to fetch user conversations');
    return response.json();
  },

  async getUserConversation(userId: string, conversationId: string): Promise<UserChatMessage[]> {
    const response = await fetch(
      `${API_URL}/api/ChatHistory/user/${userId}/conversation/${conversationId}`
    );
    if (!response.ok) throw new Error('Failed to fetch user conversation');
    return response.json();
  },

  async searchUserChat(
    userId: string,
    request: ChatHistorySearchRequest
  ): Promise<ChatHistorySearchResult<UserChatMessage>> {
    const response = await fetch(
      `${API_URL}/api/ChatHistory/user/${userId}/search`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      }
    );
    if (!response.ok) throw new Error('Failed to search user chat');
    return response.json();
  }
};
