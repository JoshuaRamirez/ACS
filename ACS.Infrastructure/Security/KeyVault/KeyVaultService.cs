using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ACS.Infrastructure.Security.KeyVault;

/// <summary>
/// Azure Key Vault implementation of IKeyVaultService
/// </summary>
public class KeyVaultService : IKeyVaultService
{
    private readonly KeyVaultOptions _options;
    private readonly ILogger<KeyVaultService> _logger;
    private readonly IMemoryCache _cache;
    private readonly SecretClient? _secretClient;
    private readonly CertificateClient? _certificateClient;
    private readonly IAsyncPolicy<Azure.Response<KeyVaultSecret>> _retryPolicy;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public KeyVaultService(
        IOptions<KeyVaultOptions> options,
        ILogger<KeyVaultService> logger,
        IMemoryCache cache)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        if (!_options.UseLocalDevelopmentMode && !string.IsNullOrEmpty(_options.VaultUri))
        {
            var credential = GetTokenCredential();
            _secretClient = new SecretClient(new Uri(_options.VaultUri), credential);
            _certificateClient = new CertificateClient(new Uri(_options.VaultUri), credential);
        }

        _retryPolicy = CreateRetryPolicy();
    }

    private TokenCredential GetTokenCredential()
    {
        if (_options.UseManagedIdentity)
        {
            _logger.LogInformation("Using Managed Identity for Key Vault authentication");
            return new DefaultAzureCredential();
        }

        if (!string.IsNullOrEmpty(_options.ClientId) &&
            !string.IsNullOrEmpty(_options.ClientSecret) &&
            !string.IsNullOrEmpty(_options.TenantId))
        {
            _logger.LogInformation("Using Client Secret credential for Key Vault authentication");
            return new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret);
        }

        _logger.LogInformation("Using Default Azure Credential for Key Vault authentication");
        return new DefaultAzureCredential();
    }

    private IAsyncPolicy<Response<KeyVaultSecret>> CreateRetryPolicy()
    {
        var retryPolicy = Policy
            .HandleResult<Azure.Response<KeyVaultSecret>>(r => !IsSuccessStatusCode(r.GetRawResponse().Status))
            .OrResult(r => r.Value == null)
            .WaitAndRetryAsync(
                _options.RetryPolicy.MaxRetries,
                retryAttempt =>
                {
                    if (_options.RetryPolicy.UseExponentialBackoff)
                    {
                        var delay = TimeSpan.FromMilliseconds(
                            Math.Min(
                                _options.RetryPolicy.InitialDelayMilliseconds * Math.Pow(2, retryAttempt - 1),
                                _options.RetryPolicy.MaxDelayMilliseconds));
                        return delay;
                    }
                    return TimeSpan.FromMilliseconds(_options.RetryPolicy.InitialDelayMilliseconds);
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}ms for Key Vault operation",
                        retryCount, timespan.TotalMilliseconds);
                });

        return retryPolicy;
    }

    private static bool IsSuccessStatusCode(int statusCode) => statusCode >= 200 && statusCode < 300;

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullSecretName = GetFullSecretName(secretName);

            // Check cache first
            if (_options.EnableCaching)
            {
                var cacheKey = GetCacheKey(fullSecretName);
                if (_cache.TryGetValue<string>(cacheKey, out var cachedValue))
                {
                    _logger.LogDebug("Secret {SecretName} retrieved from cache", secretName);
                    return cachedValue;
                }
            }

            // Local development mode
            if (_options.UseLocalDevelopmentMode)
            {
                return await GetLocalSecretAsync(fullSecretName, cancellationToken);
            }

            if (_secretClient == null)
            {
                _logger.LogError("Secret client is not configured");
                return null;
            }

            // Retrieve from Key Vault
            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _secretClient.GetSecretAsync(fullSecretName, cancellationToken: cancellationToken));

            if (response?.Value?.Value == null)
            {
                _logger.LogWarning("Secret {SecretName} not found in Key Vault", secretName);
                return null;
            }

            var secretValue = response.Value.Value;

            // Cache the secret
            if (_options.EnableCaching)
            {
                await CacheSecretAsync(fullSecretName, secretValue);
            }

            _logger.LogDebug("Secret {SecretName} retrieved from Key Vault", secretName);
            return secretValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret {SecretName}", secretName);
            return null;
        }
    }

    public async Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullSecretName = GetFullSecretName(secretName);

            // Local development mode
            if (_options.UseLocalDevelopmentMode)
            {
                return await SetLocalSecretAsync(fullSecretName, secretValue, cancellationToken);
            }

            if (_secretClient == null)
            {
                _logger.LogError("Secret client is not configured");
                return false;
            }

            var secret = new KeyVaultSecret(fullSecretName, secretValue);
            secret.Properties.ExpiresOn = DateTimeOffset.UtcNow.AddYears(1);

            await _secretClient.SetSecretAsync(secret, cancellationToken);

            // Invalidate cache
            if (_options.EnableCaching)
            {
                InvalidateCache(fullSecretName);
            }

            _logger.LogInformation("Secret {SecretName} updated in Key Vault", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting secret {SecretName}", secretName);
            return false;
        }
    }

    public async Task<bool> DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullSecretName = GetFullSecretName(secretName);

            // Local development mode
            if (_options.UseLocalDevelopmentMode)
            {
                return await DeleteLocalSecretAsync(fullSecretName, cancellationToken);
            }

            if (_secretClient == null)
            {
                _logger.LogError("Secret client is not configured");
                return false;
            }

            var operation = await _secretClient.StartDeleteSecretAsync(fullSecretName, cancellationToken);
            await operation.WaitForCompletionAsync(cancellationToken);

            // Invalidate cache
            if (_options.EnableCaching)
            {
                InvalidateCache(fullSecretName);
            }

            _logger.LogInformation("Secret {SecretName} deleted from Key Vault", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secret {SecretName}", secretName);
            return false;
        }
    }

    public async Task<X509Certificate2?> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_options.UseLocalDevelopmentMode)
            {
                return await GetLocalCertificateAsync(certificateName, cancellationToken);
            }

            if (_certificateClient == null)
            {
                _logger.LogError("Certificate client is not configured");
                return null;
            }

            var response = await _certificateClient.GetCertificateAsync(certificateName, cancellationToken);
            if (response?.Value == null)
            {
                _logger.LogWarning("Certificate {CertificateName} not found", certificateName);
                return null;
            }

            return X509CertificateLoader.LoadCertificate(response.Value.Cer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificate {CertificateName}", certificateName);
            return null;
        }
    }

    public async Task<bool> StoreCertificateAsync(string certificateName, X509Certificate2 certificate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_options.UseLocalDevelopmentMode)
            {
                return await StoreLocalCertificateAsync(certificateName, certificate, cancellationToken);
            }

            if (_certificateClient == null)
            {
                _logger.LogError("Certificate client is not configured");
                return false;
            }

            var certificatePolicy = new CertificatePolicy("Self", "CN=" + certificateName);
            var operation = await _certificateClient.StartCreateCertificateAsync(certificateName, certificatePolicy, cancellationToken: cancellationToken);
            await operation.WaitForCompletionAsync(cancellationToken);

            _logger.LogInformation("Certificate {CertificateName} stored in Key Vault", certificateName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing certificate {CertificateName}", certificateName);
            return false;
        }
    }

    public async Task<string?> GetConnectionStringAsync(string name, CancellationToken cancellationToken = default)
    {
        var secretName = $"ConnectionString-{name}";
        return await GetSecretAsync(secretName, cancellationToken);
    }

    public async Task<string?> GetApiKeyAsync(string keyName, CancellationToken cancellationToken = default)
    {
        var secretName = $"ApiKey-{keyName}";
        return await GetSecretAsync(secretName, cancellationToken);
    }

    public async Task<string> RotateSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate new secret value
            var newSecretValue = GenerateSecretValue();

            // Store new secret
            var success = await SetSecretAsync(secretName, newSecretValue, cancellationToken);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to rotate secret {secretName}");
            }

            _logger.LogInformation("Secret {SecretName} rotated successfully", secretName);
            return newSecretValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating secret {SecretName}", secretName);
            throw;
        }
    }

    public async Task<IEnumerable<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_options.UseLocalDevelopmentMode)
            {
                return await ListLocalSecretNamesAsync(cancellationToken);
            }

            if (_secretClient == null)
            {
                _logger.LogError("Secret client is not configured");
                return Enumerable.Empty<string>();
            }

            var secretNames = new List<string>();
            await foreach (var secretProperties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                if (_options.SecretNamePrefix != null)
                {
                    if (secretProperties.Name.StartsWith(_options.SecretNamePrefix))
                    {
                        secretNames.Add(secretProperties.Name.Substring(_options.SecretNamePrefix.Length));
                    }
                }
                else
                {
                    secretNames.Add(secretProperties.Name);
                }
            }

            return secretNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing secret names");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await GetSecretAsync(secretName, cancellationToken);
            return secret != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullSecretName = GetFullSecretName(secretName);

            if (_options.UseLocalDevelopmentMode)
            {
                return await GetLocalSecretMetadataAsync(fullSecretName, cancellationToken);
            }

            if (_secretClient == null)
            {
                _logger.LogError("Secret client is not configured");
                return null;
            }

            var response = await _secretClient.GetSecretAsync(fullSecretName, cancellationToken: cancellationToken);
            if (response?.Value == null)
            {
                return null;
            }

            var properties = response.Value.Properties;
            return new SecretMetadata
            {
                Name = secretName,
                CreatedDate = properties.CreatedOn?.DateTime ?? DateTime.MinValue,
                UpdatedDate = properties.UpdatedOn?.DateTime,
                ExpirationDate = properties.ExpiresOn?.DateTime,
                Enabled = properties.Enabled ?? false,
                ContentType = properties.ContentType,
                Tags = properties.Tags?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, string>(),
                Version = properties.Version
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting secret metadata for {SecretName}", secretName);
            return null;
        }
    }

    #region Private Helper Methods

    private string GetFullSecretName(string secretName)
    {
        if (!string.IsNullOrEmpty(_options.SecretNamePrefix))
        {
            return $"{_options.SecretNamePrefix}{secretName}";
        }
        return secretName;
    }

    private string GetCacheKey(string secretName) => $"KeyVault:{secretName}";

    private async Task CacheSecretAsync(string secretName, string secretValue)
    {
        await _cacheLock.WaitAsync();
        try
        {
            var cacheKey = GetCacheKey(secretName);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheDurationMinutes)
            };
            _cache.Set(cacheKey, secretValue, cacheOptions);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private void InvalidateCache(string secretName)
    {
        var cacheKey = GetCacheKey(secretName);
        _cache.Remove(cacheKey);
    }

    private static string GenerateSecretValue()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    #endregion

    #region Local Development Methods

    private readonly Dictionary<string, string> _localSecrets = new();
    private readonly SemaphoreSlim _localSecretsLock = new(1, 1);

    private async Task<string?> GetLocalSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        await LoadLocalSecretsAsync(cancellationToken);
        return _localSecrets.TryGetValue(secretName, out var value) ? value : null;
    }

    private async Task<bool> SetLocalSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
    {
        await _localSecretsLock.WaitAsync(cancellationToken);
        try
        {
            _localSecrets[secretName] = secretValue;
            await SaveLocalSecretsAsync(cancellationToken);
            return true;
        }
        finally
        {
            _localSecretsLock.Release();
        }
    }

    private async Task<bool> DeleteLocalSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        await _localSecretsLock.WaitAsync(cancellationToken);
        try
        {
            if (_localSecrets.Remove(secretName))
            {
                await SaveLocalSecretsAsync(cancellationToken);
                return true;
            }
            return false;
        }
        finally
        {
            _localSecretsLock.Release();
        }
    }

    private async Task<X509Certificate2?> GetLocalCertificateAsync(string certificateName, CancellationToken cancellationToken)
    {
        var certPath = Path.Combine(Path.GetDirectoryName(_options.LocalSecretsFilePath) ?? "", $"{certificateName}.pfx");
        if (File.Exists(certPath))
        {
            var certBytes = await File.ReadAllBytesAsync(certPath, cancellationToken);
            return X509CertificateLoader.LoadCertificate(certBytes);
        }
        return null;
    }

    private async Task<bool> StoreLocalCertificateAsync(string certificateName, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        var certPath = Path.Combine(Path.GetDirectoryName(_options.LocalSecretsFilePath) ?? "", $"{certificateName}.pfx");
        var certBytes = certificate.Export(X509ContentType.Pfx);
        await File.WriteAllBytesAsync(certPath, certBytes, cancellationToken);
        return true;
    }

    private async Task<IEnumerable<string>> ListLocalSecretNamesAsync(CancellationToken cancellationToken)
    {
        await LoadLocalSecretsAsync(cancellationToken);
        return _localSecrets.Keys;
    }

    private async Task<SecretMetadata?> GetLocalSecretMetadataAsync(string secretName, CancellationToken cancellationToken)
    {
        await LoadLocalSecretsAsync(cancellationToken);
        if (_localSecrets.ContainsKey(secretName))
        {
            var fileInfo = new FileInfo(_options.LocalSecretsFilePath);
            return new SecretMetadata
            {
                Name = secretName,
                CreatedDate = fileInfo.CreationTimeUtc,
                UpdatedDate = fileInfo.LastWriteTimeUtc,
                Enabled = true,
                ContentType = "text/plain",
                Version = "local"
            };
        }
        return null;
    }

    private async Task LoadLocalSecretsAsync(CancellationToken cancellationToken)
    {
        await _localSecretsLock.WaitAsync(cancellationToken);
        try
        {
            if (_localSecrets.Count == 0 && File.Exists(_options.LocalSecretsFilePath))
            {
                var json = await File.ReadAllTextAsync(_options.LocalSecretsFilePath, cancellationToken);
                var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (secrets != null)
                {
                    foreach (var kvp in secrets)
                    {
                        _localSecrets[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        finally
        {
            _localSecretsLock.Release();
        }
    }

    private async Task SaveLocalSecretsAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_localSecrets, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_options.LocalSecretsFilePath, json, cancellationToken);
    }

    #endregion
}