using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace ACS.Infrastructure.Optimization;

/// <summary>
/// Implementation of bundling service for static assets
/// </summary>
public class BundlingService : IBundlingService
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BundlingService> _logger;
    private readonly IMinificationService _minificationService;
    private readonly IMemoryCache _cache;
    private readonly IFileProvider _fileProvider;
    private readonly BundlingSettings _settings;
    private readonly Dictionary<string, Bundle> _bundles = new();

    public BundlingService(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<BundlingService> logger,
        IMinificationService minificationService,
        IMemoryCache cache,
        IFileProvider fileProvider)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _minificationService = minificationService;
        _cache = cache;
        _fileProvider = fileProvider;
        _settings = LoadBundlingSettings();
    }

    public async Task<Bundle> CreateBundleAsync(string name, IEnumerable<string> filePaths, BundleType type)
    {
        var bundle = new Bundle
        {
            Name = name,
            Type = type,
            Files = filePaths.ToList(),
            CreatedAt = DateTime.UtcNow
        };

        var contentBuilder = new StringBuilder();
        long originalSize = 0;

        foreach (var filePath in filePaths)
        {
            var fileInfo = _fileProvider.GetFileInfo(filePath);
            if (!fileInfo.Exists)
            {
                _logger.LogWarning("File not found for bundling: {FilePath}", filePath);
                continue;
            }

            using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            
            originalSize += content.Length;

            // Add file separator comment (only in development)
            if (_environment.IsDevelopment() && !_settings.MinifyInDevelopment)
            {
                contentBuilder.AppendLine($"\n/* File: {filePath} */");
            }

            contentBuilder.AppendLine(content);
        }

        bundle.Content = contentBuilder.ToString();
        bundle.OriginalSize = originalSize;

        // Minify if enabled
        if (ShouldMinify())
        {
            bundle.Content = await MinifyContentAsync(bundle.Content, type);
            bundle.IsMinified = true;
        }

        bundle.BundledSize = bundle.Content.Length;
        bundle.Hash = ComputeHash(bundle.Content);

        // Cache the bundle
        var cacheKey = GetBundleCacheKey(name);
        _cache.Set(cacheKey, bundle, TimeSpan.FromMinutes(_settings.CacheMinutes));
        
        // Store in dictionary
        _bundles[name] = bundle;

        _logger.LogInformation("Created bundle {Name} with {FileCount} files ({OriginalSize} -> {BundledSize} bytes)",
            name, bundle.Files.Count, originalSize, bundle.BundledSize);

        return bundle;
    }

    public async Task<Bundle> GetBundleAsync(string name)
    {
        var cacheKey = GetBundleCacheKey(name);
        
        if (_cache.TryGetValue<Bundle>(cacheKey, out var cachedBundle) && cachedBundle != null)
        {
            return cachedBundle;
        }

        if (_bundles.TryGetValue(name, out var bundle))
        {
            // Re-cache it
            _cache.Set(cacheKey, bundle, TimeSpan.FromMinutes(_settings.CacheMinutes));
            return bundle;
        }

        // Try to load from configuration
        var configBundle = GetBundleFromConfiguration(name);
        if (configBundle != null)
        {
            bundle = await CreateBundleAsync(name, configBundle.Files, configBundle.Type);
            return bundle;
        }

        throw new KeyNotFoundException($"Bundle '{name}' not found");
    }

    public async Task<string> GetBundleContentAsync(string name)
    {
        var bundle = await GetBundleAsync(name);
        return bundle.Content;
    }

    public Task InvalidateBundleAsync(string name)
    {
        var cacheKey = GetBundleCacheKey(name);
        _cache.Remove(cacheKey);
        _bundles.Remove(name);
        
        _logger.LogDebug("Invalidated bundle cache for {Name}", name);
        return Task.CompletedTask;
    }

    public async Task RegisterBundlesAsync()
    {
        var bundleConfigs = GetBundlesFromConfiguration();
        
        foreach (var config in bundleConfigs)
        {
            try
            {
                await CreateBundleAsync(config.Name, config.Files, config.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register bundle {Name}", config.Name);
            }
        }
        
        _logger.LogInformation("Registered {Count} bundles from configuration", bundleConfigs.Count);
    }

    public string GetBundleUrl(string name)
    {
        if (!_bundles.TryGetValue(name, out var bundle))
        {
            return $"/bundles/{name}";
        }

        // Add hash for cache busting
        return $"/bundles/{name}?v={bundle.Hash}";
    }

    private Task<string> MinifyContentAsync(string content, BundleType type)
    {
        var result = type switch
        {
            BundleType.JavaScript => _minificationService.MinifyJavaScript(content),
            BundleType.Css => _minificationService.MinifyCss(content),
            _ => content
        };
        return Task.FromResult(result);
    }

    private bool ShouldMinify()
    {
        if (_environment.IsDevelopment())
        {
            return _settings.MinifyInDevelopment;
        }

        return _settings.EnableMinification;
    }

    private string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash)[..8]; // First 8 chars for URL
    }

    private string GetBundleCacheKey(string name)
    {
        return $"bundle:{name}";
    }

    private BundleConfiguration? GetBundleFromConfiguration(string name)
    {
        var bundles = GetBundlesFromConfiguration();
        return bundles.FirstOrDefault(b => b.Name == name);
    }

    private List<BundleConfiguration> GetBundlesFromConfiguration()
    {
        var bundles = new List<BundleConfiguration>();
        _configuration.GetSection("Bundling:Bundles").Bind(bundles);
        return bundles;
    }

    private BundlingSettings LoadBundlingSettings()
    {
        var settings = new BundlingSettings();
        _configuration.GetSection("Bundling").Bind(settings);
        return settings;
    }
}

/// <summary>
/// Bundle configuration from appsettings
/// </summary>
public class BundleConfiguration
{
    public string Name { get; set; } = string.Empty;
    public BundleType Type { get; set; }
    public List<string> Files { get; set; } = new();
}

/// <summary>
/// Bundling configuration settings
/// </summary>
public class BundlingSettings
{
    public bool EnableBundling { get; set; } = true;
    public bool EnableMinification { get; set; } = true;
    public bool MinifyInDevelopment { get; set; } = false;
    public int CacheMinutes { get; set; } = 60;
    public List<BundleConfiguration> Bundles { get; set; } = new();
}