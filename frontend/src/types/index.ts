// API Types matching backend DTOs

export interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  commands?: ExecutedCommand[];
}

export interface ExecutedCommand {
  command: string;
  output: string;
  success: boolean;
  errorMessage?: string;
  executedAt: Date;
}

export interface ActivityLog {
  timestamp: string;
  type: string;
  message: string;
  icon: string;
  details?: string;
}

export interface ChatRequestDto {
  deviceId: string;
  message: string;
  conversationId?: string;
}

export interface ChatResponseDto {
  message: string;
  executedCommands: ExecutedCommand[];
  activityLogs: ActivityLog[];
  success: boolean;
  tokensUsed?: number;
  contextSummary?: string;
  relevantKnowledge?: KnowledgeReferenceDto[];
}

export interface KnowledgeReferenceDto {
  id: string;
  title: string;
  similarityScore: number;
}

export interface DeviceDto {
  deviceId: string;
  deviceName: string;
  contextSummary: string;
  totalInteractions: number;
  lastInteractionAt: Date;
  lastSummarizedAt?: Date;
  metadata: Record<string, string>;
}

// Agent Management Types

export type AgentStatus = 'Online' | 'Offline' | 'Error' | 'Disabled';

export interface DiskInfoDto {
  driveLetter: string;
  totalSizeGB: number;
  freeSpaceGB: number;
  usagePercent: number;
  volumeLabel: string;
}

export interface SystemInformationDto {
  computerName: string;
  currentUser?: string;
  osVersion?: string;
  systemUptime?: string;
  cpuUsagePercent: number;
  totalMemoryMB: number;
  availableMemoryMB: number;
  usedMemoryMB: number;
  memoryUsagePercent: number;
  disks: DiskInfoDto[];
  loggedInUsers: string[];
  ipAddress?: string;
  domain?: string;
  collectedAt: Date;
}

export interface AgentDto {
  agentId: string;
  computerName: string;
  status: AgentStatus;
  lastHeartbeat?: Date;
  installDate: Date;
  version: string;
  osVersion?: string;
  lastSystemInfo?: SystemInformationDto;
  isEnabled: boolean;
}

export interface CommandDto {
  commandId: string;
  agentId: string;
  command: string;
  executionContext: string;
  status: string;
  createdAt: Date;
  startedAt?: Date;
  completedAt?: Date;
  output?: string;
  error?: string;
  executionTimeSeconds?: number;
  requiresApproval: boolean;
  approved?: boolean;
  approvedBy?: string;
  approvedAt?: Date;
  initiatedBy?: string;
  chatSessionId?: string;
}

export interface PendingApprovalDto {
  commandId: string;
  agentId: string;
  agentName: string;
  command: string;
  executionContext: string;
  createdAt: Date;
  timeoutSeconds: number;
  initiatedBy: string;
  chatSessionId?: string;
}

// Knowledge Base Types

export interface KnowledgeBaseEntry {
  id: string;
  title: string;
  content: string;
  source: string;
  sourceDeviceId?: string;
  tags: string[];
  contentVector: number[];
  usageCount: number;
  averageRating?: number;
  createdAt: Date;
  updatedAt: Date;
  lastUsedAt?: Date;
}

export interface KnowledgeSearchResult {
  entry: KnowledgeBaseEntry;
  similarityScore: number;
  isRelevant: boolean;
}

// Chat History Types

export const DateRangeFilter = {
  Last24Hours: 0,
  Last7Days: 1,
  Last30Days: 2,
  Last90Days: 3,
  Custom: 4,
  AllTime: 5
} as const;

export type DateRangeFilter = typeof DateRangeFilter[keyof typeof DateRangeFilter];

export interface ChatHistoryMessage {
  id: string;
  conversationId: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  agentId?: string;
  agentName?: string;
  userId?: string;
  userName?: string;
}

export interface AgentChatMessage extends ChatHistoryMessage {
  agentId: string;
  agentName: string;
  userId: string;
}

export interface UserChatMessage extends ChatHistoryMessage {
  userId: string;
}

export interface ChatHistorySearchRequest {
  dateRange: DateRangeFilter;
  searchTerm?: string;
  conversationId?: string;
  maxResults?: number;
  customStartDate?: string;
  customEndDate?: string;
}

export interface ChatHistorySearchResult<T = ChatHistoryMessage> {
  messages: T[];
  totalCount: number;
  searchedAt: string;
  searchId: string;
}

export interface AgentConversationSummary {
  conversationId: string;
  userId: string;
  userName?: string;
  firstMessage: string;
  lastMessageTime: string;
  messageCount: number;
}

export interface UserConversationSummary {
  conversationId: string;
  userId: string;
  userName?: string;
  firstMessage: string;
  lastMessageTime: string;
  messageCount: number;
}
