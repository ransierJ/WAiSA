import { useState } from 'react';
import { MessageList } from './MessageList';
import { InputBox } from './InputBox';
import { ActivityPanel } from './ActivityPanel';
import { ChatHistoryPanel } from './ChatHistoryPanel';
import { chatApi } from '../services/api';
import type { Message, ExecutedCommand, ActivityLog } from '../types';

export const ChatInterface = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentActivityLogs, setCurrentActivityLogs] = useState<ActivityLog[]>([]);
  const [activeTab, setActiveTab] = useState<'chat' | 'history'>('chat');
  const [deviceId] = useState(() => {
    // Get or create device ID from localStorage
    let id = localStorage.getItem('deviceId');
    if (!id) {
      id = `web-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
      localStorage.setItem('deviceId', id);
    }
    return id;
  });

  const [conversationId, setConversationId] = useState(() => {
    // Generate a new conversation ID for this chat session
    return `conv-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  });

  const handleNewChat = () => {
    // Generate a new conversation ID
    const newConversationId = `conv-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    setConversationId(newConversationId);

    // Clear current messages and activity logs
    setMessages([]);
    setCurrentActivityLogs([]);

    console.log('Started new conversation:', newConversationId);
  };

  const handleSendMessage = async (content: string) => {
    // Add user message immediately
    const userMessage: Message = {
      id: Date.now().toString(),
      role: 'user',
      content,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setIsLoading(true);
    setCurrentActivityLogs([]); // Clear previous activity logs

    try {
      // Send to backend
      const response = await chatApi.sendMessage({
        deviceId,
        message: content,
        conversationId,
      });

      // Store activity logs for display
      setCurrentActivityLogs(response.activityLogs || []);

      // Add assistant response
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.message,
        timestamp: new Date(),
        commands: response.executedCommands as ExecutedCommand[],
      };

      setMessages((prev) => [...prev, assistantMessage]);
    } catch (error) {
      console.error('Error sending message:', error);

      // Add error message
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: 'Sorry, I encountered an error processing your request. Please try again.',
        timestamp: new Date(),
      };

      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col h-full bg-gray-50">
      {/* Header with Tabs */}
      <div className="bg-white border-b border-gray-200">
        <div className="px-6 py-4 flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">
              WAiSA
            </h1>
            <p className="text-sm text-gray-500 mt-1">
              Device ID: {deviceId}
            </p>
          </div>
          <button
            onClick={handleNewChat}
            disabled={isLoading}
            className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            ➕ New Chat
          </button>
        </div>
        <div className="flex border-t border-gray-200">
          <button
            onClick={() => setActiveTab('chat')}
            className={`flex-1 px-6 py-3 text-sm font-medium transition-colors ${
              activeTab === 'chat'
                ? 'border-b-2 border-blue-600 text-blue-600 bg-blue-50'
                : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
            }`}
          >
            💬 Chat
          </button>
          <button
            onClick={() => setActiveTab('history')}
            className={`flex-1 px-6 py-3 text-sm font-medium transition-colors ${
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
        <div className="flex-1 flex overflow-hidden">
          {/* Main chat area (70%) */}
          <div className="flex-1 flex flex-col" style={{ width: '70%' }}>
            <MessageList messages={messages} />
            <InputBox onSendMessage={handleSendMessage} disabled={isLoading} />
          </div>

          {/* Activity panel (30%) */}
          <div style={{ width: '30%' }}>
            <ActivityPanel
              activities={currentActivityLogs}
              isActive={isLoading || currentActivityLogs.length > 0}
            />
          </div>
        </div>
      ) : (
        /* History Tab */
        <div className="flex-1 overflow-hidden">
          <ChatHistoryPanel targetId={deviceId} targetType="user" />
        </div>
      )}

      {/* Loading indicator */}
      {isLoading && (
        <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2">
          <div className="bg-white rounded-lg shadow-lg p-4">
            <div className="flex items-center gap-3">
              <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-500"></div>
              <span className="text-sm text-gray-600">Processing...</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
