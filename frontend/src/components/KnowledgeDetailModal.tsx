import { useState, useEffect, Fragment } from 'react';
import { Dialog, Transition } from '@headlessui/react';
import type { KnowledgeBaseEntry } from '../types';
import { knowledgeBaseApi } from '../services/api';

interface KnowledgeDetailModalProps {
  entry: KnowledgeBaseEntry;
  isOpen: boolean;
  onClose: () => void;
  onDeleted: () => void;
}

export function KnowledgeDetailModal({ entry, isOpen, onClose, onDeleted }: KnowledgeDetailModalProps) {
  const [isDeleting, setIsDeleting] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  useEffect(() => {
    // Reset state when modal opens/closes
    if (!isOpen) {
      setShowDeleteConfirm(false);
      setDeleteError(null);
    }
  }, [isOpen]);

  const handleDelete = async () => {
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await knowledgeBaseApi.delete(entry.id);
      onDeleted();
      onClose();
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : 'Failed to delete knowledge entry');
    } finally {
      setIsDeleting(false);
    }
  };

  const getSourceBadgeColor = (source: string): string => {
    switch (source) {
      case 'powershell-documentation':
        return 'bg-blue-100 text-blue-800 border-blue-200';
      case 'best-practices':
        return 'bg-green-100 text-green-800 border-green-200';
      case 'troubleshooting':
        return 'bg-yellow-100 text-yellow-800 border-yellow-200';
      case 'user-interaction':
        return 'bg-purple-100 text-purple-800 border-purple-200';
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200';
    }
  };

  const formatDate = (date: Date): string => {
    return new Date(date).toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <Transition appear show={isOpen} as={Fragment}>
      <Dialog as="div" className="relative z-50" onClose={onClose}>
        <Transition.Child
          as={Fragment}
          enter="ease-out duration-300"
          enterFrom="opacity-0"
          enterTo="opacity-100"
          leave="ease-in duration-200"
          leaveFrom="opacity-100"
          leaveTo="opacity-0"
        >
          <div className="fixed inset-0 bg-black bg-opacity-25" />
        </Transition.Child>

        <div className="fixed inset-0 overflow-y-auto">
          <div className="flex min-h-full items-center justify-center p-4 text-center">
            <Transition.Child
              as={Fragment}
              enter="ease-out duration-300"
              enterFrom="opacity-0 scale-95"
              enterTo="opacity-100 scale-100"
              leave="ease-in duration-200"
              leaveFrom="opacity-100 scale-100"
              leaveTo="opacity-0 scale-95"
            >
              <Dialog.Panel className="w-full max-w-4xl transform overflow-hidden rounded-2xl bg-white p-6 text-left align-middle shadow-xl transition-all">
                {/* Header */}
                <div className="flex items-start justify-between mb-6">
                  <div className="flex-1">
                    <Dialog.Title as="h3" className="text-2xl font-bold text-gray-900 mb-2">
                      {entry.title}
                    </Dialog.Title>
                    <div className="flex flex-wrap gap-2">
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${getSourceBadgeColor(
                          entry.source
                        )}`}
                      >
                        {entry.source}
                      </span>
                      {entry.tags.map((tag) => (
                        <span
                          key={tag}
                          className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800 border border-gray-200"
                        >
                          {tag}
                        </span>
                      ))}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={onClose}
                    className="ml-4 text-gray-400 hover:text-gray-500 focus:outline-none"
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

                {/* Usage Statistics */}
                <div className="mb-6 grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div className="bg-blue-50 rounded-lg p-4 border border-blue-200">
                    <div className="text-sm text-blue-600 font-medium">Usage Count</div>
                    <div className="text-2xl font-bold text-blue-900">{entry.usageCount}</div>
                  </div>
                  {entry.averageRating && (
                    <div className="bg-yellow-50 rounded-lg p-4 border border-yellow-200">
                      <div className="text-sm text-yellow-600 font-medium">Average Rating</div>
                      <div className="text-2xl font-bold text-yellow-900">
                        ⭐ {entry.averageRating?.toFixed(1) ?? 'N/A'}
                      </div>
                    </div>
                  )}
                  {entry.lastUsedAt && (
                    <div className="bg-green-50 rounded-lg p-4 border border-green-200">
                      <div className="text-sm text-green-600 font-medium">Last Used</div>
                      <div className="text-sm font-semibold text-green-900">
                        {formatDate(entry.lastUsedAt)}
                      </div>
                    </div>
                  )}
                </div>

                {/* Content */}
                <div className="mb-6">
                  <h4 className="text-sm font-semibold text-gray-700 uppercase tracking-wide mb-2">
                    Content
                  </h4>
                  <div className="bg-gray-50 rounded-lg p-4 border border-gray-200">
                    <div className="prose prose-sm max-w-none text-gray-700 whitespace-pre-wrap">
                      {entry.content}
                    </div>
                  </div>
                </div>

                {/* Metadata */}
                <div className="mb-6 grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="bg-white rounded-lg p-4 border border-gray-200">
                    <div className="text-sm text-gray-500 mb-1">Created At</div>
                    <div className="text-sm font-medium text-gray-900">{formatDate(entry.createdAt)}</div>
                  </div>
                  <div className="bg-white rounded-lg p-4 border border-gray-200">
                    <div className="text-sm text-gray-500 mb-1">Last Updated</div>
                    <div className="text-sm font-medium text-gray-900">{formatDate(entry.updatedAt)}</div>
                  </div>
                  {entry.sourceDeviceId && (
                    <div className="bg-white rounded-lg p-4 border border-gray-200 sm:col-span-2">
                      <div className="text-sm text-gray-500 mb-1">Source Device ID</div>
                      <div className="text-sm font-mono text-gray-900">{entry.sourceDeviceId}</div>
                    </div>
                  )}
                </div>

                {/* Delete Error Message */}
                {deleteError && (
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
                      <p className="ml-3 text-sm text-red-800">{deleteError}</p>
                    </div>
                  </div>
                )}

                {/* Delete Confirmation */}
                {showDeleteConfirm && (
                  <div className="mb-4 bg-red-50 border border-red-300 rounded-lg p-4">
                    <div className="flex items-start">
                      <svg
                        className="h-6 w-6 text-red-600 mt-0.5"
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
                      <div className="ml-3 flex-1">
                        <h3 className="text-sm font-medium text-red-800">Confirm Deletion</h3>
                        <p className="mt-2 text-sm text-red-700">
                          Are you sure you want to delete this knowledge entry? This action cannot be
                          undone.
                        </p>
                        <div className="mt-4 flex space-x-3">
                          <button
                            type="button"
                            onClick={handleDelete}
                            disabled={isDeleting}
                            className="inline-flex items-center px-3 py-2 border border-transparent text-sm leading-4 font-medium rounded-md text-white bg-red-600 hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 disabled:bg-red-400 disabled:cursor-not-allowed"
                          >
                            {isDeleting ? (
                              <>
                                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                                Deleting...
                              </>
                            ) : (
                              'Yes, Delete'
                            )}
                          </button>
                          <button
                            type="button"
                            onClick={() => setShowDeleteConfirm(false)}
                            disabled={isDeleting}
                            className="inline-flex items-center px-3 py-2 border border-gray-300 text-sm leading-4 font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:bg-gray-100 disabled:cursor-not-allowed"
                          >
                            Cancel
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {/* Action Buttons */}
                <div className="flex justify-end space-x-3">
                  <button
                    type="button"
                    onClick={() => setShowDeleteConfirm(true)}
                    disabled={isDeleting || showDeleteConfirm}
                    className="inline-flex items-center px-4 py-2 border border-red-300 text-sm font-medium rounded-md text-red-700 bg-white hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 disabled:bg-gray-100 disabled:text-gray-400 disabled:border-gray-200 disabled:cursor-not-allowed"
                  >
                    <svg
                      className="h-5 w-5 mr-2"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                      />
                    </svg>
                    Delete Entry
                  </button>
                  <button
                    type="button"
                    onClick={onClose}
                    disabled={isDeleting}
                    className="inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:bg-gray-100 disabled:cursor-not-allowed"
                  >
                    Close
                  </button>
                </div>
              </Dialog.Panel>
            </Transition.Child>
          </div>
        </div>
      </Dialog>
    </Transition>
  );
}
