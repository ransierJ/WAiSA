using WAiSA.API.Infrastructure;
using WAiSA.API.Middleware;
using WAiSA.Core.Interfaces;
using WAiSA.Infrastructure.Data;
using WAiSA.Infrastructure.Services;
using WAiSA.Shared.Configuration;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
// Security Services
using WAiSA.API.Security.Guards;
using WAiSA.API.Security.Staging;
using WAiSA.API.Security.Validation;
using WAiSA.API.Security.Filtering;
using WAiSA.API.Security.Auditing;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Configuration
// ============================================================================

builder.Services.Configure<CosmosDbOptions>(
    builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<AzureSearchOptions>(
    builder.Configuration.GetSection("AzureSearch"));
builder.Services.Configure<CascadingSearchOptions>(
    builder.Configuration.GetSection(CascadingSearchOptions.SectionName));

// ============================================================================
// Database - SQL Server (Entity Framework)
// ============================================================================

var sqlConnectionString = builder.Configuration.GetConnectionString("SqlDatabase");
builder.Services.AddDbContext<WAiSADbContext>(options =>
    options.UseSqlServer(sqlConnectionString));

// ============================================================================
// Database - Cosmos DB (Device Memory)
// ============================================================================

builder.Services.AddSingleton(sp =>
{
    var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb");

    // Configure Cosmos to use System.Text.Json (matches our model attributes)
    var options = new CosmosClientOptions
    {
        ConsistencyLevel = ConsistencyLevel.Session, // Ensures reads see recent writes in same session
        Serializer = new CosmosSystemTextJsonSerializer(
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            })
    };

    return new CosmosClient(cosmosConnectionString, options);
});

// ============================================================================
// AI Services - Azure OpenAI
// ============================================================================

builder.Services.AddSingleton(sp =>
{
    var openAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
    var openAiKey = builder.Configuration["AzureOpenAI:ApiKey"];
    return new OpenAIClient(new Uri(openAiEndpoint!), new AzureKeyCredential(openAiKey!));
});

// ============================================================================
// AI Services - Azure AI Search
// ============================================================================

builder.Services.AddSingleton(sp =>
{
    var searchEndpoint = builder.Configuration["AzureSearch:Endpoint"];
    var searchKey = builder.Configuration["AzureSearch:AdminKey"];
    var indexName = builder.Configuration["AzureSearch:KnowledgeBaseIndexName"];
    return new SearchClient(new Uri(searchEndpoint!), indexName!, new AzureKeyCredential(searchKey!));
});

// ============================================================================
// Application Services
// ============================================================================

builder.Services.AddScoped<IDeviceMemoryService, DeviceMemoryService>();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
builder.Services.AddScoped<IAIOrchestrationService, AIOrchestrationService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<ICommandClassificationService, CommandClassificationService>();
builder.Services.AddScoped<IAgentChatHistoryService, AgentChatHistoryService>();
builder.Services.AddScoped<PowerShellDocsIngestionService>();

// NEW: Chat context and history services
builder.Services.AddSingleton<SessionContextManager>(); // Singleton for in-memory sessions
builder.Services.AddScoped<ChatHistoryService>(); // Scoped for Cosmos DB operations

// Memory cache for cascading search (Tier 1)
builder.Services.AddMemoryCache();

// Cascading Search Service (3-tier: Cache → Knowledge Base → External API)
builder.Services.AddScoped<ICascadingSearchService, CascadingSearchService>();

// Search Service with HttpClient
builder.Services.AddHttpClient<ISearchService, SearchService>();

// ============================================================================
// New Cascade Services (KB → LLM → MS Docs → Web with Confidence Scoring)
// ============================================================================

// Microsoft Docs Service with HttpClient
builder.Services.AddHttpClient<IMicrosoftDocsService, MicrosoftDocsService>();

// Web Search Service with HttpClient
builder.Services.AddHttpClient<IWebSearchService, WebSearchService>();

// Confidence Scorer
builder.Services.AddScoped<IConfidenceScorer, ConfidenceScorer>();

// Cascade Orchestrator (coordinates all cascade stages)
builder.Services.AddScoped<ICascadeOrchestrator, CascadeOrchestrator>();

// ============================================================================
// AI Agent Security Services (Phase 1 Guardrails)
// ============================================================================

// Lateral Movement Prevention
builder.Services.AddSingleton<ILateralMovementGuard>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LateralMovementGuard>>();
    var guardrailsPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "ai-agent-guardrails-enhanced.yml");
    return new LateralMovementGuard(logger, guardrailsPath);
});

// Secure Script Staging
builder.Services.AddSingleton<IStagingManager, SecureStagingManager>();

// Input Validation
builder.Services.AddInputValidation();

// Command Filtering Engine
var guardrailsConfigPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "ai-agent-guardrails-enhanced.yml");
builder.Services.AddCommandFiltering(builder.Configuration, guardrailsConfigPath);

// Audit Logging
builder.Services.AddAuditLogging(builder.Configuration);

// ============================================================================
// API Configuration
// ============================================================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI Windows System Administrator API",
        Version = "v1",
        Description = "Natural Language AI-driven System Administrator for Windows Desktop"
    });
});

// ============================================================================
// CORS
// ============================================================================

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                  "https://waisafrontend.z20.web.core.windows.net",
                  "http://localhost:5173",
                  "http://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

// ============================================================================
// Health Checks
// ============================================================================

builder.Services.AddHealthChecks()
    .AddDbContextCheck<WAiSADbContext>("sql-database")
    .AddAzureCosmosDB(
        sp => sp.GetRequiredService<CosmosClient>(),
        name: "cosmos-db");

// ============================================================================
// Application Insights
// ============================================================================

builder.Services.AddApplicationInsightsTelemetry();

// ============================================================================
// Build Application
// ============================================================================

var app = builder.Build();

// ============================================================================
// HTTP Request Pipeline
// ============================================================================

// Global exception handler (must be early in pipeline)
app.UseExceptionHandler(options => { });

// Swagger (all environments for POC)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI SysAdmin API v1");
    options.RoutePrefix = string.Empty; // Swagger at root URL
});

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// ============================================================================
// Database Migration (auto-apply on startup for POC)
// ============================================================================

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WAiSADbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error applying database migrations");
    }
}

// ============================================================================
// Run Application
// ============================================================================

app.Run();
