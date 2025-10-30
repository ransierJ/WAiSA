import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { agentsApi } from '../services/api';
import type { AgentDto, AgentStatus } from '../types';

function formatLastSeen(lastHeartbeat?: Date): string {
  if (!lastHeartbeat) return 'Never';

  const date = new Date(lastHeartbeat);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60000);

  if (diffMinutes < 1) return 'Just now';
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}

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

function getStatusIndicator(status: AgentStatus): string {
  switch (status) {
    case 'Online':
      return '🟢';
    case 'Offline':
      return '⚫';
    case 'Error':
      return '🔴';
    case 'Disabled':
      return '🟡';
    default:
      return '⚫';
  }
}

function formatOsVersion(osVersion?: string): string {
  if (!osVersion) return 'Unknown';

  // Extract Windows version from technical string
  // Examples: "Microsoft Windows NT 10.0.22631.0" -> "Windows 11"
  //           "Microsoft Windows NT 10.0.19045.0" -> "Windows 10"

  if (osVersion.includes('Windows NT 10.0')) {
    const buildMatch = osVersion.match(/10\.0\.(\d+)/);
    if (buildMatch) {
      const build = parseInt(buildMatch[1]);
      // Windows 11 starts at build 22000
      if (build >= 22000) {
        return 'Windows 11';
      }
      return 'Windows 10';
    }
  }

  // Try to extract just the Windows version part
  const windowsMatch = osVersion.match(/Windows\s+(\d+|NT\s+[\d.]+|[^\s]+)/i);
  if (windowsMatch) {
    return `Windows ${windowsMatch[1]}`;
  }

  // If we can't parse it, return the first 30 characters
  return osVersion.length > 30 ? osVersion.substring(0, 30) + '...' : osVersion;
}

export function AgentsList() {
  const [agents, setAgents] = useState<AgentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'online'>('all');
  const [viewMode, setViewMode] = useState<'card' | 'table'>('table');

  useEffect(() => {
    loadAgents();

    // Refresh agents every 30 seconds
    const interval = setInterval(loadAgents, 30000);
    return () => clearInterval(interval);
  }, [filter]);

  const loadAgents = async () => {
    try {
      setError(null);
      const data = filter === 'online'
        ? await agentsApi.getOnlineAgents()
        : await agentsApi.getAllAgents();
      setAgents(data);
    } catch (err) {
      setError('Failed to load agents. Please try again.');
      console.error('Error loading agents:', err);
    } finally {
      setLoading(false);
    }
  };

  const onlineCount = agents.filter(a => a.status === 'Online').length;
  const offlineCount = agents.filter(a => a.status === 'Offline').length;

  if (loading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading agents...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-2">Agent Management</h1>
        <p className="text-gray-600">Monitor and manage your Windows agents</p>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="text-sm font-medium text-gray-600 mb-1">Total Agents</div>
          <div className="text-3xl font-bold text-gray-900">{agents.length}</div>
        </div>
        <div className="bg-white rounded-lg shadow p-6 border border-green-200">
          <div className="text-sm font-medium text-gray-600 mb-1">Online</div>
          <div className="text-3xl font-bold text-green-600">{onlineCount}</div>
        </div>
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="text-sm font-medium text-gray-600 mb-1">Offline</div>
          <div className="text-3xl font-bold text-gray-600">{offlineCount}</div>
        </div>
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="text-sm font-medium text-gray-600 mb-1">Last Refresh</div>
          <div className="text-sm text-gray-900">{new Date().toLocaleTimeString()}</div>
        </div>
      </div>

      {/* Filter Tabs and View Toggle */}
      <div className="mb-6">
        <div className="border-b border-gray-200">
          <div className="flex items-center justify-between">
            <nav className="-mb-px flex space-x-8">
              <button
                onClick={() => setFilter('all')}
                className={`py-4 px-1 border-b-2 font-medium text-sm ${
                  filter === 'all'
                    ? 'border-blue-500 text-blue-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                All Agents ({agents.length})
              </button>
              <button
                onClick={() => setFilter('online')}
                className={`py-4 px-1 border-b-2 font-medium text-sm ${
                  filter === 'online'
                    ? 'border-blue-500 text-blue-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                Online Only ({onlineCount})
              </button>
            </nav>

            {/* View Mode Toggle */}
            <div className="flex items-center space-x-2">
              <button
                onClick={() => setViewMode('card')}
                className={`p-2 rounded ${
                  viewMode === 'card'
                    ? 'bg-blue-100 text-blue-600'
                    : 'text-gray-400 hover:text-gray-600'
                }`}
                title="Card View"
              >
                <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M3 4a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1H4a1 1 0 01-1-1V4zM3 12a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1H4a1 1 0 01-1-1v-4zM11 4a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1V4zM11 12a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1v-4z" />
                </svg>
              </button>
              <button
                onClick={() => setViewMode('table')}
                className={`p-2 rounded ${
                  viewMode === 'table'
                    ? 'bg-blue-100 text-blue-600'
                    : 'text-gray-400 hover:text-gray-600'
                }`}
                title="Table View"
              >
                <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M3 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm0 4a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z" clipRule="evenodd" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded-lg mb-6">
          {error}
        </div>
      )}

      {/* Agents List */}
      {agents.length === 0 ? (
        <div className="bg-white rounded-lg shadow-lg p-12 text-center">
          <div className="text-6xl mb-4">🖥️</div>
          <h2 className="text-2xl font-bold text-gray-900 mb-2">No Agents Found</h2>
          <p className="text-gray-600 mb-6">
            {filter === 'online'
              ? 'No agents are currently online.'
              : 'Install the Windows agent to get started.'}
          </p>
          {filter === 'online' && (
            <button
              onClick={() => setFilter('all')}
              className="text-blue-600 hover:text-blue-800 font-medium"
            >
              View all agents →
            </button>
          )}
        </div>
      ) : viewMode === 'card' ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {agents.map((agent) => (
            <Link
              key={agent.agentId}
              to={`/agents/${agent.agentId}`}
              className="bg-white rounded-lg shadow hover:shadow-xl transition-shadow duration-200 p-6 border border-gray-200"
            >
              <div className="flex items-start justify-between mb-4">
                <div className="flex-1">
                  <h3 className="text-lg font-bold text-gray-900 mb-1">
                    {agent.computerName}
                  </h3>
                  <p className="text-sm text-gray-500">v{agent.version}</p>
                </div>
                <span className="text-2xl">{getStatusIndicator(agent.status)}</span>
              </div>

              <div className="space-y-2 mb-4">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-gray-600">Status:</span>
                  <span className={`px-2 py-1 rounded-full text-xs font-medium border ${getStatusColor(agent.status)}`}>
                    {agent.status}
                  </span>
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-gray-600">Last seen:</span>
                  <span className="text-gray-900 font-medium">
                    {formatLastSeen(agent.lastHeartbeat)}
                  </span>
                </div>
                {agent.osVersion && (
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-600">OS:</span>
                    <span className="text-gray-900 font-medium truncate ml-2">
                      {formatOsVersion(agent.osVersion)}
                    </span>
                  </div>
                )}
              </div>

              {agent.lastSystemInfo && (
                <div className="border-t border-gray-200 pt-4">
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <div className="text-gray-600 mb-1">CPU</div>
                      <div className="text-lg font-bold text-gray-900">
                        {agent.lastSystemInfo.cpuUsagePercent?.toFixed(1) ?? 'N/A'}%
                      </div>
                    </div>
                    <div>
                      <div className="text-gray-600 mb-1">Memory</div>
                      <div className="text-lg font-bold text-gray-900">
                        {agent.lastSystemInfo.memoryUsagePercent?.toFixed(1) ?? 'N/A'}%
                      </div>
                    </div>
                  </div>
                </div>
              )}

              <div className="mt-4 text-center text-sm text-blue-600 font-medium">
                View Details →
              </div>
            </Link>
          ))}
        </div>
      ) : (
        <div className="bg-white shadow-lg rounded-lg border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    Computer Name
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    Version
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    OS
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    Last Seen
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    CPU
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap">
                    Memory
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap sticky right-0 bg-gray-50">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {agents.map((agent) => (
                  <tr key={agent.agentId} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="flex items-center">
                        <span className="text-xl mr-2">{getStatusIndicator(agent.status)}</span>
                        <span className={`px-2 py-1 rounded-full text-xs font-medium border ${getStatusColor(agent.status)}`}>
                          {agent.status}
                        </span>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm font-medium text-gray-900">{agent.computerName}</div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm text-gray-500">v{agent.version}</div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm text-gray-900">
                        {formatOsVersion(agent.osVersion)}
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm text-gray-900">{formatLastSeen(agent.lastHeartbeat)}</div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm font-medium text-gray-900">
                        {agent.lastSystemInfo?.cpuUsagePercent?.toFixed(1) ?? 'N/A'}%
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm font-medium text-gray-900">
                        {agent.lastSystemInfo?.memoryUsagePercent?.toFixed(1) ?? 'N/A'}%
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium sticky right-0 bg-white hover:bg-gray-50">
                      <Link
                        to={`/agents/${agent.agentId}`}
                        className="text-blue-600 hover:text-blue-900"
                      >
                        View Details →
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Refresh Button */}
      <div className="mt-8 text-center">
        <button
          onClick={loadAgents}
          disabled={loading}
          className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {loading ? 'Refreshing...' : 'Refresh Now'}
        </button>
      </div>
    </div>
  );
}
