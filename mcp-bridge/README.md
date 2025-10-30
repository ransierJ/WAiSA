# MCP Bridge for Microsoft Learn

This is an HTTP bridge service that provides access to Microsoft Learn documentation through a simple REST API. It's designed to work with the WAiSA backend to enable AI-powered documentation lookups.

## Features

- **Documentation Search**: Search Microsoft Learn for relevant articles and code samples
- **Content Fetching**: Retrieve full content from Microsoft Learn pages
- **Code Sample Search**: Find code examples filtered by programming language
- **MCP-Compatible**: Exposes tools in a format compatible with Model Context Protocol

## API Endpoints

### Health Check
```
GET /health
```
Returns the service health status.

### List Available Tools
```
GET /api/tools
```
Returns a list of available MCP tools and their schemas.

### Search Documentation
```
POST /api/docs/search
Content-Type: application/json

{
  "query": "PowerShell remoting",
  "maxResults": 10
}
```

### Fetch Documentation Page
```
POST /api/docs/fetch
Content-Type: application/json

{
  "url": "https://learn.microsoft.com/en-us/powershell/scripting/overview"
}
```

### Search Code Samples
```
POST /api/docs/code-samples
Content-Type: application/json

{
  "query": "file operations",
  "language": "powershell",
  "maxResults": 10
}
```

## Installation

```bash
npm install
```

## Running

Development mode (with auto-reload):
```bash
npm run dev
```

Production mode:
```bash
npm start
```

The server runs on port 3001 by default. You can change this with the `PORT` environment variable:

```bash
PORT=8080 npm start
```

## Docker (Optional)

To run in a Docker container:

```bash
docker build -t mcp-bridge .
docker run -p 3001:3001 mcp-bridge
```

## Integration with WAiSA

The .NET backend will call this service to retrieve documentation when the AI needs to reference Microsoft Learn content.

The service is designed to be called from the Azure OpenAI function calling mechanism.
