import axios from 'axios';
import type { ChatRequestDto, ChatResponseDto, DeviceDto, AgentDto, PendingApprovalDto, KnowledgeBaseEntry, KnowledgeSearchResult } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://waisa-poc-api-hv2lph4y32udy.azurewebsites.net';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const chatApi = {
  /**
   * Send a chat message to the AI assistant
   */
  sendMessage: async (request: ChatRequestDto): Promise<ChatResponseDto> => {
    const response = await apiClient.post<ChatResponseDto>('/api/Chat', request);
    return response.data;
  },

  /**
   * Get chat history for a device
   */
  getHistory: async (deviceId: string, count: number = 20) => {
    const response = await apiClient.get(`/api/Chat/history/${deviceId}`, {
      params: { count },
    });
    return response.data;
  },
};

export const devicesApi = {
  /**
   * Get all registered devices
   */
  getAllDevices: async (): Promise<DeviceDto[]> => {
    const response = await apiClient.get<DeviceDto[]>('/api/Devices');
    return response.data;
  },

  /**
   * Get a specific device by ID
   */
  getDevice: async (deviceId: string): Promise<DeviceDto> => {
    const response = await apiClient.get<DeviceDto>(`/api/Devices/${deviceId}`);
    return response.data;
  },
};

export const healthApi = {
  /**
   * Check API health status
   */
  checkHealth: async (): Promise<string> => {
    const response = await apiClient.get<string>('/health');
    return response.data;
  },
};

export const agentsApi = {
  /**
   * Get all registered agents
   */
  getAllAgents: async (): Promise<AgentDto[]> => {
    const response = await apiClient.get<AgentDto[]>('/api/agents');
    return response.data;
  },

  /**
   * Get online agents only
   */
  getOnlineAgents: async (): Promise<AgentDto[]> => {
    const response = await apiClient.get<AgentDto[]>('/api/agents/online');
    return response.data;
  },

  /**
   * Get a specific agent by ID
   */
  getAgent: async (agentId: string): Promise<AgentDto> => {
    const response = await apiClient.get<AgentDto>(`/api/agents/${agentId}`);
    return response.data;
  },

  /**
   * Queue a command for execution on an agent
   */
  queueCommand: async (
    agentId: string,
    command: string,
    executionContext: string,
    requiresApproval: boolean = false,
    initiatedBy?: string,
    chatSessionId?: string
  ): Promise<{ commandId: string; message: string }> => {
    const response = await apiClient.post(`/api/agents/${agentId}/commands/queue`, {
      command,
      executionContext,
      requiresApproval,
      initiatedBy,
      chatSessionId,
    });
    return response.data;
  },

  /**
   * Approve a pending command
   */
  approveCommand: async (commandId: string, approvedBy: string): Promise<{ message: string }> => {
    const response = await apiClient.post(`/api/agents/commands/${commandId}/approve`, {
      approvedBy,
    });
    return response.data;
  },

  /**
   * Cancel a pending command
   */
  cancelCommand: async (commandId: string): Promise<{ message: string }> => {
    const response = await apiClient.post(`/api/agents/commands/${commandId}/cancel`);
    return response.data;
  },

  /**
   * Get all pending approval commands
   */
  getPendingApprovals: async (): Promise<PendingApprovalDto[]> => {
    const response = await apiClient.get<PendingApprovalDto[]>('/api/agents/commands/pending-approvals');
    return response.data;
  },

  /**
   * Get pending approval commands for a specific agent
   */
  getPendingApprovalsForAgent: async (agentId: string): Promise<PendingApprovalDto[]> => {
    const response = await apiClient.get<PendingApprovalDto[]>(`/api/agents/${agentId}/commands/pending-approvals`);
    return response.data;
  },
};

export const knowledgeBaseApi = {
  /**
   * Get all knowledge entries (paginated)
   */
  getAll: async (skip: number = 0, take: number = 50): Promise<KnowledgeBaseEntry[]> => {
    const response = await apiClient.get<KnowledgeBaseEntry[]>('/api/knowledgebase', {
      params: { skip, take },
    });
    return response.data;
  },

  /**
   * Search knowledge base
   */
  search: async (query: string, tags?: string[]): Promise<KnowledgeBaseEntry[]> => {
    const response = await apiClient.get<KnowledgeBaseEntry[]>('/api/knowledgebase/search', {
      params: { query, tags },
    });
    return response.data;
  },

  /**
   * Retrieve relevant knowledge using semantic search
   */
  retrieve: async (
    query: string,
    topK: number = 5,
    minScore: number = 0.7
  ): Promise<KnowledgeSearchResult[]> => {
    const response = await apiClient.post<KnowledgeSearchResult[]>('/api/knowledgebase/retrieve', {
      query,
      topK,
      minScore,
    });
    return response.data;
  },

  /**
   * Add or update knowledge entry
   */
  addOrUpdate: async (entry: Partial<KnowledgeBaseEntry>): Promise<{ message: string; id: string }> => {
    const response = await apiClient.post('/api/knowledgebase', entry);
    return response.data;
  },

  /**
   * Delete knowledge entry
   */
  delete: async (id: string): Promise<{ message: string }> => {
    const response = await apiClient.delete(`/api/knowledgebase/${id}`);
    return response.data;
  },

  /**
   * Seed knowledge base with common PowerShell cmdlets and best practices
   */
  seed: async (): Promise<{
    message: string;
    cmdlets: { success: number; failed: number; total: number };
    bestPractices: { success: number; failed: number; total: number };
    errors?: string[];
  }> => {
    const response = await apiClient.post('/api/knowledgebase/seed');
    return response.data;
  },
};
