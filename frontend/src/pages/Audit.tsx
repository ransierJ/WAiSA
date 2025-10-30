import { useState, useEffect } from 'react';
import { agentsApi } from '../services/api';
import { chatHistoryApi } from '../services/chatHistoryApi';
import type {
  AgentDto,
  AgentConversationSummary,
  AgentChatMessage,
  DateRangeFilter,
  ChatHistoryMessage
} from '../types';

export function Audit() {
  const [agents, setAgents] = useState<AgentDto[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string>('');
  const [conversations, setConversations] = useState<AgentConversationSummary[]>([]);
  const [selectedConversation, setSelectedConversation] = useState<string | null>(null);
  const [conversationMessages, setConversationMessages] = useState<AgentChatMessage[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [dateRange, setDateRange] = useState<DateRangeFilter>(5); // AllTime = 5

  useEffect(() => {
    loadAgents();
  }, []);

  useEffect(() => {
    if (selectedAgentId) {
      loadConversations();
    }
  }, [selectedAgentId]);

  const loadAgents = async () => {
    try {
      const data = await agentsApi.getAllAgents();
      setAgents(data);
      if (data.length > 0) {
        setSelectedAgentId(data[0].agentId);
      }
    } catch (err) {
      console.error('Error loading agents:', err);
    }
  };

  const loadConversations = async () => {
    if (!selectedAgentId) return;

    setLoading(true);
    try {
      // If no filters, get all conversations
      if (!searchTerm && dateRange === 5) {
        const result = await chatHistoryApi.getAgentConversations(selectedAgentId, 100);
        setConversations(result);
      } else {
        // Use search API with filters
        const searchResult = await chatHistoryApi.searchAgentChat(selectedAgentId, {
          dateRange,
          searchTerm: searchTerm || undefined,
          maxResults: 100
        });

        // Group search results by conversation
        const conversationMap = new Map<string, ChatHistoryMessage[]>();
        searchResult.messages.forEach(msg => {
          if (!conversationMap.has(msg.conversationId)) {
            conversationMap.set(msg.conversationId, []);
          }
          conversationMap.get(msg.conversationId)!.push(msg);
        });

        // Build conversation summaries
        const summaries = Array.from(conversationMap.entries()).map(([convId, messages]) => {
          const sortedMessages = messages.sort((a, b) =>
            new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
          );
          const firstUserMessage = sortedMessages.find(m => m.role === 'user');

          return {
            conversationId: convId,
            userId: messages[0].userId || '',
            userName: messages[0].userName,
            firstMessage: firstUserMessage?.content || 'No user message',
            lastMessageTime: messages.reduce((latest, msg) =>
              new Date(msg.timestamp) > new Date(latest) ? msg.timestamp : latest,
              messages[0].timestamp
            ),
            messageCount: messages.length
          };
        }).sort((a, b) =>
          new Date(b.lastMessageTime).getTime() - new Date(a.lastMessageTime).getTime()
        );

        setConversations(summaries);
      }
    } catch (err) {
      console.error('Error loading conversations:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    loadConversations();
  };

  const handleClearFilters = () => {
    setSearchTerm('');
    setDateRange(5); // AllTime
  };

  useEffect(() => {
    loadConversations();
  }, [dateRange]);

  const loadConversationMessages = async (conversationId: string) => {
    if (selectedConversation === conversationId) {
      // Collapse if already selected
      setSelectedConversation(null);
      setConversationMessages([]);
      return;
    }

    setLoadingMessages(true);
    setSelectedConversation(conversationId);

    try {
      const messages = await chatHistoryApi.getAgentConversation(selectedAgentId, conversationId);
      setConversationMessages(messages);
    } catch (err) {
      console.error('Error loading conversation messages:', err);
    } finally {
      setLoadingMessages(false);
    }
  };

  const formatTimestamp = (timestamp: string) => {
    return new Date(timestamp).toLocaleString();
  };

  const truncate = (text: string, maxLength: number) => {
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength) + '...';
  };

  const totalMessages = conversations.reduce((sum, conv) => sum + conv.messageCount, 0);

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Audit & History</h1>
        <p className="text-gray-600 mt-1">
          View all chat sessions and interactions across agents
        </p>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="text-4xl mr-4">👥</div>
            <div>
              <p className="text-sm font-medium text-gray-600">Total Agents</p>
              <p className="text-2xl font-bold text-gray-900">{agents.length}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="text-4xl mr-4">📋</div>
            <div>
              <p className="text-sm font-medium text-gray-600">Total Sessions</p>
              <p className="text-2xl font-bold text-gray-900">{conversations.length}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="text-4xl mr-4">💬</div>
            <div>
              <p className="text-sm font-medium text-gray-600">Total Messages</p>
              <p className="text-2xl font-bold text-gray-900">{totalMessages}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Agent Selection and Search Filters */}
      <div className="bg-white rounded-lg shadow border border-gray-200 p-6 mb-6">
        <h2 className="text-lg font-bold text-gray-900 mb-4">Filters</h2>
        <div className="grid grid-cols-1 md:grid-cols-12 gap-4">
          <div className="md:col-span-3">
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Select Agent
            </label>
            <select
              value={selectedAgentId}
              onChange={(e) => setSelectedAgentId(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {agents.map((agent) => (
                <option key={agent.agentId} value={agent.agentId}>
                  {agent.computerName} ({agent.status})
                </option>
              ))}
            </select>
          </div>
          <div className="md:col-span-4">
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Search Messages
            </label>
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              placeholder="Search by message content..."
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Date Range
            </label>
            <select
              value={dateRange}
              onChange={(e) => setDateRange(Number(e.target.value) as DateRangeFilter)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value={5}>All Time</option>
              <option value={0}>Last 24 Hours</option>
              <option value={1}>Last 7 Days</option>
              <option value={2}>Last 30 Days</option>
              <option value={3}>Last 90 Days</option>
            </select>
          </div>
          <div className="md:col-span-3 flex items-end gap-2">
            <button
              onClick={handleSearch}
              className="flex-1 px-4 py-2 font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              Search
            </button>
            <button
              onClick={handleClearFilters}
              className="flex-1 px-4 py-2 font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              Clear
            </button>
          </div>
        </div>
      </div>

      {/* Sessions Table */}
      <div className="bg-white rounded-lg shadow border border-gray-200">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-bold text-gray-900">
            Chat Sessions
            {conversations.length > 0 && (
              <span className="ml-2 text-sm font-normal text-gray-600">
                ({conversations.length} sessions)
              </span>
            )}
          </h2>
          <p className="text-sm text-gray-600 mt-1">Click a session to view the full conversation</p>
        </div>

        <div className="overflow-x-auto">
          {loading ? (
            <div className="text-center py-12">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
              <p className="mt-4 text-gray-600">Loading sessions...</p>
            </div>
          ) : conversations.length === 0 ? (
            <div className="text-center py-12">
              <div className="text-6xl mb-4">📭</div>
              <h3 className="text-xl font-medium text-gray-900 mb-2">No Sessions Found</h3>
              <p className="text-gray-600">No chat history available for the selected agent</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-200">
              {/* Table Header */}
              <div className="bg-gray-50 px-6 py-3 grid grid-cols-12 gap-4 text-xs font-semibold text-gray-700 uppercase">
                <div className="col-span-2">Session ID</div>
                <div className="col-span-2">User</div>
                <div className="col-span-2">Date / Time</div>
                <div className="col-span-5">First Prompt</div>
                <div className="col-span-1 text-right">Messages</div>
              </div>

              {/* Table Rows */}
              {conversations.map((conv) => (
                <div key={conv.conversationId}>
                  {/* Session Row */}
                  <div
                    onClick={() => loadConversationMessages(conv.conversationId)}
                    className={`px-6 py-4 grid grid-cols-12 gap-4 text-sm cursor-pointer transition-colors ${
                      selectedConversation === conv.conversationId
                        ? 'bg-blue-50 border-l-4 border-blue-600'
                        : 'hover:bg-gray-50'
                    }`}
                  >
                    <div className="col-span-2 font-mono text-xs text-gray-600">
                      {conv.conversationId.substring(0, 12)}...
                    </div>
                    <div className="col-span-2 text-gray-900">
                      {conv.userName || conv.userId.substring(0, 20)}
                    </div>
                    <div className="col-span-2 text-gray-600">
                      {formatTimestamp(conv.lastMessageTime)}
                    </div>
                    <div className="col-span-5 text-gray-900">
                      {truncate(conv.firstMessage, 80)}
                    </div>
                    <div className="col-span-1 text-right">
                      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                        {conv.messageCount}
                      </span>
                    </div>
                  </div>

                  {/* Expanded Conversation */}
                  {selectedConversation === conv.conversationId && (
                    <div className="bg-gray-50 px-6 py-4 border-t border-gray-200">
                      {loadingMessages ? (
                        <div className="text-center py-4">
                          <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-600 mx-auto"></div>
                          <p className="mt-2 text-xs text-gray-600">Loading messages...</p>
                        </div>
                      ) : (
                        <div className="space-y-3 max-h-96 overflow-y-auto">
                          {conversationMessages.map((message) => (
                            <div
                              key={message.id}
                              className={`p-3 rounded-lg ${
                                message.role === 'user'
                                  ? 'bg-blue-100 border border-blue-200'
                                  : 'bg-white border border-gray-200'
                              }`}
                            >
                              <div className="flex items-center justify-between mb-1">
                                <div className="flex items-center gap-3">
                                  <span className="text-xs font-semibold text-gray-700 uppercase">
                                    {message.role}
                                  </span>
                                  <span className="text-xs text-gray-600">
                                    User: {message.userName || message.userId.substring(0, 20)}
                                  </span>
                                </div>
                                <span className="text-xs text-gray-500">
                                  {formatTimestamp(message.timestamp)}
                                </span>
                              </div>
                              <p className="text-sm text-gray-900 whitespace-pre-wrap">
                                {message.content}
                              </p>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
