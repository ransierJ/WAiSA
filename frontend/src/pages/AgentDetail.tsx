import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { agentsApi, chatApi } from '../services/api';
import type { AgentDto, AgentStatus, Message, ActivityLog } from '../types';
import { MessageList } from '../components/MessageList';
import { InputBox } from '../components/InputBox';
import { ActivityPanel } from '../components/ActivityPanel';
import { ChatHistoryPanel } from '../components/ChatHistoryPanel';

function getStatusColor(status: AgentStatus): string {
  switch (status) {
    case 'Online':
      return 'bg-green-100 text-green-800 border-green-200';
    case 'Offline':
      return 'bg-gray-100 text-gray-800 border-gray-200';
    case 'Error':
      return 'bg-red-100 text-red-800 border-red-200';
    case 'Disabled':
      return 'bg-yellow-100 text-yellow-800 border-yellow-200';
    default:
      return 'bg-gray-100 text-gray-800 border-gray-200';
  }
}

function formatBytes(mb: number): string {
  if (mb < 1024) return `${mb.toFixed(0)} MB`;
  return `${(mb / 1024).toFixed(2)} GB`;
}

function formatUptime(uptime?: string): string {
  if (!uptime) return 'N/A';

  // Parse the TimeSpan format (e.g., "3.11:47:27.7187500")
  const match = uptime.match(/^(\d+)\.(\d+):(\d+):(\d+)/);
  if (match) {
    const days = parseInt(match[1]);
    const hours = parseInt(match[2]);
    const minutes = parseInt(match[3]);

    const parts = [];
    if (days > 0) parts.push(`${days} day${days !== 1 ? 's' : ''}`);
    if (hours > 0) parts.push(`${hours} hour${hours !== 1 ? 's' : ''}`);
    if (minutes > 0 || parts.length === 0) parts.push(`${minutes} minute${minutes !== 1 ? 's' : ''}`);

    return parts.join(', ');
  }

  return uptime;
}

function formatOSVersion(osVersion?: string): string {
  if (!osVersion) return 'N/A';

  // Parse Windows version like "Microsoft Windows NT 10.0.26100.0"
  const match = osVersion.match(/(\d+)\.(\d+)\.(\d+)/);
  if (!match) return osVersion;

  const [, major, , build] = match;

  // Windows 11 builds start at 22000
  if (major === '10' && parseInt(build) >= 22000) {
    // Windows 11 version mapping
    if (parseInt(build) >= 26100) return 'Windows 11 24H2';
    if (parseInt(build) >= 22631) return 'Windows 11 23H2';
    if (parseInt(build) >= 22621) return 'Windows 11 22H2';
    if (parseInt(build) >= 22000) return 'Windows 11 21H2';
  }

  // Windows 10 version mapping
  if (major === '10') {
    if (parseInt(build) >= 19045) return 'Windows 10 22H2';
    if (parseInt(build) >= 19044) return 'Windows 10 21H2';
    if (parseInt(build) >= 19043) return 'Windows 10 21H1';
    if (parseInt(build) >= 19042) return 'Windows 10 20H2';
    if (parseInt(build) >= 19041) return 'Windows 10 2004';
    return 'Windows 10';
  }

  // Fallback for other versions
  return osVersion;
}

export function AgentDetail() {
  const { agentId } = useParams<{ agentId: string }>();
  const [agent, setAgent] = useState<AgentDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [currentActivityLogs, setCurrentActivityLogs] = useState<ActivityLog[]>([]);
  const [activeTab, setActiveTab] = useState<'chat' | 'history'>('chat');
  const [systemInfoExpanded, setSystemInfoExpanded] = useState(false);

  useEffect(() => {
    if (agentId) {
      loadAgent();
      loadChatHistory();

      // Refresh agent data every 30 seconds
      const interval = setInterval(loadAgent, 30000);
      return () => clearInterval(interval);
    }
  }, [agentId]);

  const loadAgent = async () => {
    if (!agentId) return;

    try {
      setError(null);
      const data = await agentsApi.getAgent(agentId);
      setAgent(data);
    } catch (err) {
      setError('Failed to load agent details. Please try again.');
      console.error('Error loading agent:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadChatHistory = async () => {
    if (!agentId) return;

    try {
      // Load chat history for this specific agent
      const history = await chatApi.getHistory(agentId, 50);
      setMessages(history || []);
    } catch (err) {
      console.error('Error loading chat history:', err);
    }
  };

  const handleSendMessage = async (content: string) => {
    if (!agentId || !agent) return;

    // Add user message immediately
    const userMessage: Message = {
      id: Date.now().toString(),
      role: 'user',
      content,
      timestamp: new Date(),
    };
    setMessages((prev) => [...prev, userMessage]);
    setIsStreaming(true);
    setCurrentActivityLogs([]); // Clear previous activity logs

    try {
      // Send message with agent context
      const response = await chatApi.sendMessage({
        deviceId: agentId,
        message: content,
      });

      // Store activity logs for display
      setCurrentActivityLogs(response.activityLogs || []);

      // Add assistant response
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.message,
        timestamp: new Date(),
        commands: response.executedCommands,
      };
      setMessages((prev) => [...prev, assistantMessage]);

      // Refresh agent data after command execution
      if (response.executedCommands && response.executedCommands.length > 0) {
        setTimeout(loadAgent, 2000);
      }
    } catch (err) {
      console.error('Error sending message:', err);
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: 'Sorry, I encountered an error processing your request. Please try again.',
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsStreaming(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading agent details...</p>
        </div>
      </div>
    );
  }

  if (error || !agent) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-8">
        <div className="bg-red-50 border border-red-200 text-red-800 px-6 py-4 rounded-lg">
          <h2 className="text-lg font-bold mb-2">Error</h2>
          <p>{error || 'Agent not found'}</p>
          <Link to="/agents" className="text-red-900 underline hover:text-red-700 mt-4 inline-block">
            ← Back to Agents
          </Link>
        </div>
      </div>
    );
  }

  const sysInfo = agent.lastSystemInfo;

  return (
    <div className="flex flex-col overflow-hidden h-full">
      {/* Header Section */}
      <div className="flex-shrink-0 bg-white border-b">
        <div className="max-w-7xl mx-auto px-4 py-2">
        {/* Header */}
        <div className="mb-4">
          <Link to="/agents" className="text-blue-600 hover:text-blue-800 text-sm font-medium mb-2 inline-block">
            ← Back to Agents
          </Link>
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold text-gray-900">{agent.computerName}</h1>
              <p className="text-gray-600 text-sm mt-1">Agent ID: {agent.agentId}</p>
            </div>
            <span className={`px-4 py-2 rounded-full text-sm font-medium border ${getStatusColor(agent.status)}`}>
              {agent.status}
            </span>
          </div>
        </div>

        {/* Agent Info Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-3">
        <div className="bg-white rounded-lg shadow p-3 border border-gray-200">
          <div className="text-xs font-medium text-gray-600 mb-1">Version</div>
          <div className="text-lg font-bold text-gray-900">v{agent.version}</div>
        </div>
        <div className="bg-white rounded-lg shadow p-3 border border-gray-200">
          <div className="text-xs font-medium text-gray-600 mb-1">OS</div>
          <div className="text-sm font-bold text-gray-900">{formatOSVersion(agent.osVersion)}</div>
        </div>
        <div className="bg-white rounded-lg shadow p-3 border border-gray-200">
          <div className="text-xs font-medium text-gray-600 mb-1">Installed</div>
          <div className="text-sm font-bold text-gray-900">
            {new Date(agent.installDate).toLocaleDateString()}
          </div>
        </div>
        <div className="bg-white rounded-lg shadow p-3 border border-gray-200">
          <div className="text-xs font-medium text-gray-600 mb-1">Last Heartbeat</div>
          <div className="text-sm font-bold text-gray-900">
            {agent.lastHeartbeat
              ? new Date(agent.lastHeartbeat).toLocaleString()
              : 'Never'}
          </div>
        </div>
      </div>

        {/* Compact System Information */}
        {sysInfo && (
          <div className="bg-white rounded-lg shadow border border-gray-200 mb-3">
          <div
            className="px-4 py-3 flex items-center justify-between cursor-pointer hover:bg-gray-50"
            onClick={() => setSystemInfoExpanded(!systemInfoExpanded)}
          >
            <h2 className="text-sm font-bold text-gray-900">System Metrics</h2>
            <div className="flex items-center gap-4">
              <span className="text-xs text-gray-600">
                CPU: {sysInfo.cpuUsagePercent?.toFixed(0)}% | RAM: {sysInfo.memoryUsagePercent?.toFixed(0)}%
              </span>
              <span className="text-gray-400">{systemInfoExpanded ? '▲' : '▼'}</span>
            </div>
          </div>

          {systemInfoExpanded && (
            <div className="px-4 pb-4 border-t border-gray-200 pt-4">
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {/* CPU Usage */}
            <div>
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-medium text-gray-600">CPU Usage</span>
                <span className="text-lg font-bold text-gray-900">
                  {sysInfo.cpuUsagePercent?.toFixed(1) ?? 'N/A'}%
                </span>
              </div>
              <div className="w-full bg-gray-200 rounded-full h-2">
                <div
                  className={`h-2 rounded-full transition-all duration-300 ${
                    (sysInfo.cpuUsagePercent ?? 0) > 80
                      ? 'bg-red-500'
                      : (sysInfo.cpuUsagePercent ?? 0) > 60
                      ? 'bg-yellow-500'
                      : 'bg-green-500'
                  }`}
                  style={{ width: `${Math.min(sysInfo.cpuUsagePercent ?? 0, 100)}%` }}
                ></div>
              </div>
            </div>

            {/* Memory Usage */}
            <div>
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-medium text-gray-600">Memory Usage</span>
                <span className="text-lg font-bold text-gray-900">
                  {sysInfo.memoryUsagePercent?.toFixed(1) ?? 'N/A'}%
                </span>
              </div>
              <div className="w-full bg-gray-200 rounded-full h-2">
                <div
                  className={`h-2 rounded-full transition-all duration-300 ${
                    (sysInfo.memoryUsagePercent ?? 0) > 80
                      ? 'bg-red-500'
                      : (sysInfo.memoryUsagePercent ?? 0) > 60
                      ? 'bg-yellow-500'
                      : 'bg-blue-500'
                  }`}
                  style={{ width: `${Math.min(sysInfo.memoryUsagePercent ?? 0, 100)}%` }}
                ></div>
              </div>
              <div className="mt-1 text-xs text-gray-500">
                {formatBytes(sysInfo.usedMemoryMB ?? 0)} / {formatBytes(sysInfo.totalMemoryMB ?? 0)}
              </div>
            </div>

            {/* Uptime */}
            <div>
              <div className="text-xs font-medium text-gray-600 mb-1">System Uptime</div>
              <div className="text-lg font-bold text-gray-900">{formatUptime(sysInfo.systemUptime)}</div>
            </div>

            {/* IP Address */}
            {sysInfo.ipAddress && (
              <div>
                <div className="text-xs font-medium text-gray-600 mb-1">IP Address</div>
                <div className="text-sm font-bold text-gray-900">{sysInfo.ipAddress}</div>
              </div>
            )}

            {/* Domain */}
            {sysInfo.domain && (
              <div>
                <div className="text-xs font-medium text-gray-600 mb-1">Domain</div>
                <div className="text-sm font-bold text-gray-900">{sysInfo.domain}</div>
              </div>
            )}
          </div>

          {/* Disk Information */}
          {sysInfo.disks && sysInfo.disks.length > 0 && (
            <div className="mt-4">
              <h3 className="text-sm font-bold text-gray-900 mb-2">Disk Usage</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                {sysInfo.disks.map((disk) => {
                  const percentFree = 100 - (disk.usagePercent ?? 0);
                  return (
                    <div key={disk.driveLetter} className="border border-gray-200 rounded-lg p-3">
                      <div className="flex items-center justify-between mb-1">
                        <span className="text-xs font-medium text-gray-600">Drive {disk.driveLetter}</span>
                        <span className="text-sm font-bold text-gray-900">
                          {percentFree.toFixed(0)}% Free
                        </span>
                      </div>
                      <div className="w-full bg-gray-200 rounded-full h-1.5 mb-1">
                        <div
                          className={`h-1.5 rounded-full ${
                            percentFree < 20
                              ? 'bg-red-500'
                              : percentFree < 40
                              ? 'bg-yellow-500'
                              : 'bg-green-500'
                          }`}
                          style={{ width: `${percentFree}%` }}
                        ></div>
                      </div>
                      <div className="text-xs text-gray-500">
                        {disk.freeSpaceGB?.toFixed(1) ?? 'N/A'} GB free of {disk.totalSizeGB?.toFixed(1) ?? 'N/A'} GB
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Logged In Users */}
          {sysInfo.loggedInUsers && sysInfo.loggedInUsers.length > 0 && (
            <div className="mt-3">
              <h3 className="text-sm font-bold text-gray-900 mb-2">Logged In Users</h3>
              <div className="flex flex-wrap gap-2">
                {sysInfo.loggedInUsers.map((user, idx) => (
                  <span
                    key={idx}
                    className="bg-blue-100 text-blue-800 px-2 py-1 rounded-full text-xs font-medium"
                  >
                    {user}
                  </span>
                ))}
              </div>
            </div>
          )}

          <div className="mt-3 text-xs text-gray-500 text-right">
            Last updated: {new Date(sysInfo.collectedAt).toLocaleString()}
          </div>
          </div>
          )}
          </div>
        )}
        </div>
      </div>

      {/* Agent-Specific Chat Interface */}
      <div className="flex-1 overflow-hidden">
        <div className="max-w-7xl mx-auto px-4 pb-4 h-full">
        <div className="bg-white rounded-lg shadow-lg border border-gray-200 h-full flex flex-col">
        {/* Tabs */}
        <div className="border-b border-gray-200">
          <div className="flex">
            <button
              onClick={() => setActiveTab('chat')}
              className={`flex-1 px-6 py-4 text-sm font-medium transition-colors ${
                activeTab === 'chat'
                  ? 'border-b-2 border-blue-600 text-blue-600 bg-blue-50'
                  : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
              }`}
            >
              💬 Chat
            </button>
            <button
              onClick={() => setActiveTab('history')}
              className={`flex-1 px-6 py-4 text-sm font-medium transition-colors ${
                activeTab === 'history'
                  ? 'border-b-2 border-blue-600 text-blue-600 bg-blue-50'
                  : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
              }`}
            >
              📜 History
            </button>
          </div>
        </div>

        {/* Tab Content */}
        {activeTab === 'chat' ? (
          /* Split pane: Main chat + Activity panel */
          <div className="flex flex-1 overflow-hidden">
            {/* Main chat area (70%) */}
            <div className="flex-1 flex flex-col" style={{ width: '70%' }}>
              <div className="flex-1 overflow-y-auto p-4">
                {messages.length === 0 ? (
                <div className="text-center py-12">
                  <div className="text-6xl mb-4">💬</div>
                  <h3 className="text-xl font-bold text-gray-900 mb-2">Start a Conversation</h3>
                  <p className="text-gray-600 mb-4">
                    Ask me anything about {agent.computerName} or request system operations.
                  </p>
                  <div className="text-left max-w-md mx-auto space-y-2">
                    <div className="text-sm text-gray-600">
                      <strong>Examples:</strong>
                    </div>
                    <div className="bg-gray-50 p-3 rounded-lg text-sm">
                      "What processes are using the most CPU?"
                    </div>
                    <div className="bg-gray-50 p-3 rounded-lg text-sm">
                      "Check the Windows event log for errors"
                    </div>
                    <div className="bg-gray-50 p-3 rounded-lg text-sm">
                      "Is Windows Update service running?"
                    </div>
                  </div>
                </div>
              ) : (
                <MessageList messages={messages} />
              )}
              {isStreaming && (
                <div className="flex items-center space-x-2 text-gray-500 mt-4">
                  <div className="animate-pulse">●</div>
                  <div className="animate-pulse animation-delay-200">●</div>
                  <div className="animate-pulse animation-delay-400">●</div>
                  <span className="ml-2">AI is thinking...</span>
                </div>
              )}
            </div>

            <div className="border-t border-gray-200 p-4">
              <InputBox onSendMessage={handleSendMessage} disabled={isStreaming || agent.status !== 'Online'} />
              {agent.status !== 'Online' && (
                <p className="text-sm text-yellow-600 mt-2">
                  ⚠️ Agent is offline. Messages will be queued until agent reconnects.
                </p>
              )}
            </div>
          </div>

          {/* Activity panel (30%) */}
          <div style={{ width: '30%' }}>
            <ActivityPanel
              activities={currentActivityLogs}
              isActive={isStreaming || currentActivityLogs.length > 0}
            />
            </div>
          </div>
        ) : (
          /* History Tab */
          <div className="flex-1 overflow-hidden">
            <ChatHistoryPanel targetId={agentId!} targetType="agent" />
          </div>
        )}
        </div>
        </div>
      </div>
    </div>
  );
}
