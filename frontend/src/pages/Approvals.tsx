import { useState, useEffect } from 'react';
import { agentsApi } from '../services/api';
import type { PendingApprovalDto } from '../types';
import { ApprovalModal } from '../components/ApprovalModal';

export function Approvals() {
  const [approvals, setApprovals] = useState<PendingApprovalDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedApproval, setSelectedApproval] = useState<PendingApprovalDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const loadApprovals = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await agentsApi.getPendingApprovals();
      setApprovals(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load pending approvals');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadApprovals();

    // Poll for updates every 10 seconds
    const interval = setInterval(loadApprovals, 10000);
    return () => clearInterval(interval);
  }, []);

  const handleApprovalClick = (approval: PendingApprovalDto) => {
    setSelectedApproval(approval);
    setIsModalOpen(true);
  };

  const handleModalClose = () => {
    setIsModalOpen(false);
    setSelectedApproval(null);
  };

  const handleApproved = () => {
    // Reload approvals after approval
    loadApprovals();
  };

  const handleCancelled = () => {
    // Reload approvals after cancellation
    loadApprovals();
  };

  const getTimeSince = (date: Date) => {
    const seconds = Math.floor((Date.now() - new Date(date).getTime()) / 1000);

    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    return `${Math.floor(seconds / 86400)}d ago`;
  };

  const getRiskLevel = (command: string): { label: string; color: string } => {
    const cmd = command.toLowerCase();

    // Critical risk
    if (cmd.includes('remove-') || cmd.includes('delete') || cmd.includes('format') ||
        cmd.includes('stop-computer') || cmd.includes('restart-computer')) {
      return { label: 'Critical', color: 'bg-red-100 text-red-800 border-red-200' };
    }

    // High risk
    if (cmd.includes('set-') || cmd.includes('new-') || cmd.includes('disable-') ||
        cmd.includes('enable-') || cmd.includes('install-')) {
      return { label: 'High', color: 'bg-orange-100 text-orange-800 border-orange-200' };
    }

    // Medium risk
    if (cmd.includes('restart-') || cmd.includes('stop-') || cmd.includes('start-')) {
      return { label: 'Medium', color: 'bg-yellow-100 text-yellow-800 border-yellow-200' };
    }

    return { label: 'Low', color: 'bg-blue-100 text-blue-800 border-blue-200' };
  };

  if (loading && approvals.length === 0) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center">
          <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900"></div>
          <p className="mt-2 text-gray-600">Loading pending approvals...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Pending Approvals</h1>
        <p className="mt-2 text-gray-600">
          Review and approve or cancel PowerShell commands awaiting execution
        </p>
      </div>

      {error && (
        <div className="mb-4 bg-red-50 border border-red-200 rounded-lg p-4">
          <div className="flex">
            <svg
              className="h-5 w-5 text-red-400"
              fill="currentColor"
              viewBox="0 0 20 20"
            >
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                clipRule="evenodd"
              />
            </svg>
            <p className="ml-3 text-sm text-red-800">{error}</p>
          </div>
        </div>
      )}

      {approvals.length === 0 ? (
        <div className="bg-white shadow rounded-lg p-12 text-center">
          <svg
            className="mx-auto h-12 w-12 text-gray-400"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <h3 className="mt-2 text-sm font-medium text-gray-900">No Pending Approvals</h3>
          <p className="mt-1 text-sm text-gray-500">
            All commands have been processed. New approval requests will appear here.
          </p>
        </div>
      ) : (
        <div className="bg-white shadow overflow-hidden sm:rounded-lg">
          <ul className="divide-y divide-gray-200">
            {approvals.map((approval) => {
              const risk = getRiskLevel(approval.command);
              return (
                <li
                  key={approval.commandId}
                  className="hover:bg-gray-50 transition-colors cursor-pointer"
                  onClick={() => handleApprovalClick(approval)}
                >
                  <div className="px-6 py-4">
                    <div className="flex items-center justify-between">
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center space-x-3 mb-2">
                          <h3 className="text-lg font-medium text-gray-900 truncate">
                            {approval.agentName}
                          </h3>
                          <span
                            className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${risk.color}`}
                          >
                            {risk.label} Risk
                          </span>
                          <span className="text-sm text-gray-500">
                            {getTimeSince(approval.createdAt)}
                          </span>
                        </div>

                        <div className="mb-2">
                          <div className="bg-gray-900 rounded px-3 py-2 overflow-x-auto">
                            <code className="text-sm text-green-400 font-mono">
                              {approval.command}
                            </code>
                          </div>
                        </div>

                        <div className="flex items-center space-x-4 text-sm text-gray-500">
                          <span>
                            <span className="font-medium">Context:</span> {approval.executionContext}
                          </span>
                          <span>
                            <span className="font-medium">Initiated by:</span> {approval.initiatedBy}
                          </span>
                          <span>
                            <span className="font-medium">Timeout:</span> {approval.timeoutSeconds}s
                          </span>
                        </div>
                      </div>

                      <div className="ml-4 flex-shrink-0">
                        <svg
                          className="h-6 w-6 text-gray-400"
                          fill="none"
                          viewBox="0 0 24 24"
                          stroke="currentColor"
                        >
                          <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth={2}
                            d="M9 5l7 7-7 7"
                          />
                        </svg>
                      </div>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      {selectedApproval && (
        <ApprovalModal
          approval={selectedApproval}
          isOpen={isModalOpen}
          onClose={handleModalClose}
          onApproved={handleApproved}
          onCancelled={handleCancelled}
        />
      )}

      {approvals.length > 0 && (
        <div className="mt-4 text-center text-sm text-gray-500">
          Auto-refreshing every 10 seconds • {approvals.length} pending approval{approvals.length !== 1 ? 's' : ''}
        </div>
      )}
    </div>
  );
}
