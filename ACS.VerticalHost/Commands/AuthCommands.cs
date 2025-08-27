using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Authentication Commands
public class LoginCommand : ACS.VerticalHost.Services.ICommand<AuthResult>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

public class RefreshTokenCommand : ACS.VerticalHost.Services.ICommand<AuthResult>
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class ChangePasswordCommand : ACS.VerticalHost.Services.ICommand<bool>
{
    public int UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

// Dashboard Commands
public class GetSystemOverviewQuery : ACS.VerticalHost.Services.IQuery<SystemOverview>
{
    public string TenantId { get; set; } = string.Empty;
}

public class GetHealthStatusQuery : ACS.VerticalHost.Services.IQuery<HealthStatus>
{
    public string TenantId { get; set; } = string.Empty;
}

// Migration Commands  
public class GetMigrationHistoryQuery : ACS.VerticalHost.Services.IQuery<List<MigrationInfo>>
{
    public string TenantId { get; set; } = string.Empty;
}

public class ValidateMigrationsCommand : ACS.VerticalHost.Services.ICommand<MigrationValidationResult>
{
    public string TenantId { get; set; } = string.Empty;
}

// Diagnostics Commands
public class GetSystemInfoQuery : ACS.VerticalHost.Services.IQuery<SystemDiagnosticInfo>
{
    public string TenantId { get; set; } = string.Empty;
}

// Result Types
public class AuthResult
{
    public bool IsSuccess { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public double ExpiresIn { get; set; }
}

public class SystemOverview
{
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public int UsersCount { get; set; }
    public int GroupsCount { get; set; }
    public int RolesCount { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class HealthStatus
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; }
    public TimeSpan Uptime { get; set; }
    public string Environment { get; set; } = string.Empty;
}

public class MigrationInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime AppliedDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class MigrationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
}

public class SystemDiagnosticInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string ProcessId { get; set; } = string.Empty;
    public long WorkingSetMemory { get; set; }
    public TimeSpan ProcessorTime { get; set; }
    public DateTime StartTime { get; set; }
    public string Version { get; set; } = string.Empty;
}