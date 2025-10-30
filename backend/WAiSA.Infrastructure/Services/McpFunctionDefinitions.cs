using Azure.AI.OpenAI;
using System.Text.Json;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// MCP function definitions for Azure OpenAI function calling
/// </summary>
public static class McpFunctionDefinitions
{
    /// <summary>
    /// Get all MCP tool function definitions
    /// </summary>
    public static List<FunctionDefinition> GetFunctionDefinitions()
    {
        return new List<FunctionDefinition>
        {
            GetSearchDocumentationFunction(),
            GetSearchWebFunction()
        };
    }

    /// <summary>
    /// Microsoft Learn documentation search function
    /// </summary>
    private static FunctionDefinition GetSearchDocumentationFunction()
    {
        return new FunctionDefinition
        {
            Name = "search_microsoft_docs",
            Description = @"Search Microsoft Learn documentation for Windows, PowerShell, Azure, and .NET topics.
Use this when the user asks about Windows administration, PowerShell commands, system configuration,
or needs documentation for Microsoft technologies. Returns relevant documentation articles with titles,
URLs, and descriptions.",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "Search query (e.g., 'PowerShell Get-EventLog', 'Windows Event Viewer', 'Active Directory management')"
                    },
                    maxResults = new
                    {
                        type = "integer",
                        description = "Maximum number of results to return (default: 5, max: 10)",
                        @default = 5
                    }
                },
                required = new[] { "query" }
            })
        };
    }

    /// <summary>
    /// Microsoft Learn documentation fetch function
    /// </summary>
    private static FunctionDefinition GetFetchDocumentationFunction()
    {
        return new FunctionDefinition
        {
            Name = "fetch_microsoft_docs",
            Description = @"Fetch full content from a specific Microsoft Learn documentation page.
Use this when you need detailed information from a specific documentation URL found in search results.
Returns the full text content of the documentation page.",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    url = new
                    {
                        type = "string",
                        description = "Full URL of the Microsoft Learn documentation page to fetch"
                    }
                },
                required = new[] { "url" }
            })
        };
    }

    /// <summary>
    /// Web search function for general troubleshooting
    /// </summary>
    private static FunctionDefinition GetSearchWebFunction()
    {
        return new FunctionDefinition
        {
            Name = "search_web",
            Description = @"Search the web for troubleshooting information, community solutions, and additional context.
IMPORTANT: Only use this AFTER searching Microsoft Learn documentation first. This provides broader web results
including forums, blogs, and community discussions. Use when official Microsoft docs don't have enough information.",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "Web search query (e.g., 'Adobe Acrobat crash Windows 11', 'PowerShell error handling best practices')"
                    },
                    maxResults = new
                    {
                        type = "integer",
                        description = "Maximum number of results to return (default: 5, max: 10)",
                        @default = 5
                    }
                },
                required = new[] { "query" }
            })
        };
    }

    /// <summary>
    /// Parse function call arguments
    /// </summary>
    public static T? ParseFunctionArguments<T>(string argumentsJson)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<T>(argumentsJson, options);
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
/// Arguments for search_microsoft_docs function
/// </summary>
public class SearchDocsArguments
{
    public string Query { get; set; } = string.Empty;
    public int? MaxResults { get; set; } = 5;
}

/// <summary>
/// Arguments for search_web function
/// </summary>
public class WebSearchArguments
{
    public string Query { get; set; } = string.Empty;
    public int? MaxResults { get; set; } = 5;
}
