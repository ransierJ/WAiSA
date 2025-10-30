import { useState } from 'react';
import type { PendingApprovalDto } from '../types';
import { agentsApi } from '../services/api';

interface ApprovalModalProps {
  approval: PendingApprovalDto;
  isOpen: boolean;
  onClose: () => void;
  onApproved: () => void;
  onCancelled: () => void;
}

export function ApprovalModal({ approval, isOpen, onClose, onApproved, onCancelled }: ApprovalModalProps) {
  const [approverName, setApproverName] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleApprove = async () => {
    if (!approverName.trim()) {
      setError('Please enter your name');
      return;
    }

    setIsProcessing(true);
    setError(null);

    try {
      await agentsApi.approveCommand(approval.commandId, approverName);
      onApproved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to approve command');
    } finally {
      setIsProcessing(false);
    }
  };

  const handleCancel = async () => {
    setIsProcessing(true);
    setError(null);

    try {
      await agentsApi.cancelCommand(approval.commandId);
      onCancelled();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to cancel command');
    } finally {
      setIsProcessing(false);
    }
  };

  const getTimeSince = (date: Date) => {
    const seconds = Math.floor((Date.now() - new Date(date).getTime()) / 1000);

    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    return `${Math.floor(seconds / 86400)}d ago`;
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div className="flex items-center justify-center min-h-screen px-4 pt-4 pb-20 text-center sm:block sm:p-0">
        {/* Background overlay */}
        <div
          className="fixed inset-0 transition-opacity bg-gray-500 bg-opacity-75"
          onClick={onClose}
        />

        {/* Modal panel */}
        <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full sm:p-6">
          <div>
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center">
                <div className="mx-auto flex-shrink-0 flex items-center justify-center h-12 w-12 rounded-full bg-yellow-100 sm:mx-0 sm:h-10 sm:w-10">
                  <svg
                    className="h-6 w-6 text-yellow-600"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                    />
                  </svg>
                </div>
                <h3 className="ml-3 text-lg leading-6 font-medium text-gray-900">
                  Command Approval Required
                </h3>
              </div>
              <button
                onClick={onClose}
                className="text-gray-400 hover:text-gray-500"
                disabled={isProcessing}
              >
                <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>

            <div className="mt-3 space-y-4">
              {/* Agent Info */}
              <div className="bg-gray-50 rounded-lg p-3">
                <div className="text-sm text-gray-500">Target Agent</div>
                <div className="text-base font-medium text-gray-900">{approval.agentName}</div>
              </div>

              {/* Command Info */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  PowerShell Command
                </label>
                <div className="bg-gray-900 rounded-lg p-4 overflow-x-auto">
                  <code className="text-sm text-green-400 font-mono">{approval.command}</code>
                </div>
              </div>

              {/* Metadata */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <div className="text-sm text-gray-500">Initiated By</div>
                  <div className="text-sm font-medium text-gray-900">{approval.initiatedBy}</div>
                </div>
                <div>
                  <div className="text-sm text-gray-500">Requested</div>
                  <div className="text-sm font-medium text-gray-900">
                    {getTimeSince(approval.createdAt)}
                  </div>
                </div>
                <div>
                  <div className="text-sm text-gray-500">Context</div>
                  <div className="text-sm font-medium text-gray-900">{approval.executionContext}</div>
                </div>
                <div>
                  <div className="text-sm text-gray-500">Timeout</div>
                  <div className="text-sm font-medium text-gray-900">
                    {approval.timeoutSeconds}s
                  </div>
                </div>
              </div>

              {/* Warning */}
              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                <div className="flex">
                  <div className="flex-shrink-0">
                    <svg
                      className="h-5 w-5 text-yellow-400"
                      fill="currentColor"
                      viewBox="0 0 20 20"
                    >
                      <path
                        fillRule="evenodd"
                        d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
                        clipRule="evenodd"
                      />
                    </svg>
                  </div>
                  <div className="ml-3">
                    <h3 className="text-sm font-medium text-yellow-800">
                      Review Carefully Before Approving
                    </h3>
                    <div className="mt-2 text-sm text-yellow-700">
                      <p>
                        This command will be executed on <strong>{approval.agentName}</strong>.
                        Ensure you understand what it does and approve only if necessary.
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              {/* Approver Name Input */}
              <div>
                <label htmlFor="approverName" className="block text-sm font-medium text-gray-700 mb-2">
                  Your Name (required for approval)
                </label>
                <input
                  type="text"
                  id="approverName"
                  value={approverName}
                  onChange={(e) => setApproverName(e.target.value)}
                  placeholder="Enter your name"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  disabled={isProcessing}
                />
              </div>

              {/* Error Message */}
              {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg p-3">
                  <p className="text-sm text-red-800">{error}</p>
                </div>
              )}
            </div>
          </div>

          {/* Action Buttons */}
          <div className="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
            <button
              type="button"
              onClick={handleApprove}
              disabled={isProcessing || !approverName.trim()}
              className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:col-start-2 sm:text-sm disabled:bg-gray-300 disabled:cursor-not-allowed"
            >
              {isProcessing ? 'Processing...' : '✓ Approve & Execute'}
            </button>
            <button
              type="button"
              onClick={handleCancel}
              disabled={isProcessing}
              className="mt-3 w-full inline-flex justify-center rounded-md border border-red-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 sm:mt-0 sm:col-start-1 sm:text-sm disabled:bg-gray-100 disabled:cursor-not-allowed"
            >
              {isProcessing ? 'Processing...' : '✗ Cancel Command'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
