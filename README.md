# WAiSA - Workplace AI System Assistant

**AI-Powered Windows System Administration Assistant**

WAiSA is an intelligent assistant designed to help Windows system administrators with chat-based interactions, automated command execution, and knowledge management. Built with enterprise-grade security, it provides safe AI-powered assistance for system administration tasks.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Azure](https://img.shields.io/badge/Azure-Cloud-0078D4?logo=microsoft-azure)](https://azure.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Features

### Core Capabilities

- **🤖 AI-Powered Chat Interface** - Natural language interactions with GPT-4o for Windows system administration
- **🔐 Enterprise Security Framework** - Multi-layered security with input validation, command filtering, and audit logging
- **🎯 Agent Management** - Register and manage Windows agents with API key authentication and heartbeat monitoring
- **💾 Device Memory** - Context-aware interactions with persistent device history and conversation tracking
- **📚 Knowledge Base Management** - Semantic search across PowerShell documentation and system knowledge
- **⚡ Cascading Search** - Multi-tier search system (Cache → Knowledge Base → External API) with automatic fallback
- **📊 Comprehensive Diagnostics** - Service health monitoring and configuration validation

### Security Features

WAiSA implements **defense-in-depth** security:

- **Input Validation** - Path traversal detection, malicious pattern blocking, size limits
- **Command Filtering** - Semantic analysis to prevent dangerous operations
- **Lateral Movement Guard** - Detection and prevention of unauthorized access attempts
- **Secure Script Staging** - Sandboxed script execution with validation
- **Audit Logging** - Complete audit trail of all agent actions and commands
- **Rate Limiting** - Protection against abuse and resource exhaustion
- **Context Validation** - Verification of execution context and permissions

### Cascading Search System

Intelligent multi-tier search with automatic fallback:

```
┌─────────────────────────────────────────────┐
│ Tier 1: Memory Cache (< 10ms)              │
├─────────────────────────────────────────────┤
│ Tier 2: Knowledge Base (100-500ms)         │
│         - Azure AI Search                   │
│         - Vector embeddings                 │
│         - Semantic search                   │
├─────────────────────────────────────────────┤
│ Tier 3: External API (1-5s)                │
│         - Microsoft Docs                    │
│         - Web search (optional)             │
└─────────────────────────────────────────────┘
```

**Thresholds:**
- Knowledge Base: 0.85 confidence
- LLM: 0.75 confidence
- Microsoft Docs: 0.80 confidence
- Web Search: 0.70 confidence

## Architecture

### Technology Stack

**Backend:**
- .NET 8.0 Web API
- ASP.NET Core
- Entity Framework Core

**Azure Services:**
- **Azure OpenAI** - GPT-4o for chat, text-embedding-ada-002 for vectors
- **Azure Cosmos DB** - Device memory and chat history
- **Azure SQL Database** - Structured data storage
- **Azure AI Search** - Knowledge base with vector search
- **Azure Service Bus** - Asynchronous messaging
- **Azure SignalR** - Real-time updates
- **Azure Key Vault** - Secrets management
- **Application Insights** - Monitoring and diagnostics

### API Endpoints

**Chat:**
- `POST /api/chat/send` - Send chat message
- `POST /api/chat/stream` - Stream chat response (SSE)
- `GET /api/chat/history` - Get conversation history

**Agents:**
- `POST /api/agents/register` - Register new agent
- `POST /api/agents/heartbeat` - Agent heartbeat
- `GET /api/agents/{id}` - Get agent details

**Knowledge Base:**
- `GET /api/knowledgebase` - List knowledge entries
- `GET /api/knowledgebase/search` - Search knowledge
- `POST /api/knowledgebase/retrieve` - Semantic search
- `POST /api/knowledgebase/ingest` - Ingest documentation

**Devices:**
- `GET /api/devices` - List all devices
- `GET /api/devices/{id}` - Get device details
- `GET /api/devices/{id}/interactions` - Get interaction history
- `POST /api/devices/{id}/summarize` - Summarize device context

**Diagnostics:**
- `GET /api/diagnostics/ping` - Health check
- `GET /api/diagnostics/config` - Configuration status
- `GET /api/diagnostics/services` - Service health

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Azure subscription
- Azure CLI (for deployment)

### Azure Infrastructure Deployment

Deploy the complete Azure infrastructure using Bicep:

```bash
cd infra

# Quick deployment
./deploy.sh

# Or with custom parameters
az deployment group create \
  --resource-group waisa-poc-rg \
  --template-file main.bicep \
  --parameters main.bicepparam \
  --parameters sqlAdminPassword="YourSecurePassword"
```

See [infra/README.md](infra/README.md) for detailed deployment instructions.

### Local Development

1. **Clone the repository:**
   ```bash
   git clone https://github.com/ransierJ/WAiSA.git
   cd WAiSA
   ```

2. **Configure user secrets:**
   ```bash
   cd backend/WAiSA.API

   # Initialize user secrets
   dotnet user-secrets init

   # Set Azure OpenAI credentials
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-openai.openai.azure.com/"
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
   dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "gpt-4o"

   # Set Cosmos DB connection
   dotnet user-secrets set "ConnectionStrings:CosmosDb" "your-cosmos-connection-string"

   # Set Azure Search credentials
   dotnet user-secrets set "AzureSearch:Endpoint" "https://your-search.search.windows.net"
   dotnet user-secrets set "AzureSearch:AdminKey" "your-admin-key"
   ```

3. **Run the application:**
   ```bash
   dotnet restore
   dotnet build
   dotnet run --project backend/WAiSA.API
   ```

4. **Access the API:**
   - Swagger UI: `https://localhost:7001/swagger`
   - Health check: `https://localhost:7001/api/diagnostics/ping`

## Configuration

### Application Settings

Key configuration sections in `appsettings.json`:

```json
{
  "Cascade": {
    "KnowledgeBaseThreshold": 0.85,
    "LLMThreshold": 0.75,
    "MicrosoftDocsThreshold": 0.80,
    "WebSearchThreshold": 0.70,
    "EnableEarlyStopping": true,
    "EnableConflictDetection": true
  },
  "ChatHistory": {
    "MaxTokensPerSession": 8000,
    "RetentionPeriodDays": 90,
    "MaxInteractionsBeforeSummary": 20
  },
  "Security": {
    "EnableInputValidation": true,
    "EnableCommandFiltering": true,
    "EnableLateralMovementDetection": true,
    "EnableAuditLogging": true
  }
}
```

### Environment Variables

Required for production:

```bash
# Azure Services
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
COSMOS_DB_CONNECTION_STRING=your-cosmos-connection
AZURE_SEARCH_ENDPOINT=https://your-search.search.windows.net
AZURE_SEARCH_ADMIN_KEY=your-admin-key

# Optional
GOOGLE_API_KEY=your-google-api-key  # For web search
APPLICATION_INSIGHTS_CONNECTION_STRING=your-appinsights-connection
```

## Security Considerations

### Secrets Management

**Never commit secrets to version control.** Use:

- **Azure Key Vault** for production secrets
- **User Secrets** for local development (.NET)
- **Environment Variables** for container deployments
- **Key Vault References** in App Service

### Credential Rotation

After the security incident, the following credentials were identified for rotation:

⚠️ **Action Required:** See [SECURITY_INCIDENT_RESPONSE.md](SECURITY_INCIDENT_RESPONSE.md) for complete remediation steps.

- SQL Server admin password
- Google API key
- Azure OpenAI API keys
- Azure Search admin keys
- Cosmos DB connection strings

## Project Structure

```
WAiSA/
├── backend/
│   ├── WAiSA.API/              # Web API project
│   │   ├── Controllers/        # API endpoints
│   │   ├── Security/           # Security framework
│   │   │   ├── Guards/         # Security guards
│   │   │   ├── Validation/     # Input validation
│   │   │   ├── Filtering/      # Command filtering
│   │   │   ├── Staging/        # Script staging
│   │   │   └── Auditing/       # Audit logging
│   │   └── Models/             # DTOs
│   ├── WAiSA.Infrastructure/   # Service implementations
│   │   └── Services/           # Core services
│   ├── WAiSA.Core/             # Interfaces and domain
│   │   └── Interfaces/         # Service contracts
│   ├── WAiSA.Shared/           # Shared models
│   │   ├── Models/             # Domain models
│   │   └── Configuration/      # Config classes
│   └── WAiSA.Tests/            # Unit tests
├── infra/                      # Azure infrastructure
│   ├── main.bicep              # Infrastructure as Code
│   ├── main.bicepparam         # Parameters
│   └── deploy.sh               # Deployment script
└── docs/                       # Documentation
```

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Workflow

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Make your changes
4. Write/update tests
5. Ensure all tests pass: `dotnet test`
6. Commit with conventional commits: `feat: add new feature`
7. Push and create a Pull Request

### Code Standards

- Follow C# coding conventions
- Write unit tests for new features
- Update documentation
- Use conventional commit messages
- Ensure all security checks pass

## Roadmap

- [ ] Multi-tenant support
- [ ] Advanced analytics dashboard
- [ ] PowerShell module for direct agent integration
- [ ] Slack/Teams integration
- [ ] Extended knowledge sources (GitHub, Confluence)
- [ ] Advanced command chaining and workflows
- [ ] Machine learning for command prediction

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **Issues:** [GitHub Issues](https://github.com/ransierJ/WAiSA/issues)
- **Documentation:** [Wiki](https://github.com/ransierJ/WAiSA/wiki)
- **Security:** See [SECURITY_INCIDENT_RESPONSE.md](SECURITY_INCIDENT_RESPONSE.md)

## Acknowledgments

- Built with [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
- Powered by [.NET 8.0](https://dotnet.microsoft.com/)
- Infrastructure managed with [Azure Bicep](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)

---

**Status:** Active Development
**Version:** 1.0.0
**Last Updated:** 2025-10-30
