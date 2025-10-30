namespace WAiSA.Shared.Configuration;

/// <summary>
/// Configuration options for Azure OpenAI service
/// </summary>
public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API key (retrieved from Key Vault)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// GPT-4 deployment name
    /// </summary>
    public string ChatDeploymentName { get; set; } = "gpt-4";

    /// <summary>
    /// Text embedding deployment name
    /// </summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-large";

    /// <summary>
    /// Maximum tokens for chat completions
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for chat completions (0.0 to 2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
