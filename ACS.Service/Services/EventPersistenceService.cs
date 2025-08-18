using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using System.Text.Json;

namespace ACS.Service.Services;

public class EventPersistenceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<EventPersistenceService> _logger;
    private readonly string _tenantId;

    public EventPersistenceService(
        ApplicationDbContext dbContext,
        Infrastructure.TenantConfiguration tenantConfig,
        ILogger<EventPersistenceService> logger)
    {
        _dbContext = dbContext;
        _tenantId = tenantConfig.TenantId;
        _logger = logger;
    }

    #region User-Group Events

    public async Task LogAddUserToGroupAsync(int userId, int groupId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Add",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "AddUserToGroup",
                UserId = userId,
                GroupId = groupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for AddUserToGroup: User {UserId} added to Group {GroupId}", userId, groupId);
    }

    public async Task LogRemoveUserFromGroupAsync(int userId, int groupId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Remove",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "RemoveUserFromGroup",
                UserId = userId,
                GroupId = groupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for RemoveUserFromGroup: User {UserId} removed from Group {GroupId}", userId, groupId);
    }

    #endregion

    #region User-Role Events

    public async Task LogAssignUserToRoleAsync(int userId, int roleId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Assign",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "AssignUserToRole",
                UserId = userId,
                RoleId = roleId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for AssignUserToRole: User {UserId} assigned to Role {RoleId}", userId, roleId);
    }

    public async Task LogUnAssignUserFromRoleAsync(int userId, int roleId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Unassign",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "UnAssignUserFromRole",
                UserId = userId,
                RoleId = roleId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for UnAssignUserFromRole: User {UserId} unassigned from Role {RoleId}", userId, roleId);
    }

    #endregion

    #region Group-Role Events

    public async Task LogAddRoleToGroupAsync(int groupId, int roleId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = groupId,
            ChangeType = "Add",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "AddRoleToGroup",
                GroupId = groupId,
                RoleId = roleId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for AddRoleToGroup: Role {RoleId} added to Group {GroupId}", roleId, groupId);
    }

    public async Task LogRemoveRoleFromGroupAsync(int groupId, int roleId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = groupId,
            ChangeType = "Remove",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "RemoveRoleFromGroup",
                GroupId = groupId,
                RoleId = roleId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for RemoveRoleFromGroup: Role {RoleId} removed from Group {GroupId}", roleId, groupId);
    }

    #endregion

    #region Group-Group Events

    public async Task LogAddGroupToGroupAsync(int parentGroupId, int childGroupId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = childGroupId,
            ChangeType = "Add",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "AddGroupToGroup",
                ParentGroupId = parentGroupId,
                ChildGroupId = childGroupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for AddGroupToGroup: Group {ChildGroupId} added to Group {ParentGroupId}", childGroupId, parentGroupId);
    }

    public async Task LogRemoveGroupFromGroupAsync(int parentGroupId, int childGroupId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = childGroupId,
            ChangeType = "Remove",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "RemoveGroupFromGroup",
                ParentGroupId = parentGroupId,
                ChildGroupId = childGroupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for RemoveGroupFromGroup: Group {ChildGroupId} removed from Group {ParentGroupId}", childGroupId, parentGroupId);
    }

    #endregion

    #region Permission Events

    public async Task LogAddPermissionToEntityAsync(int entityId, Permission permission, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Entity",
            EntityId = entityId,
            ChangeType = "Grant",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "AddPermissionToEntity",
                EntityId = entityId,
                Permission = new
                {
                    permission.Id,
                    permission.Uri,
                    HttpVerb = permission.HttpVerb.ToString(),
                    permission.Grant,
                    permission.Deny,
                    Scheme = permission.Scheme.ToString()
                },
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for AddPermissionToEntity: Permission {Uri}:{HttpVerb} added to Entity {EntityId}", 
            permission.Uri, permission.HttpVerb, entityId);
    }

    public async Task LogRemovePermissionFromEntityAsync(int entityId, Permission permission, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Entity",
            EntityId = entityId,
            ChangeType = "Revoke",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "RemovePermissionFromEntity",
                EntityId = entityId,
                Permission = new
                {
                    permission.Id,
                    permission.Uri,
                    HttpVerb = permission.HttpVerb.ToString(),
                    permission.Grant,
                    permission.Deny,
                    Scheme = permission.Scheme.ToString()
                },
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for RemovePermissionFromEntity: Permission {Uri}:{HttpVerb} removed from Entity {EntityId}", 
            permission.Uri, permission.HttpVerb, entityId);
    }

    #endregion

    #region Query Events (Optional - for security auditing)

    public async Task LogPermissionCheckAsync(int entityId, string uri, HttpVerb httpVerb, bool result, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Entity",
            EntityId = entityId,
            ChangeType = "Check",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "CheckPermission",
                EntityId = entityId,
                Uri = uri,
                HttpVerb = httpVerb.ToString(),
                Result = result,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for CheckPermission: Entity {EntityId} permission check for {Uri}:{HttpVerb} = {Result}", 
            entityId, uri, httpVerb, result);
    }

    #endregion

    #region CREATE Events - Phase 2 requirement

    public async Task LogCreateUserAsync(int userId, string name, int? groupId = null, int? roleId = null, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Create",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "CreateUser",
                UserId = userId,
                Name = name,
                GroupId = groupId,
                RoleId = roleId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for CreateUser: User {UserId} with name '{UserName}'", userId, name);
    }

    public async Task LogCreateGroupAsync(int groupId, string name, int? parentGroupId = null, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = groupId,
            ChangeType = "Create",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "CreateGroup",
                GroupId = groupId,
                Name = name,
                ParentGroupId = parentGroupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for CreateGroup: Group {GroupId} with name '{GroupName}'", groupId, name);
    }

    public async Task LogCreateRoleAsync(int roleId, string name, int? groupId = null, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Role",
            EntityId = roleId,
            ChangeType = "Create",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "CreateRole",
                RoleId = roleId,
                Name = name,
                GroupId = groupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for CreateRole: Role {RoleId} with name '{RoleName}'", roleId, name);
    }

    #endregion

    #region UPDATE Events

    public async Task LogUpdateUserAsync(int userId, string name, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Update",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "UpdateUser",
                UserId = userId,
                Name = name,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for UpdateUser: User {UserId} with name '{UserName}'", userId, name);
    }

    public async Task LogUpdateGroupAsync(int groupId, string name, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = groupId,
            ChangeType = "Update",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "UpdateGroup",
                GroupId = groupId,
                Name = name,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for UpdateGroup: Group {GroupId} with name '{GroupName}'", groupId, name);
    }

    public async Task LogUpdateRoleAsync(int roleId, string name, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Role",
            EntityId = roleId,
            ChangeType = "Update",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "UpdateRole",
                RoleId = roleId,
                Name = name,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for UpdateRole: Role {RoleId} with name '{RoleName}'", roleId, name);
    }

    #endregion

    #region DELETE Events

    public async Task LogDeleteUserAsync(int userId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "User",
            EntityId = userId,
            ChangeType = "Delete",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "DeleteUser",
                UserId = userId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for DeleteUser: User {UserId}", userId);
    }

    public async Task LogDeleteGroupAsync(int groupId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Group",
            EntityId = groupId,
            ChangeType = "Delete",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "DeleteGroup",
                GroupId = groupId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for DeleteGroup: Group {GroupId}", groupId);
    }

    public async Task LogDeleteRoleAsync(int roleId, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Role",
            EntityId = roleId,
            ChangeType = "Delete",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "DeleteRole",
                RoleId = roleId,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for DeleteRole: Role {RoleId}", roleId);
    }

    #endregion

    #region Command Events

    public async Task LogCommandExecutionAsync(string commandType, object commandData, bool success, string errorMessage = "", long processingTimeMs = 0, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Command",
            EntityId = 0, // Commands don't have specific entity IDs
            ChangeType = success ? "Execute" : "Error",
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "CommandExecution",
                CommandType = commandType,
                CommandData = commandData,
                Success = success,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = processingTimeMs,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for CommandExecution: {CommandType} success={Success} time={ProcessingTimeMs}ms", 
            commandType, success, processingTimeMs);
    }

    #endregion

    #region Bulk Operations

    public async Task LogBulkOperationAsync(string operationType, int affectedCount, object operationData, string changedBy = "System")
    {
        var auditLog = new AuditLog
        {
            EntityType = "Bulk",
            EntityId = 0,
            ChangeType = operationType,
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = JsonSerializer.Serialize(new
            {
                Action = "BulkOperation",
                OperationType = operationType,
                AffectedCount = affectedCount,
                OperationData = operationData,
                TenantId = _tenantId,
                Timestamp = DateTime.UtcNow
            })
        };

        await PersistAuditLogAsync(auditLog);
        _logger.LogDebug("Audit log created for BulkOperation: {OperationType} affected {AffectedCount} entities", 
            operationType, affectedCount);
    }

    #endregion

    #region Query Methods

    public async Task<List<AuditLog>> GetAuditLogsAsync(int? entityId = null, string? entityType = null, DateTime? fromDate = null, DateTime? toDate = null, int maxResults = 100)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId.Value);

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (fromDate.HasValue)
            query = query.Where(a => a.ChangeDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.ChangeDate <= toDate.Value);

        var results = await query
            .OrderByDescending(a => a.ChangeDate)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync();

        _logger.LogDebug("Retrieved {Count} audit logs for tenant {TenantId}", results.Count, _tenantId);

        return results;
    }

    public async Task<int> GetAuditLogCountAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(a => a.ChangeDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.ChangeDate <= toDate.Value);

        var count = await query.CountAsync();
        
        _logger.LogDebug("Found {Count} audit logs for tenant {TenantId} in date range", count, _tenantId);

        return count;
    }

    #endregion

    #region Private Methods

    private async Task PersistAuditLogAsync(AuditLog auditLog)
    {
        try
        {
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogTrace("Audit log persisted: {EntityType} {EntityId} {ChangeType}", 
                auditLog.EntityType, auditLog.EntityId, auditLog.ChangeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log for {EntityType} {EntityId} {ChangeType}", 
                auditLog.EntityType, auditLog.EntityId, auditLog.ChangeType);
            
            // Don't throw - audit logging should not break business operations
            // Could implement retry logic or store in a queue for later processing
        }
    }

    #endregion
}