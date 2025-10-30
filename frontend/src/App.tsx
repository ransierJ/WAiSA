import { HashRouter, Routes, Route, Link, useLocation } from 'react-router-dom';
import { ChatInterface } from './components/ChatInterface';
import { Dashboard } from './pages/Dashboard';
import { AgentsList } from './pages/AgentsList';
import { AgentDetail } from './pages/AgentDetail';
import { KnowledgeBase } from './pages/KnowledgeBase';
import { Audit } from './pages/Audit';

function Navigation() {
  const location = useLocation();

  return (
    <nav className="bg-gray-800 text-white shadow-lg">
      <div className="max-w-7xl mx-auto px-4">
        <div className="flex items-center justify-between h-16">
          <div className="flex items-center space-x-8">
            <div className="flex-shrink-0 flex items-center">
              <img src="/WAiSAv2.png" alt="WAiSA Logo" className="h-48 w-48 object-contain" />
            </div>
            <div className="flex space-x-4">
              <Link
                to="/"
                className={`px-3 py-2 rounded-md text-sm font-medium ${
                  location.pathname === '/'
                    ? 'bg-gray-900 text-white'
                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
              >
                Dashboard
              </Link>
              <Link
                to="/chat"
                className={`px-3 py-2 rounded-md text-sm font-medium ${
                  location.pathname === '/chat'
                    ? 'bg-gray-900 text-white'
                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
              >
                Chat
              </Link>
              <Link
                to="/agents"
                className={`px-3 py-2 rounded-md text-sm font-medium ${
                  location.pathname.startsWith('/agents')
                    ? 'bg-gray-900 text-white'
                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
              >
                Agents
              </Link>
              <Link
                to="/knowledge"
                className={`px-3 py-2 rounded-md text-sm font-medium ${
                  location.pathname === '/knowledge'
                    ? 'bg-gray-900 text-white'
                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
              >
                Knowledge Base
              </Link>
              <Link
                to="/audit"
                className={`px-3 py-2 rounded-md text-sm font-medium ${
                  location.pathname === '/audit'
                    ? 'bg-gray-900 text-white'
                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
              >
                Audit
              </Link>
            </div>
          </div>
        </div>
      </div>
    </nav>
  );
}

function App() {
  return (
    <HashRouter>
      <div className="h-screen bg-gray-50 flex flex-col overflow-hidden">
        <Navigation />
        <div className="flex-1 overflow-hidden">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/chat" element={<ChatInterface />} />
          <Route path="/agents" element={<AgentsList />} />
          <Route path="/agents/:agentId" element={<AgentDetail />} />
          <Route path="/knowledge" element={<KnowledgeBase />} />
          <Route path="/audit" element={<Audit />} />
        </Routes>
        </div>
      </div>
    </HashRouter>
  );
}

export default App;
