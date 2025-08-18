using ACS.Service.Domain;

namespace ACS.Service.Services;

public interface IPermissionEvaluationService
{
    Task<bool> HasPermissionAsync(int entityId, string uri, HttpVerb httpVerb);
    Task<List<Permission>> GetEffectivePermissionsAsync(int entityId);
    Task<bool> CanUserAccessResourceAsync(int userId, string uri, HttpVerb httpVerb);
    Task<List<Permission>> GetUserPermissionsAsync(int userId);
    Task<List<Permission>> GetGroupPermissionsAsync(int groupId);
    Task<List<Permission>> GetRolePermissionsAsync(int roleId);
}