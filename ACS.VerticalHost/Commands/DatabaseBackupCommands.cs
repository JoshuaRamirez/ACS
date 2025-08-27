using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Database Backup Commands  
public class CreateBackupCommand : ICommand<BackupResult>
{
    public BackupType BackupType { get; set; } = BackupType.Full;
    public string BackupPath { get; set; } = string.Empty;
    public bool Compress { get; set; } = true;
    public bool UseNativeCompression { get; set; } = true;
    public bool DeleteUncompressedAfterCompress { get; set; } = true;
    public bool VerifyAfterBackup { get; set; } = true;
    public bool CopyOnly { get; set; } = false;
    public int? TimeoutSeconds { get; set; }
}

public class RestoreBackupCommand : ICommand<RestoreResult>
{
    public string BackupPath { get; set; } = string.Empty;
    public string TargetDatabaseName { get; set; } = string.Empty;
    public string DataFilePath { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public bool ForceRestore { get; set; } = false;
    public bool NoRecovery { get; set; } = false;
    public bool VerifyBeforeRestore { get; set; } = true;
    public int? TimeoutSeconds { get; set; }
    public bool ConfirmRestore { get; set; } = false;
}

public class VerifyBackupCommand : ICommand<bool>
{
    public string BackupPath { get; set; } = string.Empty;
}

public class CleanupOldBackupsCommand : ICommand<CleanupResult>
{
    public int RetentionDays { get; set; } = 30;
}

// Database Backup Queries
public class GetBackupHistoryQuery : IQuery<List<BackupHistoryInfo>>
{
    public int Days { get; set; } = 30;
}

public class GetBackupConfigurationQuery : IQuery<BackupConfiguration>
{
}

// Result Types
public class BackupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public BackupType BackupType { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string CompressedPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long? CompressedSizeBytes { get; set; }
    public bool IsVerified { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class RestoreResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string RestoredDatabaseName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class CleanupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> FailedFiles { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class BackupHistoryInfo
{
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime BackupStartDate { get; set; }
    public DateTime BackupFinishDate { get; set; }
    public BackupType BackupType { get; set; }
    public long BackupSizeBytes { get; set; }
    public long? CompressedSizeBytes { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
}

public class BackupConfiguration
{
    public string DefaultPath { get; set; } = string.Empty;
    public BackupScheduleConfiguration Schedule { get; set; } = new();
}

public class BackupScheduleConfiguration
{
    public bool Enabled { get; set; }
    public object? FullBackup { get; set; }
    public object? DifferentialBackup { get; set; }
    public object? TransactionLogBackup { get; set; }
    public object? Cleanup { get; set; }
}

public enum BackupType
{
    Full = 0,
    Differential = 1,
    TransactionLog = 2
}