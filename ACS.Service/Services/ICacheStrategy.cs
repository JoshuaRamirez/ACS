namespace ACS.Service.Services;

/// <summary>
/// Cache strategy interface
/// </summary>
public interface ICacheStrategy
{
    TimeSpan GetExpiration(string key);
    bool ShouldCache(string key, object value);
    string GetCacheKey(string prefix, params object[] keyParts);
    int GetPriority(string key);
}