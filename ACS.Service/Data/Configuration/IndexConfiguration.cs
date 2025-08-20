using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ACS.Service.Data.Models;

namespace ACS.Service.Data.Configuration;

/// <summary>
/// Database index configuration for query performance optimization
/// </summary>
public static class IndexConfiguration
{
    /// <summary>
    /// Configure all indexes for optimal query performance
    /// </summary>
    public static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        ConfigureUserIndexes(modelBuilder);
        ConfigureGroupIndexes(modelBuilder);
        ConfigureRoleIndexes(modelBuilder);
        ConfigureResourceIndexes(modelBuilder);
        ConfigurePermissionIndexes(modelBuilder);
        ConfigureAuditLogIndexes(modelBuilder);
        ConfigureJunctionTableIndexes(modelBuilder);
    }

    /// <summary>
    /// Configure indexes for User entity
    /// </summary>
    private static void ConfigureUserIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // Unique email index (already exists)
            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("IX_User_Email");

            // Index for active users query
            entity.HasIndex(u => u.IsActive)
                .HasDatabaseName("IX_User_IsActive");

            // Index for failed login attempts monitoring
            entity.HasIndex(u => u.FailedLoginAttempts)
                .HasDatabaseName("IX_User_FailedLoginAttempts")
                .HasFilter("[FailedLoginAttempts] > 0");

            // Index for last login tracking
            entity.HasIndex(u => u.LastLoginAt)
                .HasDatabaseName("IX_User_LastLoginAt")
                .HasFilter("[LastLoginAt] IS NOT NULL");

            // Composite index for user searches
            entity.HasIndex(u => new { u.Name, u.Email, u.IsActive })
                .HasDatabaseName("IX_User_Search");

            // Index for entity relationship
            entity.HasIndex(u => u.EntityId)
                .HasDatabaseName("IX_User_EntityId");

            // Index for created date queries
            entity.HasIndex(u => u.CreatedAt)
                .HasDatabaseName("IX_User_CreatedAt");
        });
    }

    /// <summary>
    /// Configure indexes for Group entity
    /// </summary>
    private static void ConfigureGroupIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Group>(entity =>
        {
            // Index for group name searches
            entity.HasIndex(g => g.Name)
                .HasDatabaseName("IX_Group_Name");

            // Index for entity relationship
            entity.HasIndex(g => g.EntityId)
                .HasDatabaseName("IX_Group_EntityId");

            // Index for created date queries
            entity.HasIndex(g => g.CreatedAt)
                .HasDatabaseName("IX_Group_CreatedAt");

            // Composite index for group hierarchy queries
            entity.HasIndex(g => new { g.Id, g.Name })
                .HasDatabaseName("IX_Group_Hierarchy");
        });
    }

    /// <summary>
    /// Configure indexes for Role entity
    /// </summary>
    private static void ConfigureRoleIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            // Index for role name searches
            entity.HasIndex(r => r.Name)
                .HasDatabaseName("IX_Role_Name");

            // Index for entity relationship
            entity.HasIndex(r => r.EntityId)
                .HasDatabaseName("IX_Role_EntityId");

            // Index for created date queries
            entity.HasIndex(r => r.CreatedAt)
                .HasDatabaseName("IX_Role_CreatedAt");
        });
    }

    /// <summary>
    /// Configure indexes for Resource entity
    /// </summary>
    private static void ConfigureResourceIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resource>(entity =>
        {
            // Index for URI lookups (critical for permission checks)
            entity.HasIndex(r => r.Uri)
                .HasDatabaseName("IX_Resource_Uri");

            // Index for pattern matching queries
            entity.HasIndex(r => r.Uri)
                .HasDatabaseName("IX_Resource_Uri_Pattern")
                .HasFilter("[Uri] LIKE '%*%' OR [Uri] LIKE '%{%'");
        });
    }

    /// <summary>
    /// Configure indexes for permission-related entities
    /// </summary>
    private static void ConfigurePermissionIndexes(ModelBuilder modelBuilder)
    {
        // VerbType indexes
        modelBuilder.Entity<VerbType>(entity =>
        {
            entity.HasIndex(v => v.VerbName)
                .IsUnique()
                .HasDatabaseName("IX_VerbType_VerbName");
        });

        // SchemeType indexes
        modelBuilder.Entity<SchemeType>(entity =>
        {
            entity.HasIndex(s => s.SchemeName)
                .HasDatabaseName("IX_SchemeType_SchemeName");
        });

        // PermissionScheme indexes
        modelBuilder.Entity<PermissionScheme>(entity =>
        {
            // Index for entity lookups
            entity.HasIndex(ps => ps.EntityId)
                .HasDatabaseName("IX_PermissionScheme_EntityId");

            // Index for scheme type lookups
            entity.HasIndex(ps => ps.SchemeTypeId)
                .HasDatabaseName("IX_PermissionScheme_SchemeTypeId");

            // Composite index for permission queries
            entity.HasIndex(ps => new { ps.EntityId, ps.SchemeTypeId })
                .HasDatabaseName("IX_PermissionScheme_Entity_SchemeType");
        });

        // UriAccess indexes (critical for permission evaluation)
        modelBuilder.Entity<UriAccess>(entity =>
        {
            // Index for resource lookups
            entity.HasIndex(ua => ua.ResourceId)
                .HasDatabaseName("IX_UriAccess_ResourceId");

            // Index for verb type lookups
            entity.HasIndex(ua => ua.VerbTypeId)
                .HasDatabaseName("IX_UriAccess_VerbTypeId");

            // Index for permission scheme lookups
            entity.HasIndex(ua => ua.PermissionSchemeId)
                .HasDatabaseName("IX_UriAccess_PermissionSchemeId");

            // Index for grant permissions
            entity.HasIndex(ua => ua.Grant)
                .HasDatabaseName("IX_UriAccess_Grant")
                .HasFilter("[Grant] = 1");

            // Index for deny permissions
            entity.HasIndex(ua => ua.Deny)
                .HasDatabaseName("IX_UriAccess_Deny")
                .HasFilter("[Deny] = 1");

            // Composite index for permission evaluation queries
            entity.HasIndex(ua => new { ua.ResourceId, ua.VerbTypeId, ua.PermissionSchemeId })
                .HasDatabaseName("IX_UriAccess_Permission_Evaluation");

            // Composite index for grant/deny filtering
            entity.HasIndex(ua => new { ua.ResourceId, ua.VerbTypeId, ua.Grant, ua.Deny })
                .HasDatabaseName("IX_UriAccess_Grant_Deny_Filter");
        });
    }

    /// <summary>
    /// Configure indexes for AuditLog entity
    /// </summary>
    private static void ConfigureAuditLogIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Index for entity type queries
            entity.HasIndex(al => al.EntityType)
                .HasDatabaseName("IX_AuditLog_EntityType");

            // Index for entity ID queries
            entity.HasIndex(al => al.EntityId)
                .HasDatabaseName("IX_AuditLog_EntityId");

            // Index for change type queries
            entity.HasIndex(al => al.ChangeType)
                .HasDatabaseName("IX_AuditLog_ChangeType");

            // Index for user activity queries
            entity.HasIndex(al => al.ChangedBy)
                .HasDatabaseName("IX_AuditLog_ChangedBy");

            // Index for date range queries
            entity.HasIndex(al => al.ChangeDate)
                .HasDatabaseName("IX_AuditLog_ChangeDate")
                .IsDescending();

            // Composite index for entity audit trail
            entity.HasIndex(al => new { al.EntityType, al.EntityId, al.ChangeDate })
                .HasDatabaseName("IX_AuditLog_Entity_Trail")
                .IsDescending(false, false, true);

            // Composite index for user activity tracking
            entity.HasIndex(al => new { al.ChangedBy, al.ChangeDate })
                .HasDatabaseName("IX_AuditLog_User_Activity")
                .IsDescending(false, true);

            // Filtered index for security events
            entity.HasIndex(al => new { al.ChangeType, al.ChangeDate })
                .HasDatabaseName("IX_AuditLog_Security_Events")
                .HasFilter("[ChangeType] IN ('Security', 'Login', 'Logout', 'FailedLogin', 'PermissionChange')")
                .IsDescending(false, true);
        });
    }

    /// <summary>
    /// Configure indexes for junction tables
    /// </summary>
    private static void ConfigureJunctionTableIndexes(ModelBuilder modelBuilder)
    {
        // UserGroup indexes
        modelBuilder.Entity<UserGroup>(entity =>
        {
            // Unique composite index (already exists)
            entity.HasIndex(ug => new { ug.UserId, ug.GroupId })
                .IsUnique()
                .HasDatabaseName("IX_UserGroup_User_Group");

            // Index for group lookups
            entity.HasIndex(ug => ug.GroupId)
                .HasDatabaseName("IX_UserGroup_GroupId");

            // Index for user lookups
            entity.HasIndex(ug => ug.UserId)
                .HasDatabaseName("IX_UserGroup_UserId");

            // Index for created date
            entity.HasIndex(ug => ug.CreatedAt)
                .HasDatabaseName("IX_UserGroup_CreatedAt");
        });

        // UserRole indexes
        modelBuilder.Entity<UserRole>(entity =>
        {
            // Unique composite index (already exists)
            entity.HasIndex(ur => new { ur.UserId, ur.RoleId })
                .IsUnique()
                .HasDatabaseName("IX_UserRole_User_Role");

            // Index for role lookups
            entity.HasIndex(ur => ur.RoleId)
                .HasDatabaseName("IX_UserRole_RoleId");

            // Index for user lookups
            entity.HasIndex(ur => ur.UserId)
                .HasDatabaseName("IX_UserRole_UserId");

            // Index for created date
            entity.HasIndex(ur => ur.CreatedAt)
                .HasDatabaseName("IX_UserRole_CreatedAt");
        });

        // GroupRole indexes
        modelBuilder.Entity<GroupRole>(entity =>
        {
            // Unique composite index (already exists)
            entity.HasIndex(gr => new { gr.GroupId, gr.RoleId })
                .IsUnique()
                .HasDatabaseName("IX_GroupRole_Group_Role");

            // Index for role lookups
            entity.HasIndex(gr => gr.RoleId)
                .HasDatabaseName("IX_GroupRole_RoleId");

            // Index for group lookups
            entity.HasIndex(gr => gr.GroupId)
                .HasDatabaseName("IX_GroupRole_GroupId");

            // Index for created date
            entity.HasIndex(gr => gr.CreatedAt)
                .HasDatabaseName("IX_GroupRole_CreatedAt");
        });

        // GroupHierarchy indexes
        modelBuilder.Entity<GroupHierarchy>(entity =>
        {
            // Unique composite index (already exists)
            entity.HasIndex(gh => new { gh.ParentGroupId, gh.ChildGroupId })
                .IsUnique()
                .HasDatabaseName("IX_GroupHierarchy_Parent_Child");

            // Index for child group lookups
            entity.HasIndex(gh => gh.ChildGroupId)
                .HasDatabaseName("IX_GroupHierarchy_ChildGroupId");

            // Index for parent group lookups
            entity.HasIndex(gh => gh.ParentGroupId)
                .HasDatabaseName("IX_GroupHierarchy_ParentGroupId");

            // Index for created date
            entity.HasIndex(gh => gh.CreatedAt)
                .HasDatabaseName("IX_GroupHierarchy_CreatedAt");
        });
    }

    /// <summary>
    /// Configure additional performance indexes for common query patterns
    /// </summary>
    public static void ConfigurePerformanceIndexes(ModelBuilder modelBuilder)
    {
        // Entity base table indexes
        modelBuilder.Entity<Entity>(entity =>
        {
            // Index for entity type filtering
            entity.HasIndex(e => e.EntityType)
                .HasDatabaseName("IX_Entity_EntityType");

            // Index for created date queries
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Entity_CreatedAt");

            // Index for updated date queries
            entity.HasIndex(e => e.UpdatedAt)
                .HasDatabaseName("IX_Entity_UpdatedAt");

            // Composite index for type and date filtering
            entity.HasIndex(e => new { e.EntityType, e.CreatedAt })
                .HasDatabaseName("IX_Entity_Type_CreatedAt");
        });
    }

    /// <summary>
    /// Apply index configuration to ApplicationDbContext
    /// </summary>
    public static void ApplyIndexConfiguration(this ModelBuilder modelBuilder)
    {
        ConfigureIndexes(modelBuilder);
        ConfigurePerformanceIndexes(modelBuilder);
    }
}