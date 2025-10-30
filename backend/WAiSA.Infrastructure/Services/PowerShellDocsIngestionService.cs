using WAiSA.Core.Interfaces;
using WAiSA.Shared.Models;
using Microsoft.Extensions.Logging;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Service for ingesting PowerShell cmdlet documentation into the knowledge base
/// </summary>
public class PowerShellDocsIngestionService
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<PowerShellDocsIngestionService> _logger;

    public PowerShellDocsIngestionService(
        IKnowledgeBaseService knowledgeBaseService,
        ILogger<PowerShellDocsIngestionService> logger)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _logger = logger;
    }

    /// <summary>
    /// Ingest a PowerShell cmdlet documentation entry
    /// </summary>
    public async Task IngestCmdletDocumentationAsync(
        string cmdletName,
        string synopsis,
        string description,
        string syntax,
        List<string> examples,
        List<string> parameters,
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = BuildCmdletDocumentation(
                cmdletName,
                synopsis,
                description,
                syntax,
                examples,
                parameters);

            var entry = new KnowledgeBaseEntry
            {
                Id = $"cmdlet-{cmdletName.ToLower()}",
                Title = $"PowerShell Cmdlet: {cmdletName}",
                Content = content,
                Source = "powershell-documentation",
                Tags = new List<string> { "powershell", "cmdlet" }.Concat(tags).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _knowledgeBaseService.AddOrUpdateKnowledgeAsync(entry, cancellationToken);

            _logger.LogInformation("Ingested PowerShell cmdlet documentation: {CmdletName}", cmdletName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting cmdlet documentation for {CmdletName}", cmdletName);
            throw;
        }
    }

    /// <summary>
    /// Bulk ingest multiple cmdlet documentation entries
    /// </summary>
    public async Task BulkIngestCmdletsAsync(
        List<CmdletDocumentation> cmdlets,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting bulk ingest of {Count} cmdlets", cmdlets.Count);

        var successCount = 0;
        var failCount = 0;

        foreach (var cmdlet in cmdlets)
        {
            try
            {
                await IngestCmdletDocumentationAsync(
                    cmdlet.Name,
                    cmdlet.Synopsis,
                    cmdlet.Description,
                    cmdlet.Syntax,
                    cmdlet.Examples,
                    cmdlet.Parameters,
                    cmdlet.Tags,
                    cancellationToken);

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest cmdlet: {CmdletName}", cmdlet.Name);
                failCount++;
            }
        }

        _logger.LogInformation(
            "Bulk ingest complete. Success: {SuccessCount}, Failed: {FailCount}",
            successCount,
            failCount);
    }

    /// <summary>
    /// Ingest Windows administration best practices
    /// </summary>
    public async Task IngestBestPracticeAsync(
        string title,
        string content,
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new KnowledgeBaseEntry
            {
                Title = title,
                Content = content,
                Source = "best-practices",
                Tags = new List<string> { "best-practices" }.Concat(tags).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _knowledgeBaseService.AddOrUpdateKnowledgeAsync(entry, cancellationToken);

            _logger.LogInformation("Ingested best practice: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting best practice: {Title}", title);
            throw;
        }
    }

    /// <summary>
    /// Ingest common troubleshooting scenarios
    /// </summary>
    public async Task IngestTroubleshootingScenarioAsync(
        string problem,
        string solution,
        List<string> commands,
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = $@"**Problem:** {problem}

**Solution:** {solution}

**Commands:**
{string.Join("\n", commands.Select(c => $"```powershell\n{c}\n```"))}";

            var entry = new KnowledgeBaseEntry
            {
                Title = $"Troubleshooting: {problem}",
                Content = content,
                Source = "troubleshooting",
                Tags = new List<string> { "troubleshooting", "solution" }.Concat(tags).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _knowledgeBaseService.AddOrUpdateKnowledgeAsync(entry, cancellationToken);

            _logger.LogInformation("Ingested troubleshooting scenario: {Problem}", problem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting troubleshooting scenario: {Problem}", problem);
            throw;
        }
    }

    private string BuildCmdletDocumentation(
        string cmdletName,
        string synopsis,
        string description,
        string syntax,
        List<string> examples,
        List<string> parameters)
    {
        var doc = new System.Text.StringBuilder();

        doc.AppendLine($"# {cmdletName}");
        doc.AppendLine();
        doc.AppendLine($"**Synopsis:** {synopsis}");
        doc.AppendLine();
        doc.AppendLine("## Description");
        doc.AppendLine(description);
        doc.AppendLine();
        doc.AppendLine("## Syntax");
        doc.AppendLine("```powershell");
        doc.AppendLine(syntax);
        doc.AppendLine("```");
        doc.AppendLine();

        if (parameters.Any())
        {
            doc.AppendLine("## Parameters");
            foreach (var param in parameters)
            {
                doc.AppendLine($"- {param}");
            }
            doc.AppendLine();
        }

        if (examples.Any())
        {
            doc.AppendLine("## Examples");
            for (int i = 0; i < examples.Count; i++)
            {
                doc.AppendLine($"### Example {i + 1}");
                doc.AppendLine("```powershell");
                doc.AppendLine(examples[i]);
                doc.AppendLine("```");
                doc.AppendLine();
            }
        }

        return doc.ToString();
    }
}

/// <summary>
/// Cmdlet documentation model for bulk ingestion
/// </summary>
public class CmdletDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Syntax { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
    public List<string> Parameters { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
