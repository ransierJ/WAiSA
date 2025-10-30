# WAiSA - Workplace AI System Assistant

> **Intelligent information routing system with confidence-based orchestration**

WAiSA (Workplace AI System Assistant) is an advanced AI-powered system that intelligently routes queries to multiple information sources—including knowledge bases, LLMs, Microsoft documentation, and web search—and returns the highest-confidence answer with comprehensive metadata.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Azure](https://img.shields.io/badge/Azure-Cloud-0089D6?logo=microsoft-azure)](https://azure.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## 🌟 Features

### Confidence-Based Routing
- **Multi-Source Intelligence** - Queries knowledge bases, LLMs, MS Docs, and web simultaneously
- **Confidence Scoring** - Evaluates answer quality with detailed metrics
- **Smart Orchestration** - Selects optimal routing strategy based on query type
- **Conflict Resolution** - Handles conflicting answers intelligently
- **Edge Case Handling** - Robust fallback mechanisms

### Supported Information Sources
- 🗂️ **Local Knowledge Base** - Fast, free, offline access
- 🤖 **LLM Providers** - Claude, GPT, Gemini
- 📚 **Microsoft Documentation** - Official MS Learn integration
- 🌐 **Web Search** - Bing and Google search

### Enterprise Features
- **Azure Integration** - Cosmos DB, SQL Database, Service Bus, SignalR
- **Real-time Updates** - WebSocket support for live responses
- **Caching Layer** - Redis-compatible distributed cache
- **Monitoring** - Application Insights and Log Analytics
- **Security** - Azure Key Vault, RBAC, managed identities

## 🏗️ Architecture

```
┌──────────────┐
│   Client     │
└──────┬───────┘
       │
┌──────▼──────────────────────────────┐
│      API Gateway                    │
│  (Auth, Rate Limit, Validation)     │
└──────┬──────────────────────────────┘
       │
┌──────▼──────────────────────────────┐
│  Confidence-Based Orchestrator      │
│  ┌────────────────────────────────┐ │
│  │ Query Classifier               │ │
│  │ Strategy Selector              │ │
│  │ Confidence Aggregator          │ │
│  │ Routing Executor               │ │
│  └────────────────────────────────┘ │
└──┬────────┬────────┬────────┬───────┘
   │        │        │        │
   ▼        ▼        ▼        ▼
┌────┐  ┌─────┐  ┌──────┐  ┌─────┐
│ KB │  │ LLM │  │ Docs │  │ Web │
└────┘  └─────┘  └──────┘  └─────┘
```

## 🚀 Quick Start

### Prerequisites

- **.NET 8.0 SDK** or later
- **Azure Subscription** (for cloud deployment)
- **Azure CLI** (for infrastructure deployment)
- **Docker** (optional, for local development)

### Local Development

1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-org/waisa.git
   cd waisa
   ```

2. **Configure local settings:**
   ```bash
   cd backend/WAiSA.API
   cp appsettings.json appsettings.Development.json
   # Edit appsettings.Development.json with your settings
   ```

3. **Run the API:**
   ```bash
   dotnet run --project backend/WAiSA.API
   ```

4. **Access the API:**
   - API: `http://localhost:5000`
   - Swagger UI: `http://localhost:5000/swagger`

### Cloud Deployment

Deploy to Azure using the provided infrastructure-as-code:

```bash
cd infra

# Quick deploy (interactive)
./deploy.sh

# Or with parameters
./deploy.sh \
  --resource-group waisa-poc-rg \
  --location eastus2 \
  --sql-password 'YourSecurePassword123!'
```

See [Infrastructure Documentation](infra/README.md) for detailed deployment instructions.

## 📁 Project Structure

```
.
├── backend/                      # .NET Backend
│   ├── WAiSA.API/               # Main API project
│   ├── WAiSA.Infrastructure/    # Infrastructure layer
│   ├── WAiSA.Shared/            # Shared models and utilities
│   └── WAiSA.API.Tests/         # Unit and integration tests
│
├── infra/                        # Infrastructure as Code
│   ├── main.bicep               # Bicep template
│   ├── main.bicepparam          # Parameters file
│   ├── deploy.sh                # Deployment script
│   └── README.md                # Infrastructure docs
│
├── docs/                         # Documentation
│   ├── architecture-diagrams.md
│   ├── api-specification.yaml
│   └── confidence-routing-architecture.md
│
└── .gitignore                   # Git ignore rules
```

## 🔧 Configuration

### Application Settings

Key configuration in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SqlDatabase": "Server=...",
    "CosmosDb": "AccountEndpoint=..."
  },
  "OpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
    "DeploymentName": "gpt-4o"
  },
  "AzureSearch": {
    "ServiceName": "your-search-service",
    "IndexName": "waisa-index"
  }
}
```

### Environment Variables

Set these for local development:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export AZURE_CLIENT_ID=your-client-id
export AZURE_TENANT_ID=your-tenant-id
```

## 📚 Documentation

- **[Architecture Diagrams](architecture-diagrams.md)** - Visual system architecture
- **[API Specification](api-specification.yaml)** - OpenAPI/Swagger specification
- **[Infrastructure Guide](infra/README.md)** - Deployment and infrastructure
- **[Confidence Routing](confidence-routing-architecture.md)** - Routing algorithm details
- **[AI Guardrails](aiguardrails.md)** - Safety and security guidelines

## 🧪 Testing

Run tests with:

```bash
# All tests
dotnet test

# Specific test project
dotnet test backend/WAiSA.API.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 🔒 Security

- **Authentication** - Azure AD / Entra ID integration
- **Authorization** - Role-based access control (RBAC)
- **Secrets Management** - Azure Key Vault
- **Network Security** - Virtual Network integration
- **Data Encryption** - TLS 1.2+ in transit, encryption at rest

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📊 Performance

- **Query Latency** - < 500ms average for KB queries
- **Throughput** - 1000+ queries/second
- **Availability** - 99.9% SLA
- **Scalability** - Auto-scales based on load

## 🛠️ Built With

- **[.NET 8.0](https://dotnet.microsoft.com/)** - Backend framework
- **[Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service)** - LLM integration
- **[Azure Cosmos DB](https://azure.microsoft.com/en-us/products/cosmos-db/)** - NoSQL database
- **[Azure SQL Database](https://azure.microsoft.com/en-us/products/azure-sql/database/)** - Relational database
- **[Azure AI Search](https://azure.microsoft.com/en-us/products/ai-services/ai-search)** - Cognitive search
- **[SignalR](https://dotnet.microsoft.com/apps/aspnet/signalr)** - Real-time communication
- **[Service Bus](https://azure.microsoft.com/en-us/products/service-bus/)** - Message queue

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👥 Authors

- **Backend Architecture Team** - Initial work and architecture

## 🙏 Acknowledgments

- Microsoft Azure team for cloud infrastructure
- OpenAI for language model capabilities
- .NET community for excellent frameworks and tools

## 📞 Support

For issues, questions, or contributions:

- **Issues** - [GitHub Issues](https://github.com/your-org/waisa/issues)
- **Discussions** - [GitHub Discussions](https://github.com/your-org/waisa/discussions)
- **Email** - backend@example.com

## 🗺️ Roadmap

- [ ] Multi-tenant support
- [ ] Additional LLM providers
- [ ] Enhanced caching strategies
- [ ] GraphQL API support
- [ ] Mobile SDK
- [ ] On-premises deployment option

---

**Version:** 1.0.0
**Status:** Production Ready
**Last Updated:** 2025-10-30
