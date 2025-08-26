namespace ACS.Infrastructure.Security;

/// <summary>
/// Configuration options for key management services
/// </summary>
public class KeyManagementOptions
{
    public string BaseDirectory { get; set; } = "keys";
    public string MasterKeyFile { get; set; } = "master.key";
    public bool UseEncryption { get; set; } = true;
    public int KeyRotationDays { get; set; } = 90;
    public string BackupDirectory { get; set; } = "key-backups";
    public bool EnableAuditing { get; set; } = true;
    public int MaxKeyVersions { get; set; } = 10;
}