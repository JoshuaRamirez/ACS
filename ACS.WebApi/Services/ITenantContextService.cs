using ACS.Infrastructure;
using Grpc.Net.Client;
using static ACS.Infrastructure.TenantProcessDiscoveryService;

namespace ACS.WebApi.Services;

public interface ITenantContextService
{
    string GetTenantId();
    TenantProcessInfo? GetTenantProcessInfo();
    GrpcChannel? GetGrpcChannel();
}

public class TenantContextService : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetTenantId()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.Items["TenantId"]?.ToString() ?? throw new InvalidOperationException("Tenant ID not found in request context");
    }

    public TenantProcessInfo? GetTenantProcessInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.Items["TenantProcessInfo"] as TenantProcessInfo;
    }

    public GrpcChannel? GetGrpcChannel()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.Items["GrpcChannel"] as GrpcChannel;
    }
}