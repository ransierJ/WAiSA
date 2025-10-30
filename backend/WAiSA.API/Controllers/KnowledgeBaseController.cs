using Microsoft.AspNetCore.Mvc;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Models;
using WAiSA.Infrastructure.Services;

namespace WAiSA.API.Controllers;

/// <summary>
/// Knowledge Base Management API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly PowerShellDocsIngestionService _ingestionService;
    private readonly ILogger<KnowledgeBaseController> _logger;

    public KnowledgeBaseController(
        IKnowledgeBaseService knowledgeBaseService,
        PowerShellDocsIngestionService ingestionService,
        ILogger<KnowledgeBaseController> logger)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Get all knowledge entries (paginated)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<KnowledgeBaseEntry>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _knowledgeBaseService.GetAllKnowledgeAsync(skip, take, cancellationToken);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving knowledge entries");
            return StatusCode(500, new { message = "Error retrieving knowledge entries" });
        }
    }

    /// <summary>
    /// Search knowledge base
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<KnowledgeBaseEntry>), 200)]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { message = "Query is required" });

            var results = await _knowledgeBaseService.SearchKnowledgeAsync(query, tags, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge base");
            return StatusCode(500, new { message = "Error searching knowledge base" });
        }
    }

    /// <summary>
    /// Retrieve relevant knowledge using semantic search
    /// </summary>
    [HttpPost("retrieve")]
    [ProducesResponseType(typeof(List<KnowledgeSearchResult>), 200)]
    public async Task<IActionResult> RetrieveRelevant(
        [FromBody] RetrieveKnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest(new { message = "Query is required" });

            var results = await _knowledgeBaseService.RetrieveRelevantKnowledgeAsync(
                request.Query,
                request.TopK,
                request.MinScore,
                cancellationToken);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relevant knowledge");
            return StatusCode(500, new { message = "Error retrieving relevant knowledge" });
        }
    }

    /// <summary>
    /// Add or update knowledge entry
    /// </summary>
    [HttpPost]
    [ProducesResponseType(200)]
    public async Task<IActionResult> AddOrUpdate(
        [FromBody] KnowledgeBaseEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.Title))
                return BadRequest(new { message = "Title is required" });

            if (string.IsNullOrWhiteSpace(entry.Content))
                return BadRequest(new { message = "Content is required" });

            await _knowledgeBaseService.AddOrUpdateKnowledgeAsync(entry, cancellationToken);

            return Ok(new { message = "Knowledge entry added/updated successfully", id = entry.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating knowledge entry");
            return StatusCode(500, new { message = "Error adding/updating knowledge entry" });
        }
    }

    /// <summary>
    /// Delete knowledge entry
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _knowledgeBaseService.DeleteKnowledgeAsync(id, cancellationToken);
            return Ok(new { message = "Knowledge entry deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting knowledge entry {Id}", id);
            return StatusCode(500, new { message = "Error deleting knowledge entry" });
        }
    }

    /// <summary>
    /// Ingest PowerShell cmdlet documentation
    /// </summary>
    [HttpPost("ingest/cmdlet")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> IngestCmdlet(
        [FromBody] IngestCmdletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _ingestionService.IngestCmdletDocumentationAsync(
                request.Name,
                request.Synopsis,
                request.Description,
                request.Syntax,
                request.Examples,
                request.Parameters,
                request.Tags,
                cancellationToken);

            return Ok(new { message = "Cmdlet documentation ingested successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting cmdlet documentation");
            return StatusCode(500, new { message = "Error ingesting cmdlet documentation" });
        }
    }

    /// <summary>
    /// Bulk ingest PowerShell cmdlets
    /// </summary>
    [HttpPost("ingest/cmdlets/bulk")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> BulkIngestCmdlets(
        [FromBody] List<CmdletDocumentation> cmdlets,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _ingestionService.BulkIngestCmdletsAsync(cmdlets, cancellationToken);
            return Ok(new { message = $"Bulk ingestion completed for {cmdlets.Count} cmdlets" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk ingesting cmdlets");
            return StatusCode(500, new { message = "Error bulk ingesting cmdlets" });
        }
    }

    /// <summary>
    /// Ingest best practice
    /// </summary>
    [HttpPost("ingest/best-practice")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> IngestBestPractice(
        [FromBody] IngestBestPracticeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _ingestionService.IngestBestPracticeAsync(
                request.Title,
                request.Content,
                request.Tags,
                cancellationToken);

            return Ok(new { message = "Best practice ingested successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting best practice");
            return StatusCode(500, new { message = "Error ingesting best practice" });
        }
    }

    /// <summary>
    /// Ingest troubleshooting scenario
    /// </summary>
    [HttpPost("ingest/troubleshooting")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> IngestTroubleshooting(
        [FromBody] IngestTroubleshootingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _ingestionService.IngestTroubleshootingScenarioAsync(
                request.Problem,
                request.Solution,
                request.Commands,
                request.Tags,
                cancellationToken);

            return Ok(new { message = "Troubleshooting scenario ingested successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting troubleshooting scenario");
            return StatusCode(500, new { message = "Error ingesting troubleshooting scenario" });
        }
    }

    /// <summary>
    /// Initialize Azure AI Search index for knowledge base
    /// Creates the index if it doesn't exist
    /// </summary>
    [HttpPost("initialize")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> InitializeSearchIndex(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing knowledge base search index");
            await _knowledgeBaseService.InitializeIndexAsync(cancellationToken);
            return Ok(new { message = "Search index initialized successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing search index");
            return StatusCode(500, new { message = $"Error initializing search index: {ex.Message}" });
        }
    }

    /// <summary>
    /// Test seed with just 3 cmdlets - quick verification
    /// </summary>
    [HttpPost("seed/test")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> TestSeedKnowledgeBase(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting test seed with 3 cmdlets");

            var testCmdlets = new List<CmdletDocumentation>
            {
                new()
                {
                    Name = "Get-Process",
                    Synopsis = "Gets the processes running on the local or remote computer",
                    Description = "The Get-Process cmdlet gets objects representing the processes running on the local or remote computer.",
                    Syntax = "Get-Process [[-Name] <String[]>] [-ComputerName <String[]>]",
                    Examples = new List<string>
                    {
                        "Get-Process",
                        "Get-Process -Name \"chrome\"",
                        "Get-Process | Where-Object {$_.CPU -gt 100}"
                    },
                    Parameters = new List<string>
                    {
                        "-Name: Specifies one or more processes by name",
                        "-ComputerName: Specifies the computers for which this cmdlet gets active processes"
                    },
                    Tags = new List<string> { "system", "process", "monitoring" }
                },
                new()
                {
                    Name = "Get-Service",
                    Synopsis = "Gets the services on the computer",
                    Description = "The Get-Service cmdlet gets objects that represent the services on a computer, including running and stopped services.",
                    Syntax = "Get-Service [[-Name] <String[]>] [-ComputerName <String[]>]",
                    Examples = new List<string>
                    {
                        "Get-Service",
                        "Get-Service -Name \"wuauserv\"",
                        "Get-Service | Where-Object {$_.Status -eq 'Running'}"
                    },
                    Parameters = new List<string>
                    {
                        "-Name: Service name or display name",
                        "-ComputerName: Remote computer name"
                    },
                    Tags = new List<string> { "system", "services" }
                },
                new()
                {
                    Name = "Get-EventLog",
                    Synopsis = "Gets events from event logs",
                    Description = "The Get-EventLog cmdlet gets events and event logs on the local and remote computers.",
                    Syntax = "Get-EventLog [-LogName] <String> [[-InstanceId] <Int64[]>] [-After <DateTime>] [-Before <DateTime>] [-ComputerName <String[]>] [-EntryType <String[]>] [-Newest <Int32>]",
                    Examples = new List<string>
                    {
                        "Get-EventLog -LogName System -Newest 100",
                        "Get-EventLog -LogName Application -EntryType Error -Newest 20"
                    },
                    Parameters = new List<string>
                    {
                        "-LogName: Event log name (System, Application, Security, etc.)",
                        "-EntryType: Entry types (Error, Warning, Information)",
                        "-Newest: Number of entries to return"
                    },
                    Tags = new List<string> { "events", "troubleshooting", "logs" }
                }
            };

            await _ingestionService.BulkIngestCmdletsAsync(testCmdlets, cancellationToken);

            _logger.LogInformation("Test seed completed successfully");

            return Ok(new
            {
                message = "Test seed completed successfully",
                cmdlets = testCmdlets.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test seed");
            return StatusCode(500, new { message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Seed knowledge base with common PowerShell cmdlets (batched processing)
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> SeedKnowledgeBase(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting full knowledge base seed");

            var cmdlets = GetCommonPowerShellCmdlets();
            _logger.LogInformation("Processing {Count} cmdlets", cmdlets.Count);

            var successCount = 0;
            var failCount = 0;
            var errors = new List<string>();

            // Process cmdlets one at a time with error logging
            foreach (var cmdlet in cmdlets)
            {
                try
                {
                    await _ingestionService.IngestCmdletDocumentationAsync(
                        cmdlet.Name,
                        cmdlet.Synopsis,
                        cmdlet.Description,
                        cmdlet.Syntax,
                        cmdlet.Examples,
                        cmdlet.Parameters,
                        cmdlet.Tags,
                        cancellationToken);
                    successCount++;
                    _logger.LogInformation("Successfully ingested cmdlet {Name} ({Index}/{Total})",
                        cmdlet.Name, successCount + failCount, cmdlets.Count);
                }
                catch (Exception ex)
                {
                    failCount++;
                    var error = $"{cmdlet.Name}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, "Failed to ingest cmdlet: {Name}", cmdlet.Name);
                }
            }

            _logger.LogInformation("Processing best practices");
            var bestPractices = GetWindowsAdminBestPractices();
            var bpSuccess = 0;
            var bpFail = 0;

            foreach (var bp in bestPractices)
            {
                try
                {
                    await _ingestionService.IngestBestPracticeAsync(
                        bp.Title,
                        bp.Content,
                        bp.Tags,
                        cancellationToken);
                    bpSuccess++;
                    _logger.LogInformation("Successfully ingested best practice: {Title}", bp.Title);
                }
                catch (Exception ex)
                {
                    bpFail++;
                    errors.Add($"BP - {bp.Title}: {ex.Message}");
                    _logger.LogError(ex, "Failed to ingest best practice: {Title}", bp.Title);
                }
            }

            _logger.LogInformation("Seed complete. Cmdlets: {Success}/{Total}, Best Practices: {BPSuccess}/{BPTotal}",
                successCount, cmdlets.Count, bpSuccess, bestPractices.Count);

            return Ok(new
            {
                message = "Knowledge base seed completed",
                cmdlets = new { success = successCount, failed = failCount, total = cmdlets.Count },
                bestPractices = new { success = bpSuccess, failed = bpFail, total = bestPractices.Count },
                errors = errors.Count > 0 ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during knowledge base seed");
            return StatusCode(500, new { message = $"Error: {ex.Message}", stackTrace = ex.StackTrace });
        }
    }

    private List<CmdletDocumentation> GetCommonPowerShellCmdlets()
    {
        return new List<CmdletDocumentation>
        {
            // Process Management
            new()
            {
                Name = "Get-Process",
                Synopsis = "Gets the processes running on the local or remote computer",
                Description = "The Get-Process cmdlet gets objects representing the processes running on the local or remote computer. You can use this to monitor resource usage, identify specific processes, or gather diagnostic information.",
                Syntax = "Get-Process [[-Name] <String[]>] [-ComputerName <String[]>]",
                Examples = new List<string>
                {
                    "Get-Process",
                    "Get-Process -Name \"chrome\"",
                    "Get-Process | Where-Object {$_.CPU -gt 100} | Sort-Object CPU -Descending",
                    "Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 10"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies one or more processes by name",
                    "-ComputerName: Specifies the computers for which this cmdlet gets active processes",
                    "-Id: Specifies one or more processes by process ID"
                },
                Tags = new List<string> { "system", "process", "monitoring" }
            },
            new()
            {
                Name = "Stop-Process",
                Synopsis = "Stops one or more running processes",
                Description = "The Stop-Process cmdlet stops one or more running processes. You can specify processes by process name or ID. Use -Force to stop processes without confirmation.",
                Syntax = "Stop-Process [-Id] <Int32[]> [-Force] [-PassThru]",
                Examples = new List<string>
                {
                    "Stop-Process -Name \"notepad\"",
                    "Stop-Process -Id 1234 -Force",
                    "Get-Process chrome | Stop-Process -Force"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies process names to stop",
                    "-Id: Specifies process IDs to stop",
                    "-Force: Forces the process to stop without confirmation"
                },
                Tags = new List<string> { "system", "process", "management" }
            },
            new()
            {
                Name = "Get-Service",
                Synopsis = "Gets the services on the computer",
                Description = "The Get-Service cmdlet gets objects representing the services on a local or remote computer, including running and stopped services.",
                Syntax = "Get-Service [[-Name] <String[]>] [-ComputerName <String[]>]",
                Examples = new List<string>
                {
                    "Get-Service",
                    "Get-Service -Name \"W32Time\"",
                    "Get-Service | Where-Object {$_.Status -eq 'Running'}"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies the service names of services to retrieve",
                    "-ComputerName: Specifies the remote computers",
                    "-DisplayName: Specifies display names of services"
                },
                Tags = new List<string> { "system", "service", "management" }
            },
            new()
            {
                Name = "Restart-Service",
                Synopsis = "Stops and then starts one or more services",
                Description = "The Restart-Service cmdlet stops and then starts one or more services. Use this when a service needs to be restarted to apply configuration changes.",
                Syntax = "Restart-Service [-Name] <String[]> [-Force] [-WhatIf]",
                Examples = new List<string>
                {
                    "Restart-Service -Name \"W32Time\"",
                    "Restart-Service -Name \"Spooler\" -Force"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies the service names of the services to restart",
                    "-Force: Forces the command to run without asking for confirmation",
                    "-WhatIf: Shows what would happen if the cmdlet runs"
                },
                Tags = new List<string> { "service", "management", "restart" }
            },
            new()
            {
                Name = "Get-EventLog",
                Synopsis = "Gets events from an event log",
                Description = "The Get-EventLog cmdlet gets events and event logs on the local and remote computers. Use this for troubleshooting and diagnosing system issues.",
                Syntax = "Get-EventLog [-LogName] <String> [-Newest <Int32>] [-ComputerName <String[]>]",
                Examples = new List<string>
                {
                    "Get-EventLog -LogName System -Newest 100",
                    "Get-EventLog -LogName Application -EntryType Error -Newest 50"
                },
                Parameters = new List<string>
                {
                    "-LogName: Specifies the event log name (System, Application, Security)",
                    "-Newest: Specifies the maximum number of events to retrieve",
                    "-EntryType: Filters by entry type (Error, Warning, Information)"
                },
                Tags = new List<string> { "troubleshooting", "logs", "events" }
            },
            new()
            {
                Name = "Test-Connection",
                Synopsis = "Sends ICMP echo request packets (pings) to one or more computers",
                Description = "The Test-Connection cmdlet sends Internet Control Message Protocol (ICMP) echo request packets, or pings, to one or more remote computers and returns the echo response replies.",
                Syntax = "Test-Connection [-ComputerName] <String[]> [-Count <Int32>] [-Quiet]",
                Examples = new List<string>
                {
                    "Test-Connection -ComputerName \"google.com\"",
                    "Test-Connection -ComputerName \"192.168.1.1\" -Count 4 -Quiet"
                },
                Parameters = new List<string>
                {
                    "-ComputerName: Specifies the computers to ping",
                    "-Count: Specifies the number of echo requests to send",
                    "-Quiet: Returns True if any pings succeeded, False if all failed"
                },
                Tags = new List<string> { "network", "connectivity", "diagnostics" }
            },
            new()
            {
                Name = "Get-Counter",
                Synopsis = "Gets performance counter data from local and remote computers",
                Description = "The Get-Counter cmdlet gets live, real-time performance counter data directly from the performance monitoring instrumentation in Windows. Use this to monitor system performance.",
                Syntax = "Get-Counter [[-Counter] <String[]>] [-SampleInterval <Int32>] [-MaxSamples <Int64>]",
                Examples = new List<string>
                {
                    "Get-Counter '\\Processor(_Total)\\% Processor Time'",
                    "Get-Counter '\\Memory\\Available MBytes'",
                    "Get-Counter -Counter '\\PhysicalDisk(_Total)\\% Disk Time' -SampleInterval 2 -MaxSamples 5"
                },
                Parameters = new List<string>
                {
                    "-Counter: Specifies the performance counter path",
                    "-SampleInterval: Specifies the interval in seconds between samples",
                    "-MaxSamples: Specifies the number of samples to get"
                },
                Tags = new List<string> { "performance", "monitoring", "metrics" }
            },
            new()
            {
                Name = "Get-Disk",
                Synopsis = "Gets one or more disks visible to the operating system",
                Description = "The Get-Disk cmdlet retrieves information about all disks visible to the Windows operating system. Use this to check disk health, capacity, and configuration.",
                Syntax = "Get-Disk [[-Number] <UInt32[]>]",
                Examples = new List<string>
                {
                    "Get-Disk",
                    "Get-Disk -Number 0",
                    "Get-Disk | Select-Object Number, FriendlyName, Size, HealthStatus"
                },
                Parameters = new List<string>
                {
                    "-Number: Specifies the disk number",
                    "-FriendlyName: Specifies the friendly name of the disk"
                },
                Tags = new List<string> { "storage", "disk", "hardware" }
            },
            new()
            {
                Name = "Get-NetIPAddress",
                Synopsis = "Gets the IP address configuration",
                Description = "The Get-NetIPAddress cmdlet gets the IP address configuration, including both IPv4 and IPv6 addresses. Use this to diagnose network connectivity issues.",
                Syntax = "Get-NetIPAddress [-InterfaceAlias <String>] [-AddressFamily {IPv4 | IPv6}]",
                Examples = new List<string>
                {
                    "Get-NetIPAddress",
                    "Get-NetIPAddress -AddressFamily IPv4",
                    "Get-NetIPAddress -InterfaceAlias \"Ethernet\""
                },
                Parameters = new List<string>
                {
                    "-InterfaceAlias: Specifies the interface alias",
                    "-AddressFamily: Specifies the address family (IPv4 or IPv6)"
                },
                Tags = new List<string> { "network", "ip", "configuration" }
            },

            // Additional Service Management
            new()
            {
                Name = "Start-Service",
                Synopsis = "Starts one or more stopped services",
                Description = "The Start-Service cmdlet starts stopped services on the local or remote computer.",
                Syntax = "Start-Service [-Name] <String[]> [-PassThru]",
                Examples = new List<string>
                {
                    "Start-Service -Name \"Spooler\"",
                    "Start-Service -Name \"W32Time\" -PassThru"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies service names to start",
                    "-PassThru: Returns an object representing the service"
                },
                Tags = new List<string> { "service", "management" }
            },
            new()
            {
                Name = "Stop-Service",
                Synopsis = "Stops one or more running services",
                Description = "The Stop-Service cmdlet stops running services on the local or remote computer.",
                Syntax = "Stop-Service [-Name] <String[]> [-Force] [-PassThru]",
                Examples = new List<string>
                {
                    "Stop-Service -Name \"Spooler\"",
                    "Stop-Service -Name \"Themes\" -Force"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies service names to stop",
                    "-Force: Forces the service to stop even if it has dependent services"
                },
                Tags = new List<string> { "service", "management" }
            },
            new()
            {
                Name = "Set-Service",
                Synopsis = "Changes the properties of a service",
                Description = "The Set-Service cmdlet changes service properties like startup type, display name, and description.",
                Syntax = "Set-Service [-Name] <String> [-StartupType {Automatic | Manual | Disabled}]",
                Examples = new List<string>
                {
                    "Set-Service -Name \"W32Time\" -StartupType Automatic",
                    "Set-Service -Name \"Spooler\" -DisplayName \"Print Service\""
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies the service name",
                    "-StartupType: Sets the service startup mode",
                    "-DisplayName: Sets the display name"
                },
                Tags = new List<string> { "service", "configuration" }
            },

            // Event Logs (Modern)
            new()
            {
                Name = "Get-WinEvent",
                Synopsis = "Gets events from event logs and event tracing log files",
                Description = "The Get-WinEvent cmdlet gets events from event logs, including classic logs and new Windows Event Log technology logs. Much more powerful than Get-EventLog.",
                Syntax = "Get-WinEvent [-LogName] <String[]> [-MaxEvents <Int64>]",
                Examples = new List<string>
                {
                    "Get-WinEvent -LogName System -MaxEvents 100",
                    "Get-WinEvent -FilterHashtable @{LogName='Application';Level=2}",
                    "Get-WinEvent -LogName System | Where-Object {$_.LevelDisplayName -eq 'Error'}"
                },
                Parameters = new List<string>
                {
                    "-LogName: Specifies event log names",
                    "-MaxEvents: Maximum number of events to return",
                    "-FilterHashtable: Filters events using a hash table"
                },
                Tags = new List<string> { "troubleshooting", "logs", "events", "modern" }
            },

            // System Information
            new()
            {
                Name = "Get-ComputerInfo",
                Synopsis = "Gets a consolidated object of system and operating system properties",
                Description = "The Get-ComputerInfo cmdlet gets comprehensive system information including OS version, hardware details, and system configuration.",
                Syntax = "Get-ComputerInfo [-Property <String[]>]",
                Examples = new List<string>
                {
                    "Get-ComputerInfo",
                    "Get-ComputerInfo -Property WindowsVersion,OsHardwareAbstractionLayer",
                    "(Get-ComputerInfo).WindowsVersion"
                },
                Parameters = new List<string>
                {
                    "-Property: Specifies properties to retrieve"
                },
                Tags = new List<string> { "system", "information", "diagnostics" }
            },
            new()
            {
                Name = "Get-HotFix",
                Synopsis = "Gets the hotfixes installed on local or remote computers",
                Description = "The Get-HotFix cmdlet gets hotfixes (also called updates) that are installed on the local or remote computer.",
                Syntax = "Get-HotFix [[-Id] <String[]>] [-ComputerName <String[]>]",
                Examples = new List<string>
                {
                    "Get-HotFix",
                    "Get-HotFix -Id KB5034441",
                    "Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 10"
                },
                Parameters = new List<string>
                {
                    "-Id: Specifies hotfix IDs",
                    "-ComputerName: Specifies remote computers"
                },
                Tags = new List<string> { "system", "updates", "patches" }
            },

            // Storage Management
            new()
            {
                Name = "Get-Volume",
                Synopsis = "Gets the specified Volume object or all Volume objects",
                Description = "The Get-Volume cmdlet returns information about volumes including drive letter, file system, health status, and capacity.",
                Syntax = "Get-Volume [[-DriveLetter] <Char[]>]",
                Examples = new List<string>
                {
                    "Get-Volume",
                    "Get-Volume -DriveLetter C",
                    "Get-Volume | Where-Object {$_.SizeRemaining -lt 10GB}"
                },
                Parameters = new List<string>
                {
                    "-DriveLetter: Specifies drive letter",
                    "-FileSystemLabel: Specifies file system label"
                },
                Tags = new List<string> { "storage", "disk", "volume" }
            },
            new()
            {
                Name = "Get-PSDrive",
                Synopsis = "Gets drives in the current session",
                Description = "The Get-PSDrive cmdlet gets the drives in the current session, including filesystem drives, registry, certificate store, and more.",
                Syntax = "Get-PSDrive [[-Name] <String[]>] [-PSProvider <String[]>]",
                Examples = new List<string>
                {
                    "Get-PSDrive",
                    "Get-PSDrive -PSProvider FileSystem",
                    "Get-PSDrive C"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies drive name",
                    "-PSProvider: Specifies provider type (FileSystem, Registry, etc.)"
                },
                Tags = new List<string> { "storage", "drives", "filesystem" }
            },

            // Network - Advanced
            new()
            {
                Name = "Test-NetConnection",
                Synopsis = "Displays diagnostic information for a connection",
                Description = "Test-NetConnection is a powerful diagnostic tool that tests network connectivity, performs ping, traceroute, and port testing.",
                Syntax = "Test-NetConnection [[-ComputerName] <String>] [-Port <Int32>] [-TraceRoute]",
                Examples = new List<string>
                {
                    "Test-NetConnection google.com",
                    "Test-NetConnection -ComputerName server01 -Port 3389",
                    "Test-NetConnection -ComputerName 8.8.8.8 -TraceRoute"
                },
                Parameters = new List<string>
                {
                    "-ComputerName: Target computer or IP",
                    "-Port: TCP port number to test",
                    "-TraceRoute: Performs route tracing"
                },
                Tags = new List<string> { "network", "connectivity", "diagnostics", "modern" }
            },
            new()
            {
                Name = "Get-NetAdapter",
                Synopsis = "Gets the basic network adapter properties",
                Description = "The Get-NetAdapter cmdlet gets information about network adapters including status, speed, MAC address.",
                Syntax = "Get-NetAdapter [[-Name] <String[]>]",
                Examples = new List<string>
                {
                    "Get-NetAdapter",
                    "Get-NetAdapter -Name \"Ethernet\"",
                    "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies adapter name",
                    "-Physical: Gets only physical adapters"
                },
                Tags = new List<string> { "network", "adapter", "hardware" }
            },
            new()
            {
                Name = "Get-DnsClientCache",
                Synopsis = "Retrieves the contents of the DNS client cache",
                Description = "Gets cached DNS records from the local DNS client cache.",
                Syntax = "Get-DnsClientCache [-Entry <String[]>]",
                Examples = new List<string>
                {
                    "Get-DnsClientCache",
                    "Get-DnsClientCache -Entry \"google.com\""
                },
                Parameters = new List<string>
                {
                    "-Entry: Specifies DNS entry name"
                },
                Tags = new List<string> { "network", "dns", "cache" }
            },
            new()
            {
                Name = "Clear-DnsClientCache",
                Synopsis = "Clears the DNS client cache",
                Description = "The Clear-DnsClientCache cmdlet clears the Domain Name System (DNS) client cache (equivalent to ipconfig /flushdns).",
                Syntax = "Clear-DnsClientCache",
                Examples = new List<string>
                {
                    "Clear-DnsClientCache"
                },
                Parameters = new List<string>(),
                Tags = new List<string> { "network", "dns", "troubleshooting" }
            },
            new()
            {
                Name = "Get-NetRoute",
                Synopsis = "Gets the IP route information from the IP routing table",
                Description = "The Get-NetRoute cmdlet gets IP route information, similar to 'route print' command.",
                Syntax = "Get-NetRoute [-AddressFamily {IPv4 | IPv6}] [-DestinationPrefix <String[]>]",
                Examples = new List<string>
                {
                    "Get-NetRoute",
                    "Get-NetRoute -AddressFamily IPv4",
                    "Get-NetRoute -DestinationPrefix '0.0.0.0/0'"
                },
                Parameters = new List<string>
                {
                    "-AddressFamily: IPv4 or IPv6",
                    "-DestinationPrefix: Destination network prefix"
                },
                Tags = new List<string> { "network", "routing", "diagnostics" }
            },
            new()
            {
                Name = "Get-NetFirewallRule",
                Synopsis = "Retrieves firewall rules from the target computer",
                Description = "Gets Windows Firewall rules and their configuration.",
                Syntax = "Get-NetFirewallRule [-DisplayName <String[]>] [-Enabled {True | False}]",
                Examples = new List<string>
                {
                    "Get-NetFirewallRule | Where-Object {$_.Enabled -eq 'True'}",
                    "Get-NetFirewallRule -DisplayName '*Remote Desktop*'",
                    "Get-NetFirewallRule -Direction Inbound -Enabled True"
                },
                Parameters = new List<string>
                {
                    "-DisplayName: Rule display name pattern",
                    "-Enabled: Filter by enabled status",
                    "-Direction: Inbound or Outbound"
                },
                Tags = new List<string> { "network", "firewall", "security" }
            },

            // User Management
            new()
            {
                Name = "Get-LocalUser",
                Synopsis = "Gets local user accounts",
                Description = "The Get-LocalUser cmdlet gets local user accounts from the local Security Accounts Manager (SAM).",
                Syntax = "Get-LocalUser [[-Name] <String[]>]",
                Examples = new List<string>
                {
                    "Get-LocalUser",
                    "Get-LocalUser -Name \"Administrator\"",
                    "Get-LocalUser | Where-Object {$_.Enabled -eq $true}"
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies user name",
                    "-SID: Specifies security identifier"
                },
                Tags = new List<string> { "user", "account", "management" }
            },
            new()
            {
                Name = "Get-LocalGroup",
                Synopsis = "Gets the local security groups",
                Description = "The Get-LocalGroup cmdlet gets local security groups in the Security Accounts Manager.",
                Syntax = "Get-LocalGroup [[-Name] <String[]>]",
                Examples = new List<string>
                {
                    "Get-LocalGroup",
                    "Get-LocalGroup -Name \"Administrators\"",
                    "Get-LocalGroupMember -Group \"Administrators\""
                },
                Parameters = new List<string>
                {
                    "-Name: Specifies group name"
                },
                Tags = new List<string> { "user", "group", "security" }
            },

            // File System
            new()
            {
                Name = "Get-ChildItem",
                Synopsis = "Gets the items and child items in specified locations",
                Description = "The Get-ChildItem cmdlet gets files and folders. Use 'dir' or 'ls' as aliases.",
                Syntax = "Get-ChildItem [[-Path] <String[]>] [-Recurse] [-Force]",
                Examples = new List<string>
                {
                    "Get-ChildItem C:\\",
                    "Get-ChildItem -Path C:\\Windows -Recurse -Filter *.log",
                    "Get-ChildItem -File | Where-Object {$_.Length -gt 100MB}"
                },
                Parameters = new List<string>
                {
                    "-Path: Specifies path",
                    "-Recurse: Gets items in subdirectories",
                    "-Force: Gets hidden items"
                },
                Tags = new List<string> { "filesystem", "files", "folders" }
            },
            new()
            {
                Name = "Get-Content",
                Synopsis = "Gets the content of the item at the specified location",
                Description = "The Get-Content cmdlet reads file content. Use 'cat' or 'type' as aliases.",
                Syntax = "Get-Content [-Path] <String[]> [-Tail <Int32>]",
                Examples = new List<string>
                {
                    "Get-Content C:\\Logs\\app.log",
                    "Get-Content C:\\Logs\\app.log -Tail 50",
                    "Get-Content C:\\Logs\\app.log | Select-String 'Error'"
                },
                Parameters = new List<string>
                {
                    "-Path: File path",
                    "-Tail: Gets last N lines"
                },
                Tags = new List<string> { "filesystem", "files", "reading" }
            },
            new()
            {
                Name = "Test-Path",
                Synopsis = "Determines whether all elements of a path exist",
                Description = "The Test-Path cmdlet checks if a file or folder exists.",
                Syntax = "Test-Path [-Path] <String[]> [-PathType {Leaf | Container}]",
                Examples = new List<string>
                {
                    "Test-Path C:\\Windows",
                    "Test-Path C:\\temp\\file.txt -PathType Leaf",
                    "if (Test-Path C:\\Logs) { Get-ChildItem C:\\Logs }"
                },
                Parameters = new List<string>
                {
                    "-Path: Path to test",
                    "-PathType: Leaf (file) or Container (folder)"
                },
                Tags = new List<string> { "filesystem", "validation" }
            },
            new()
            {
                Name = "Get-FileHash",
                Synopsis = "Computes the hash value for a file",
                Description = "The Get-FileHash cmdlet computes cryptographic hash values for files using SHA256, MD5, or other algorithms.",
                Syntax = "Get-FileHash [-Path] <String[]> [-Algorithm {SHA256 | MD5 | SHA1}]",
                Examples = new List<string>
                {
                    "Get-FileHash C:\\Downloads\\file.exe",
                    "Get-FileHash C:\\file.zip -Algorithm MD5",
                    "Get-FileHash *.dll | Format-Table Hash,Path"
                },
                Parameters = new List<string>
                {
                    "-Path: File path",
                    "-Algorithm: Hash algorithm (default SHA256)"
                },
                Tags = new List<string> { "filesystem", "security", "hash" }
            },

            // Scheduled Tasks
            new()
            {
                Name = "Get-ScheduledTask",
                Synopsis = "Gets scheduled tasks on the local computer",
                Description = "The Get-ScheduledTask cmdlet gets scheduled task objects from Task Scheduler.",
                Syntax = "Get-ScheduledTask [[-TaskName] <String[]>] [-TaskPath <String>]",
                Examples = new List<string>
                {
                    "Get-ScheduledTask",
                    "Get-ScheduledTask -TaskName \"Windows Update*\"",
                    "Get-ScheduledTask | Where-Object {$_.State -eq 'Running'}"
                },
                Parameters = new List<string>
                {
                    "-TaskName: Task name pattern",
                    "-TaskPath: Path in task scheduler hierarchy"
                },
                Tags = new List<string> { "system", "tasks", "automation" }
            },

            // System Control
            new()
            {
                Name = "Restart-Computer",
                Synopsis = "Restarts the operating system on local and remote computers",
                Description = "The Restart-Computer cmdlet restarts the computer. Use -Force to force restart.",
                Syntax = "Restart-Computer [-ComputerName <String[]>] [-Force] [-Wait]",
                Examples = new List<string>
                {
                    "Restart-Computer",
                    "Restart-Computer -Force",
                    "Restart-Computer -ComputerName Server01 -Wait"
                },
                Parameters = new List<string>
                {
                    "-ComputerName: Remote computer names",
                    "-Force: Forces immediate restart",
                    "-Wait: Waits until restart completes"
                },
                Tags = new List<string> { "system", "reboot", "management" }
            },

            // Package Management
            new()
            {
                Name = "Get-Package",
                Synopsis = "Returns installed software packages",
                Description = "The Get-Package cmdlet returns a list of all software packages installed via Package Management.",
                Syntax = "Get-Package [[-Name] <String[]>] [-ProviderName <String[]>]",
                Examples = new List<string>
                {
                    "Get-Package",
                    "Get-Package -Name \"*Microsoft*\"",
                    "Get-Package | Where-Object {$_.Version -like '1.*'}"
                },
                Parameters = new List<string>
                {
                    "-Name: Package name pattern",
                    "-ProviderName: Package provider"
                },
                Tags = new List<string> { "software", "packages", "management" }
            },
            new()
            {
                Name = "Get-AppxPackage",
                Synopsis = "Gets a list of the app packages installed in a user profile",
                Description = "The Get-AppxPackage cmdlet gets modern/UWP apps installed for the current user or all users.",
                Syntax = "Get-AppxPackage [[-Name] <String>] [-AllUsers]",
                Examples = new List<string>
                {
                    "Get-AppxPackage",
                    "Get-AppxPackage *Calculator*",
                    "Get-AppxPackage -AllUsers | Where-Object {$_.Name -like '*Camera*'}"
                },
                Parameters = new List<string>
                {
                    "-Name: App name pattern",
                    "-AllUsers: Gets apps for all users"
                },
                Tags = new List<string> { "apps", "modern-apps", "uwp" }
            },

            // Date/Time
            new()
            {
                Name = "Get-TimeZone",
                Synopsis = "Gets the current time zone or a list of available time zones",
                Description = "The Get-TimeZone cmdlet gets the current time zone configuration.",
                Syntax = "Get-TimeZone [[-Id] <String[]>]",
                Examples = new List<string>
                {
                    "Get-TimeZone",
                    "Get-TimeZone -ListAvailable",
                    "Get-TimeZone -Id 'Pacific Standard Time'"
                },
                Parameters = new List<string>
                {
                    "-Id: Time zone ID",
                    "-ListAvailable: Lists all available time zones"
                },
                Tags = new List<string> { "system", "time", "timezone" }
            },
            new()
            {
                Name = "Get-Date",
                Synopsis = "Gets the current date and time",
                Description = "The Get-Date cmdlet gets the current date/time and can format it in various ways.",
                Syntax = "Get-Date [-Format <String>] [-DisplayHint {Date | Time | DateTime}]",
                Examples = new List<string>
                {
                    "Get-Date",
                    "Get-Date -Format 'yyyy-MM-dd'",
                    "Get-Date -DisplayHint Time"
                },
                Parameters = new List<string>
                {
                    "-Format: Output format string",
                    "-DisplayHint: Display date, time, or both"
                },
                Tags = new List<string> { "system", "time", "date" }
            },

            // Registry
            new()
            {
                Name = "Get-ItemProperty",
                Synopsis = "Gets the properties of a specified item",
                Description = "The Get-ItemProperty cmdlet gets properties of items, commonly used for reading registry values.",
                Syntax = "Get-ItemProperty [-Path] <String[]> [[-Name] <String[]>]",
                Examples = new List<string>
                {
                    "Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion'",
                    "Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows NT\\CurrentVersion' -Name ProductName",
                    "(Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows NT\\CurrentVersion').ProductName"
                },
                Parameters = new List<string>
                {
                    "-Path: Registry path or file path",
                    "-Name: Property name"
                },
                Tags = new List<string> { "registry", "configuration", "system" }
            },

            // Data Processing
            new()
            {
                Name = "Select-Object",
                Synopsis = "Selects objects or object properties",
                Description = "The Select-Object cmdlet selects specified properties of objects or unique objects from an array.",
                Syntax = "Select-Object [[-Property] <Object[]>] [-First <Int32>] [-Last <Int32>] [-Unique]",
                Examples = new List<string>
                {
                    "Get-Process | Select-Object Name,CPU",
                    "Get-Service | Select-Object -First 10",
                    "Get-Process | Select-Object Name -Unique"
                },
                Parameters = new List<string>
                {
                    "-Property: Properties to select",
                    "-First: Select first N objects",
                    "-Unique: Select unique objects"
                },
                Tags = new List<string> { "pipeline", "filtering", "data" }
            },
            new()
            {
                Name = "Where-Object",
                Synopsis = "Selects objects from a collection based on property values",
                Description = "The Where-Object cmdlet filters objects in the pipeline based on conditions.",
                Syntax = "Where-Object [-FilterScript] <ScriptBlock>",
                Examples = new List<string>
                {
                    "Get-Service | Where-Object {$_.Status -eq 'Running'}",
                    "Get-Process | Where-Object {$_.CPU -gt 10}",
                    "Get-ChildItem | Where-Object {$_.Length -gt 1MB}"
                },
                Parameters = new List<string>
                {
                    "-FilterScript: Script block with filter conditions"
                },
                Tags = new List<string> { "pipeline", "filtering", "data" }
            },
            new()
            {
                Name = "Sort-Object",
                Synopsis = "Sorts objects by property values",
                Description = "The Sort-Object cmdlet sorts objects based on one or more properties.",
                Syntax = "Sort-Object [[-Property] <Object[]>] [-Descending] [-Unique]",
                Examples = new List<string>
                {
                    "Get-Process | Sort-Object CPU -Descending",
                    "Get-ChildItem | Sort-Object Length",
                    "Get-Service | Sort-Object Status,Name"
                },
                Parameters = new List<string>
                {
                    "-Property: Property names to sort by",
                    "-Descending: Sort in descending order"
                },
                Tags = new List<string> { "pipeline", "sorting", "data" }
            },
            new()
            {
                Name = "Measure-Object",
                Synopsis = "Calculates numeric properties of objects",
                Description = "The Measure-Object cmdlet calculates sums, averages, counts, min/max of object properties.",
                Syntax = "Measure-Object [[-Property] <String[]>] [-Sum] [-Average] [-Maximum] [-Minimum]",
                Examples = new List<string>
                {
                    "Get-Process | Measure-Object WorkingSet -Sum -Average",
                    "Get-ChildItem | Measure-Object -Property Length -Sum",
                    "1..100 | Measure-Object -Average -Maximum -Minimum"
                },
                Parameters = new List<string>
                {
                    "-Property: Property to measure",
                    "-Sum: Calculate sum",
                    "-Average: Calculate average"
                },
                Tags = new List<string> { "pipeline", "statistics", "data" }
            },

            // Output/Export
            new()
            {
                Name = "Export-Csv",
                Synopsis = "Converts objects into CSV strings and saves to file",
                Description = "The Export-Csv cmdlet creates CSV files from objects.",
                Syntax = "Export-Csv [-Path] <String> [-InputObject <PSObject>] [-NoTypeInformation]",
                Examples = new List<string>
                {
                    "Get-Process | Export-Csv C:\\Processes.csv -NoTypeInformation",
                    "Get-Service | Export-Csv C:\\Services.csv",
                    "Get-EventLog System -Newest 100 | Export-Csv C:\\Events.csv"
                },
                Parameters = new List<string>
                {
                    "-Path: Output file path",
                    "-NoTypeInformation: Omits type information header"
                },
                Tags = new List<string> { "export", "csv", "data" }
            },
            new()
            {
                Name = "Out-File",
                Synopsis = "Sends output to a file",
                Description = "The Out-File cmdlet sends output to a text file.",
                Syntax = "Out-File [-FilePath] <String> [-Append] [-Encoding <String>]",
                Examples = new List<string>
                {
                    "Get-Process | Out-File C:\\Processes.txt",
                    "Get-Service | Out-File C:\\Services.txt -Append",
                    "'Hello World' | Out-File C:\\test.txt -Encoding UTF8"
                },
                Parameters = new List<string>
                {
                    "-FilePath: Output file path",
                    "-Append: Adds to existing file",
                    "-Encoding: Character encoding"
                },
                Tags = new List<string> { "export", "file", "output" }
            }
        };
    }

    private List<(string Title, string Content, List<string> Tags)> GetWindowsAdminBestPractices()
    {
        return new List<(string, string, List<string>)>
        {
            (
                "PowerShell Best Practices for Remote Management",
                @"When managing remote Windows systems with PowerShell:

1. **Use WinRM (Windows Remote Management)**: Enable and configure WinRM on remote systems
2. **Prefer Invoke-Command over PSRemoting**: Use Invoke-Command for one-off commands
3. **Use CIM cmdlets over WMI**: CIM cmdlets (Get-CimInstance) are newer and more efficient
4. **Implement proper error handling**: Always use try-catch blocks for remote operations
5. **Use credential objects securely**: Never hardcode credentials in scripts
6. **Batch operations when possible**: Use -ComputerName parameter to target multiple systems
7. **Test connections first**: Use Test-WSMan before attempting remote operations",
                new List<string> { "best-practices", "remote-management", "powershell" }
            ),
            (
                "Disk Management and Monitoring Best Practices",
                @"Effective disk management prevents data loss and performance issues:

1. **Monitor disk space regularly**: Set alerts for 85% capacity
2. **Use Get-Volume for modern storage info**: Replaces older Get-WmiObject queries
3. **Check disk health with Get-Disk**: Look for OperationalStatus and HealthStatus properties
4. **Enable SMART monitoring**: Modern drives report health via SMART attributes
5. **Schedule regular disk cleanup**: Use Disk Cleanup or DISM for system cleanup
6. **Implement quota management**: Use FSRM (File Server Resource Manager) for quotas
7. **Monitor I/O performance**: Use Get-Counter with PhysicalDisk counters
8. **Plan for growth**: Maintain at least 15-20% free space for optimal performance",
                new List<string> { "best-practices", "disk", "storage", "monitoring" }
            ),
            (
                "Windows Update Management Best Practices",
                @"Proper update management ensures security while maintaining stability:

1. **Test updates in non-production first**: Never deploy untested updates to production
2. **Use WSUS or Configuration Manager**: Centralized update management for enterprises
3. **Establish maintenance windows**: Schedule updates during low-usage periods
4. **Document update baselines**: Know what updates are approved
5. **Monitor update status regularly**: Use Get-HotFix and Windows Update logs
6. **Keep update history**: Useful for troubleshooting post-update issues
7. **Enable automatic updates cautiously**: Balance security vs. change control
8. **Review update failures promptly**: Check WindowsUpdate.log and CBS.log
9. **Plan rollback procedures**: Know how to uninstall problematic updates",
                new List<string> { "best-practices", "updates", "patch-management" }
            ),
            (
                "Security Hardening Best Practices",
                @"Essential security practices for Windows systems:

1. **Implement principle of least privilege**: Users get minimum required permissions
2. **Enable Windows Defender/Antivirus**: Keep real-time protection active
3. **Configure Windows Firewall properly**: Default deny, explicit allow rules
4. **Disable unnecessary services**: Reduce attack surface
5. **Keep systems patched**: Apply security updates promptly
6. **Use strong local admin passwords**: Unique per system, managed centrally
7. **Enable audit logging**: Track security-relevant events
8. **Disable unnecessary protocols**: SMBv1, outdated TLS versions
9. **Use BitLocker for disk encryption**: Protect data at rest
10. **Regular security scans**: Use Microsoft Baseline Security Analyzer (MBSA)
11. **Implement account lockout policies**: Prevent brute force attacks
12. **Review event logs regularly**: Look for suspicious activity",
                new List<string> { "best-practices", "security", "hardening" }
            ),
            (
                "Backup and Recovery Best Practices",
                @"Comprehensive backup strategy protects against data loss:

1. **Follow 3-2-1 rule**: 3 copies, 2 different media types, 1 offsite
2. **Test restores regularly**: Backups are useless if you can't restore
3. **Document recovery procedures**: Step-by-step restoration guides
4. **Use Windows Server Backup or third-party tools**: Automated, scheduled backups
5. **Backup system state**: Includes Active Directory, Registry, system files
6. **Keep backup media secure**: Encrypt and store safely
7. **Monitor backup jobs**: Verify completion and success daily
8. **Maintain backup retention policy**: Balance storage costs vs. recovery needs
9. **Version important files**: Multiple restore points for critical data
10. **Practice disaster recovery**: Regular DR drills identify gaps",
                new List<string> { "best-practices", "backup", "disaster-recovery" }
            ),
            (
                "Event Log Management Best Practices",
                @"Effective event log management aids troubleshooting and security:

1. **Increase log sizes**: Default sizes too small for busy systems
2. **Archive logs regularly**: Move to central storage before overwriting
3. **Use event forwarding**: Centralize logs from multiple systems
4. **Create custom views**: Filter for specific event types quickly
5. **Set up event subscriptions**: Automated forwarding to SIEM or log server
6. **Monitor critical events**: Errors, audit failures, security events
7. **Clear logs only when archived**: Never delete without backup
8. **Use Get-WinEvent over Get-EventLog**: More powerful, supports newer logs
9. **Document important Event IDs**: Create reference for common issues
10. **Review security logs daily**: Watch for failed logons, privilege use",
                new List<string> { "best-practices", "event-logs", "monitoring", "troubleshooting" }
            ),
            (
                "Group Policy Best Practices",
                @"Efficient Group Policy management for domain environments:

1. **Plan OU structure carefully**: Aligns with policy application needs
2. **Use security filtering**: Target policies to specific users/computers
3. **Minimize loopback processing**: Use only when necessary
4. **Document all GPOs**: Maintain inventory with purpose and settings
5. **Test in lab first**: Never deploy untested policies to production
6. **Use WMI filters judiciously**: Can slow policy processing
7. **Disable unused policy sections**: Computer/User settings if not needed
8. **Regular GPO cleanup**: Remove obsolete policies
9. **Monitor policy application**: Use gpresult and Group Policy Results
10. **Backup GPOs regularly**: Before making changes
11. **Use starter GPOs**: Templates for consistent policy creation",
                new List<string> { "best-practices", "group-policy", "active-directory" }
            ),
            (
                "Scheduled Task Management Best Practices",
                @"Reliable scheduled task configuration and monitoring:

1. **Use service accounts**: Dedicated accounts with minimum required permissions
2. **Document task purposes**: Clear descriptions in task properties
3. **Set appropriate triggers**: Consider dependencies and timing
4. **Configure task history**: Enable for troubleshooting
5. **Test tasks manually first**: Verify execution before scheduling
6. **Set maximum run time**: Prevent hung tasks
7. **Configure retry behavior**: For transient failures
8. **Monitor task results**: Review last run result regularly
9. **Use error handling in scripts**: Proper exit codes for success/failure
10. **Avoid overlapping schedules**: Prevent resource contention
11. **Review task history regularly**: Check Task Scheduler Event Log",
                new List<string> { "best-practices", "scheduled-tasks", "automation" }
            ),
            (
                "Service Management Best Practices",
                @"When managing Windows services:

1. **Check dependencies before stopping services**: Use Get-Service -DependentServices
2. **Use -Force carefully**: Only use -Force when you understand the impact
3. **Verify service status after changes**: Always check that the service reached the desired state
4. **Set recovery options**: Configure service recovery options for critical services
5. **Use service accounts appropriately**: Don't run services as LocalSystem unless necessary
6. **Monitor service dependencies**: Understand service dependency chains
7. **Document service purposes**: Maintain documentation of custom service configurations",
                new List<string> { "best-practices", "service-management" }
            ),
            (
                "Performance Monitoring Best Practices",
                @"For effective Windows performance monitoring:

1. **Establish baselines**: Know your system's normal performance metrics
2. **Monitor key counters regularly**:
   - CPU: \\Processor(_Total)\\% Processor Time
   - Memory: \\Memory\\Available MBytes
   - Disk: \\PhysicalDisk(_Total)\\% Disk Time
3. **Use Performance Monitor (perfmon) for long-term data collection**
4. **Set up alerts for critical thresholds**: Configure performance alerts
5. **Review Event Viewer regularly**: Check System and Application logs
6. **Use Task Manager for quick checks**: Understand Task Manager metrics
7. **Consider third-party monitoring tools**: For enterprise environments",
                new List<string> { "best-practices", "performance", "monitoring" }
            ),
            (
                "Network Troubleshooting Best Practices",
                @"When troubleshooting network issues:

1. **Start with basic connectivity**: Use Test-Connection (ping) first
2. **Check DNS resolution**: Use nslookup or Resolve-DnsName
3. **Verify firewall rules**: Check Windows Firewall with Get-NetFirewallRule
4. **Test port connectivity**: Use Test-NetConnection -Port
5. **Check IP configuration**: Use Get-NetIPAddress and Get-NetIPConfiguration
6. **Review network adapter settings**: Check with Get-NetAdapter
7. **Capture network traces when needed**: Use netsh trace or Wireshark
8. **Document your findings**: Keep detailed notes of troubleshooting steps",
                new List<string> { "best-practices", "network", "troubleshooting" }
            )
        };
    }
}

// Request DTOs
public class RetrieveKnowledgeRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.7;
}

public class IngestCmdletRequest
{
    public string Name { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Syntax { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
    public List<string> Parameters { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class IngestBestPracticeRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class IngestTroubleshootingRequest
{
    public string Problem { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public List<string> Commands { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
