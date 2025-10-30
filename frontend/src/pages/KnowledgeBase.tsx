import { useState, useEffect } from 'react';
import { knowledgeBaseApi } from '../services/api';
import type { KnowledgeBaseEntry } from '../types';
import { KnowledgeDetailModal } from '../components/KnowledgeDetailModal';
import { AddKnowledgeModal } from '../components/AddKnowledgeModal';

export function KnowledgeBase() {
  const [entries, setEntries] = useState<KnowledgeBaseEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedEntry, setSelectedEntry] = useState<KnowledgeBaseEntry | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);

  const loadEntries = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await knowledgeBaseApi.getAll(0, 100);
      setEntries(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load knowledge base');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = async () => {
    if (!searchQuery.trim()) {
      loadEntries();
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const results = await knowledgeBaseApi.search(searchQuery);
      setEntries(results);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setLoading(false);
    }
  };

  const handleEntryClick = (entry: KnowledgeBaseEntry) => {
    setSelectedEntry(entry);
    setIsDetailModalOpen(true);
  };

  const handleAddEntry = () => {
    setIsAddModalOpen(true);
  };

  const handleEntrySaved = () => {
    loadEntries();
  };

  const handleEntryDeleted = () => {
    setIsDetailModalOpen(false);
    setSelectedEntry(null);
    loadEntries();
  };

  useEffect(() => {
    loadEntries();
  }, []);

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
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  if (loading && entries.length === 0) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center">
          <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900"></div>
          <p className="mt-2 text-gray-600">Loading knowledge base...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Knowledge Base</h1>
            <p className="mt-2 text-gray-600">
              PowerShell documentation, best practices, and troubleshooting guides
            </p>
          </div>
          <button
            onClick={handleAddEntry}
            className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
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
                d="M12 4v16m8-8H4"
              />
            </svg>
            Add Entry
          </button>
        </div>
      </div>

      {/* Error Message */}
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

      {/* Search Bar */}
      <div className="mb-6">
        <div className="flex space-x-2">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
            placeholder="Search knowledge base..."
            className="flex-1 px-4 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button
            onClick={handleSearch}
            className="px-6 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
          >
            Search
          </button>
          {searchQuery && (
            <button
              onClick={() => {
                setSearchQuery('');
                loadEntries();
              }}
              className="px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
            >
              Clear
            </button>
          )}
        </div>
      </div>

      {/* Stats */}
      <div className="mb-6 grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white rounded-lg shadow p-4">
          <div className="text-sm text-gray-500">Total Entries</div>
          <div className="text-2xl font-bold text-gray-900">{entries.length}</div>
        </div>
        <div className="bg-white rounded-lg shadow p-4">
          <div className="text-sm text-gray-500">Total Usage</div>
          <div className="text-2xl font-bold text-gray-900">
            {entries.reduce((sum, e) => sum + e.usageCount, 0)}
          </div>
        </div>
        <div className="bg-white rounded-lg shadow p-4">
          <div className="text-sm text-gray-500">Avg Rating</div>
          <div className="text-2xl font-bold text-gray-900">
            {entries.filter((e) => e.averageRating).length > 0
              ? (
                  entries.reduce((sum, e) => sum + (e.averageRating || 0), 0) /
                  entries.filter((e) => e.averageRating).length
                ).toFixed(1)
              : 'N/A'}
          </div>
        </div>
      </div>

      {/* Entries List */}
      {entries.length === 0 ? (
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
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
          <h3 className="mt-2 text-sm font-medium text-gray-900">No Knowledge Entries</h3>
          <p className="mt-1 text-sm text-gray-500">
            Get started by adding a custom entry.
          </p>
          <div className="mt-6">
            <button
              onClick={handleAddEntry}
              className="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
            >
              Add Entry
            </button>
          </div>
        </div>
      ) : (
        <div className="bg-white shadow overflow-hidden sm:rounded-lg">
          <ul className="divide-y divide-gray-200">
            {entries.map((entry) => (
              <li
                key={entry.id}
                className="hover:bg-gray-50 transition-colors cursor-pointer"
                onClick={() => handleEntryClick(entry)}
              >
                <div className="px-6 py-4">
                  <div className="flex items-start justify-between">
                    <div className="flex-1 min-w-0">
                      <h3 className="text-lg font-medium text-gray-900 mb-2">{entry.title}</h3>

                      <div className="flex flex-wrap gap-2 mb-2">
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
                            className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800"
                          >
                            {tag}
                          </span>
                        ))}
                      </div>

                      <p className="text-sm text-gray-500 line-clamp-2">
                        {entry.content.substring(0, 200)}...
                      </p>

                      <div className="mt-2 flex items-center space-x-4 text-xs text-gray-500">
                        <span>📊 Used {entry.usageCount} times</span>
                        {entry.averageRating && <span>⭐ {entry.averageRating?.toFixed(1) ?? 'N/A'}</span>}
                        <span>📅 {formatDate(entry.updatedAt)}</span>
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
            ))}
          </ul>
        </div>
      )}

      {/* Modals */}
      {selectedEntry && (
        <KnowledgeDetailModal
          entry={selectedEntry}
          isOpen={isDetailModalOpen}
          onClose={() => {
            setIsDetailModalOpen(false);
            setSelectedEntry(null);
          }}
          onDeleted={handleEntryDeleted}
        />
      )}

      <AddKnowledgeModal
        isOpen={isAddModalOpen}
        onClose={() => setIsAddModalOpen(false)}
        onSaved={handleEntrySaved}
      />
    </div>
  );
}
