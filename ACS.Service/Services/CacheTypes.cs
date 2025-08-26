namespace ACS.Service.Services;

/// <summary>
/// Types of cache layers for invalidation strategies
/// </summary>
public enum CacheType
{
    Memory,
    Distributed,
    Database,
    L1,
    L2,
    L3,
    All
}