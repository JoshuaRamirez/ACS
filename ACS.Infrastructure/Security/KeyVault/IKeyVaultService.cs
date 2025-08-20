using System.Security.Cryptography.X509Certificates;

namespace ACS.Infrastructure.Security.KeyVault;

/// <summary>
/// Service for managing secrets, certificates, and keys in a secure vault
/// </summary>
public interface IKeyVaultService
{
    /// <summary>
    /// Get a secret value by name
    /// </summary>
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set or update a secret value
    /// </summary>
    Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a secret
    /// </summary>
    Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a certificate by name
    /// </summary>
    Task<X509Certificate2?> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a certificate
    /// </summary>
    Task<bool> StoreCertificateAsync(string certificateName, X509Certificate2 certificate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get connection string from vault
    /// </summary>
    Task<string?> GetConnectionStringAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get API key from vault
    /// </summary>
    Task<string?> GetApiKeyAsync(string keyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotate a secret (create new version)
    /// </summary>
    Task<string> RotateSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all secret names (not values)
    /// </summary>
    Task<IEnumerable<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a secret exists
    /// </summary>
    Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get secret metadata (without value)
    /// </summary>
    Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about a secret
/// </summary>
public class SecretMetadata
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool Enabled { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Key vault configuration options
/// </summary>
public class KeyVaultOptions
{
    /// <summary>
    /// Vault URI (e.g., https://myvault.vault.azure.net)
    /// </summary>
    public string? VaultUri { get; set; }

    /// <summary>
    /// Client ID for authentication
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for authentication
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Tenant ID for Azure AD
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Use managed identity for authentication
    /// </summary>
    public bool UseManagedIdentity { get; set; } = false;

    /// <summary>
    /// Enable caching of secrets
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration for secrets in minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Retry policy settings
    /// </summary>
    public RetryPolicyOptions RetryPolicy { get; set; } = new();

    /// <summary>
    /// Secret name prefix for multi-tenant scenarios
    /// </summary>
    public string? SecretNamePrefix { get; set; }

    /// <summary>
    /// Enable local development mode (uses local secrets)
    /// </summary>
    public bool UseLocalDevelopmentMode { get; set; } = false;

    /// <summary>
    /// Local development secrets file path
    /// </summary>
    public string LocalSecretsFilePath { get; set; } = "appsettings.secrets.json";
}

/// <summary>
/// Retry policy options
/// </summary>
public class RetryPolicyOptions
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelayMilliseconds { get; set; } = 1000;
    public int MaxDelayMilliseconds { get; set; } = 30000;
    public bool UseExponentialBackoff { get; set; } = true;
}