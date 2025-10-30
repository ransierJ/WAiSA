import express from 'express';
import cors from 'cors';
import bodyParser from 'body-parser';
import axios from 'axios';

const app = express();
const PORT = process.env.PORT || 3001;

// Middleware
app.use(cors());
app.use(bodyParser.json());

// Microsoft Learn API base URL
const MS_LEARN_API = 'https://learn.microsoft.com/api/search';

/**
 * Search Microsoft Learn documentation
 * POST /api/docs/search
 * Body: { query: string, maxResults?: number }
 */
app.post('/api/docs/search', async (req, res) => {
  try {
    const { query, maxResults = 10 } = req.body;

    if (!query) {
      return res.status(400).json({ error: 'Query parameter is required' });
    }

    console.log(`Searching Microsoft Learn for: ${query}`);

    // Call Microsoft Learn search API
    const response = await axios.get(MS_LEARN_API, {
      params: {
        search: query,
        locale: 'en-us',
        $top: maxResults,
        facet: 'category',
      },
      headers: {
        'User-Agent': 'WAiSA-MCP-Bridge/1.0',
      },
    });

    const results = response.data.results || [];

    // Format results for easier consumption
    const formattedResults = results.map(result => ({
      title: result.title,
      url: result.url,
      description: result.description || '',
      excerpt: result.displayUrl || '',
      lastUpdated: result.lastUpdatedDate,
    }));

    console.log(`Found ${formattedResults.length} results`);

    res.json({
      query,
      count: formattedResults.length,
      results: formattedResults,
    });
  } catch (error) {
    console.error('Error searching Microsoft Learn:', error.message);
    res.status(500).json({
      error: 'Failed to search Microsoft Learn',
      details: error.message
    });
  }
});

/**
 * Fetch Microsoft Learn documentation page
 * POST /api/docs/fetch
 * Body: { url: string }
 */
app.post('/api/docs/fetch', async (req, res) => {
  try {
    const { url } = req.body;

    if (!url) {
      return res.status(400).json({ error: 'URL parameter is required' });
    }

    if (!url.includes('learn.microsoft.com') && !url.includes('docs.microsoft.com')) {
      return res.status(400).json({ error: 'URL must be from Microsoft Learn or Docs' });
    }

    console.log(`Fetching Microsoft Learn page: ${url}`);

    // Fetch the page content
    const response = await axios.get(url, {
      headers: {
        'User-Agent': 'WAiSA-MCP-Bridge/1.0',
        'Accept': 'text/html,application/xhtml+xml',
      },
    });

    // Extract main content (simplified - in production, use proper HTML parsing)
    const html = response.data;

    // Simple extraction of text content between <main> tags
    const mainContentMatch = html.match(/<main[^>]*>([\s\S]*?)<\/main>/i);
    let content = mainContentMatch ? mainContentMatch[1] : html;

    // Strip HTML tags for simplified content
    content = content.replace(/<script[^>]*>[\s\S]*?<\/script>/gi, '');
    content = content.replace(/<style[^>]*>[\s\S]*?<\/style>/gi, '');
    content = content.replace(/<[^>]+>/g, ' ');
    content = content.replace(/\s+/g, ' ').trim();

    // Limit content length to avoid huge responses
    if (content.length > 10000) {
      content = content.substring(0, 10000) + '...';
    }

    console.log(`Fetched content length: ${content.length} characters`);

    res.json({
      url,
      content,
      contentLength: content.length,
    });
  } catch (error) {
    console.error('Error fetching Microsoft Learn page:', error.message);
    res.status(500).json({
      error: 'Failed to fetch Microsoft Learn page',
      details: error.message
    });
  }
});

/**
 * Search for code samples
 * POST /api/docs/code-samples
 * Body: { query: string, language?: string, maxResults?: number }
 */
app.post('/api/docs/code-samples', async (req, res) => {
  try {
    const { query, language, maxResults = 10 } = req.body;

    if (!query) {
      return res.status(400).json({ error: 'Query parameter is required' });
    }

    console.log(`Searching code samples for: ${query}${language ? ` (${language})` : ''}`);

    // Search with code-specific filters
    const searchQuery = `${query} ${language || ''} code sample example`;

    const response = await axios.get(MS_LEARN_API, {
      params: {
        search: searchQuery,
        locale: 'en-us',
        $top: maxResults,
        $filter: language ? `programmingLanguage eq '${language}'` : undefined,
      },
      headers: {
        'User-Agent': 'WAiSA-MCP-Bridge/1.0',
      },
    });

    const results = response.data.results || [];

    const formattedResults = results.map(result => ({
      title: result.title,
      url: result.url,
      description: result.description || '',
      language: language || 'unknown',
    }));

    console.log(`Found ${formattedResults.length} code sample results`);

    res.json({
      query,
      language,
      count: formattedResults.length,
      results: formattedResults,
    });
  } catch (error) {
    console.error('Error searching code samples:', error.message);
    res.status(500).json({
      error: 'Failed to search code samples',
      details: error.message
    });
  }
});

/**
 * Health check endpoint
 */
app.get('/health', (req, res) => {
  res.json({
    status: 'healthy',
    service: 'MCP Bridge for Microsoft Learn',
    version: '1.0.0',
  });
});

/**
 * List available tools (for MCP compatibility)
 */
app.get('/api/tools', (req, res) => {
  res.json({
    tools: [
      {
        name: 'microsoft_docs_search',
        description: 'Search Microsoft Learn documentation',
        inputSchema: {
          type: 'object',
          properties: {
            query: { type: 'string', description: 'Search query' },
            maxResults: { type: 'number', description: 'Maximum results to return', default: 10 },
          },
          required: ['query'],
        },
      },
      {
        name: 'microsoft_docs_fetch',
        description: 'Fetch full content from a Microsoft Learn documentation page',
        inputSchema: {
          type: 'object',
          properties: {
            url: { type: 'string', description: 'URL of the Microsoft Learn page' },
          },
          required: ['url'],
        },
      },
      {
        name: 'microsoft_code_sample_search',
        description: 'Search for code samples in Microsoft Learn',
        inputSchema: {
          type: 'object',
          properties: {
            query: { type: 'string', description: 'Search query' },
            language: { type: 'string', description: 'Programming language filter' },
            maxResults: { type: 'number', description: 'Maximum results to return', default: 10 },
          },
          required: ['query'],
        },
      },
    ],
  });
});

// Start server
app.listen(PORT, () => {
  console.log(`MCP Bridge server running on port ${PORT}`);
  console.log(`Health check: http://localhost:${PORT}/health`);
  console.log(`Tools list: http://localhost:${PORT}/api/tools`);
});
