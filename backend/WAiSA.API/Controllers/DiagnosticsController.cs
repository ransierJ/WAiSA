using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WAiSA.Shared.Configuration;
using WAiSA.Core.Interfaces;

namespace WAiSA.API.Controllers;

/// <summary>
/// Diagnostics controller to test Azure service connectivity
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly ILogger<DiagnosticsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public DiagnosticsController(
        ILogger<DiagnosticsController> logger,
        IConfiguration _configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        this._configuration = _configuration;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Test endpoint that doesn't depend on any Azure services
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        _logger.LogInformation("Ping endpoint called");
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Check configuration values
    /// </summary>
    [HttpGet("config")]
    public IActionResult CheckConfig()
    {
        try
        {
            var config = new
            {
                CosmosDbEndpoint = _configuration["CosmosDb:Endpoint"],
                CosmosDbConnectionStringExists = !string.IsNullOrEmpty(_configuration.GetConnectionString("CosmosDb")),
                AzureOpenAIEndpoint = _configuration["AzureOpenAI:Endpoint"],
                AzureOpenAIApiKeyExists = !string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]),
                AzureSearchEndpoint = _configuration["AzureSearch:Endpoint"],
                AzureSearchAdminKeyExists = !string.IsNullOrEmpty(_configuration["AzureSearch:AdminKey"])
            };

            _logger.LogInformation("Configuration check completed");
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking configuration");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test service instantiation to identify DI failures
    /// </summary>
    [HttpGet("services")]
    public IActionResult CheckServices()
    {
        var results = new Dictionary<string, object>();

        // Test AIOrchestrationService
        try
        {
            var aiService = _serviceProvider.GetRequiredService<IAIOrchestrationService>();
            results["AIOrchestrationService"] = new { success = true, message = "Service created successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating AIOrchestrationService");
            results["AIOrchestrationService"] = new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
        }

        // Test DeviceMemoryService
        try
        {
            var deviceService = _serviceProvider.GetRequiredService<IDeviceMemoryService>();
            results["DeviceMemoryService"] = new { success = true, message = "Service created successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating DeviceMemoryService");
            results["DeviceMemoryService"] = new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
        }

        // Test KnowledgeBaseService
        try
        {
            var knowledgeService = _serviceProvider.GetRequiredService<IKnowledgeBaseService>();
            results["KnowledgeBaseService"] = new { success = true, message = "Service created successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating KnowledgeBaseService");
            results["KnowledgeBaseService"] = new { success = false, error = ex.Message, stackTrace = ex.StackTrace };
        }

        _logger.LogInformation("Service instantiation check completed");
        return Ok(results);
    }
}
