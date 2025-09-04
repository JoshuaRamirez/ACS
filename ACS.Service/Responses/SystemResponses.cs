namespace ACS.Service.Responses;

/// <summary>
/// Service response for system overview information
/// </summary>
public record SystemOverviewResponse
{
    public SystemOverviewData? Data { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for migration history information
/// </summary>
public record MigrationHistoryResponse
{
    public ICollection<MigrationData> Migrations { get; init; } = new List<MigrationData>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for system diagnostic information
/// </summary>
public record SystemDiagnosticsResponse
{
    public SystemDiagnosticsData? Data { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// System overview data transfer object
/// </summary>
public record SystemOverviewData
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = "Healthy";
    public int UsersCount { get; init; }
    public int GroupsCount { get; init; }
    public int RolesCount { get; init; }
    public TimeSpan Uptime { get; init; }
}

/// <summary>
/// Migration data transfer object
/// </summary>
public record MigrationData
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime AppliedDate { get; init; }
    public string Status { get; init; } = "Applied";
}

/// <summary>
/// System diagnostics data transfer object
/// </summary>
public record SystemDiagnosticsData
{
    public string MachineName { get; init; } = string.Empty;
    public string ProcessId { get; init; } = string.Empty;
    public long WorkingSetMemory { get; init; }
    public TimeSpan ProcessorTime { get; init; }
    public DateTime StartTime { get; init; }
    public string Version { get; init; } = string.Empty;
}