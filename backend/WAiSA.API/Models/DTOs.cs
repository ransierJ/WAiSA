namespace WAiSA.API.Models;

// Device DTOs
public class DeviceDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string ContextSummary { get; set; } = string.Empty;
    public int TotalInteractions { get; set; }
    public DateTime LastInteractionAt { get; set; }
    public DateTime? LastSummarizedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// Chat DTOs
public class ChatRequestDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
}

public class ChatResponseDto
{
    public string Message { get; set; } = string.Empty;
    public List<ExecutedCommandDto> ExecutedCommands { get; set; } = new();
    public List<ActivityLogDto> ActivityLogs { get; set; } = new();
    public bool Success { get; set; }
    public int TokensUsed { get; set; }
    public string? ContextSummary { get; set; }
    public List<KnowledgeReferenceDto> RelevantKnowledge { get; set; } = new();
    public Dictionary<string, object>? CascadeMetadata { get; set; }
}

public class ExecutedCommandDto
{
    public string Command { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
}

public class ActivityLogDto
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class KnowledgeReferenceDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
}

// Interaction DTOs
public class InteractionDto
{
    public string Id { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantResponse { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<ExecutedCommandDto> Commands { get; set; } = new();
    public int? FeedbackRating { get; set; }
}

// Feedback DTO
public class FeedbackDto
{
    public int Rating { get; set; } // 1-5 stars
    public string? Text { get; set; }
}

// Agent DTOs
public class AgentRegistrationRequestDto
{
    public string ComputerName { get; set; } = string.Empty;
    public string InstallationKey { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = "1.0.0";
}

public class AgentRegistrationResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid AgentId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

public class HeartbeatRequestDto
{
    public Guid AgentId { get; set; }
    public string Status { get; set; } = "Online";
    public SystemInformationDto? SystemInfo { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class HeartbeatResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool HasPendingCommands { get; set; }
}

public class SystemInformationDto
{
    public string ComputerName { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public TimeSpan SystemUptime { get; set; }
    public double CpuUsagePercent { get; set; }
    public long TotalMemoryMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public double MemoryUsagePercent { get; set; }
    public List<DiskInfoDto> Disks { get; set; } = new();
    public string IpAddress { get; set; } = string.Empty;
    public string DomainWorkgroup { get; set; } = string.Empty;
    public List<string> LoggedInUsers { get; set; } = new();
    public DateTime CollectedAt { get; set; }
}

public class DiskInfoDto
{
    public string DriveLetter { get; set; } = string.Empty;
    public long TotalSizeGB { get; set; }
    public long FreeSpaceGB { get; set; }
    public double UsagePercent { get; set; }
    public string VolumeLabel { get; set; } = string.Empty;
}

public class CommandRequestDto
{
    public Guid CommandId { get; set; }
    public string Command { get; set; } = string.Empty;
    public string ExecutionContext { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
}

public class CommandResponseDto
{
    public Guid CommandId { get; set; }
    public Guid AgentId { get; set; }
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double ExecutionTimeSeconds { get; set; }
}

public class PendingCommandsResponseDto
{
    public List<CommandRequestDto> Commands { get; set; } = new();
}

public class AgentDto
{
    public Guid AgentId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastHeartbeat { get; set; }
    public DateTime InstallDate { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? OsVersion { get; set; }
    public SystemInformationDto? LastSystemInfo { get; set; }
    public bool IsEnabled { get; set; }
}

// Agent Chat History DTOs
public class AgentChatHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<WAiSA.Shared.Models.ExecutedCommand> Commands { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public int? TokensUsed { get; set; }
    public bool Success { get; set; }
}
