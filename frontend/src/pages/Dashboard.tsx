import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { agentsApi } from '../services/api';
import type { AgentDto, AgentStatus } from '../types';

interface DashboardStats {
  totalAgents: number;
  onlineAgents: number;
  offlineAgents: number;
  errorAgents: number;
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

export const Dashboard = () => {
  const [agents, setAgents] = useState<AgentDto[]>([]);
  const [stats, setStats] = useState<DashboardStats>({
    totalAgents: 0,
    onlineAgents: 0,
    offlineAgents: 0,
    errorAgents: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadData = async () => {
      try {
        setLoading(true);
        const agentsList = await agentsApi.getAllAgents();
        setAgents(agentsList);

        // Calculate stats
        const newStats: DashboardStats = {
          totalAgents: agentsList.length,
          onlineAgents: agentsList.filter((a: AgentDto) => a.status === 'Online').length,
          offlineAgents: agentsList.filter((a: AgentDto) => a.status === 'Offline').length,
          errorAgents: agentsList.filter((a: AgentDto) => a.status === 'Error').length,
        };
        setStats(newStats);
      } catch (err) {
        console.error('Error loading dashboard data:', err);
        setError('Failed to load dashboard data. Please try again.');
      } finally {
        setLoading(false);
      }
    };

    loadData();
    const interval = setInterval(loadData, 30000); // Refresh every 30 seconds
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-800">
          {error}
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
        <p className="mt-2 text-sm text-gray-600">
          Welcome to WAiSA - Windows AI System Administrator
        </p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        {/* Total Agents */}
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="flex-shrink-0">
              <div className="text-4xl">💻</div>
            </div>
            <div className="ml-4 flex-1">
              <p className="text-sm font-medium text-gray-600">Total Agents</p>
              <p className="text-2xl font-bold text-gray-900">{stats.totalAgents}</p>
            </div>
          </div>
        </div>

        {/* Online Agents */}
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="flex-shrink-0">
              <div className="text-4xl">🟢</div>
            </div>
            <div className="ml-4 flex-1">
              <p className="text-sm font-medium text-gray-600">Online</p>
              <p className="text-2xl font-bold text-green-600">{stats.onlineAgents}</p>
            </div>
          </div>
        </div>

        {/* Offline Agents */}
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="flex-shrink-0">
              <div className="text-4xl">⚫</div>
            </div>
            <div className="ml-4 flex-1">
              <p className="text-sm font-medium text-gray-600">Offline</p>
              <p className="text-2xl font-bold text-gray-600">{stats.offlineAgents}</p>
            </div>
          </div>
        </div>

        {/* Error Agents */}
        <div className="bg-white rounded-lg shadow p-6 border border-gray-200">
          <div className="flex items-center">
            <div className="flex-shrink-0">
              <div className="text-4xl">🔴</div>
            </div>
            <div className="ml-4 flex-1">
              <p className="text-sm font-medium text-gray-600">Errors</p>
              <p className="text-2xl font-bold text-red-600">{stats.errorAgents}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Quick Actions */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <Link
          to="/chat"
          className="bg-blue-500 hover:bg-blue-600 text-white rounded-lg shadow-lg p-6 transition-colors"
        >
          <div className="text-3xl mb-3">💬</div>
          <h3 className="text-lg font-semibold mb-2">Chat with WAiSA</h3>
          <p className="text-sm text-blue-100">
            Interact with your AI assistant to manage Windows systems
          </p>
        </Link>

        <Link
          to="/agents"
          className="bg-green-500 hover:bg-green-600 text-white rounded-lg shadow-lg p-6 transition-colors"
        >
          <div className="text-3xl mb-3">🖥️</div>
          <h3 className="text-lg font-semibold mb-2">Manage Agents</h3>
          <p className="text-sm text-green-100">
            View and manage all connected Windows agents
          </p>
        </Link>

        <Link
          to="/knowledge"
          className="bg-purple-500 hover:bg-purple-600 text-white rounded-lg shadow-lg p-6 transition-colors"
        >
          <div className="text-3xl mb-3">📚</div>
          <h3 className="text-lg font-semibold mb-2">Knowledge Base</h3>
          <p className="text-sm text-purple-100">
            Browse and manage system documentation
          </p>
        </Link>
      </div>

      {/* Recent Agents */}
      <div className="bg-white rounded-lg shadow border border-gray-200">
        <div className="px-6 py-4 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-semibold text-gray-900">Recent Agents</h2>
            <Link
              to="/agents"
              className="text-sm text-blue-600 hover:text-blue-700 font-medium"
            >
              View All →
            </Link>
          </div>
        </div>
        <div className="divide-y divide-gray-200">
          {agents.length === 0 ? (
            <div className="px-6 py-8 text-center">
              <div className="text-4xl mb-3">🤖</div>
              <p className="text-gray-500 mb-2">No agents registered yet</p>
              <p className="text-sm text-gray-400">
                Install and register an agent to get started
              </p>
            </div>
          ) : (
            agents.slice(0, 5).map((agent) => (
              <Link
                key={agent.agentId}
                to={`/agents/${agent.agentId}`}
                className="block px-6 py-4 hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-center justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-3">
                      <h3 className="text-sm font-medium text-gray-900">
                        {agent.computerName || agent.agentId}
                      </h3>
                      <span
                        className={`px-2 py-1 text-xs font-medium rounded-full border ${getStatusColor(
                          agent.status
                        )}`}
                      >
                        {agent.status}
                      </span>
                    </div>
                    {agent.osVersion && (
                      <p className="text-xs text-gray-500 mt-1">
                        {agent.osVersion}
                      </p>
                    )}
                  </div>
                  <div className="text-xs text-gray-400">
                    {agent.lastHeartbeat
                      ? new Date(agent.lastHeartbeat).toLocaleString()
                      : 'Never'}
                  </div>
                </div>
              </Link>
            ))
          )}
        </div>
      </div>

      {/* System Status */}
      <div className="mt-8 bg-white rounded-lg shadow border border-gray-200 p-6">
        <h2 className="text-xl font-semibold text-gray-900 mb-4">System Status</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="flex items-center gap-3 p-3 bg-green-50 rounded-lg border border-green-200">
            <div className="text-2xl">✅</div>
            <div>
              <p className="text-sm font-medium text-green-900">API Service</p>
              <p className="text-xs text-green-700">Operational</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-3 bg-green-50 rounded-lg border border-green-200">
            <div className="text-2xl">✅</div>
            <div>
              <p className="text-sm font-medium text-green-900">AI Service</p>
              <p className="text-xs text-green-700">Operational</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
