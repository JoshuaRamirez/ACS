namespace ACS.WebApi.Services;

public interface IUserContextService
{
    string GetCurrentUserId();
    string GetCurrentUserName();
    string GetTenantId();
    bool IsAuthenticated();
}