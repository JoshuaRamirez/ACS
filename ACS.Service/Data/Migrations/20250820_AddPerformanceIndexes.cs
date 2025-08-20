using Microsoft.EntityFrameworkCore.Migrations;

namespace ACS.Service.Data.Migrations;

/// <summary>
/// Migration to add performance indexes for frequently queried columns
/// </summary>
public partial class AddPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ========================================
        // User Table Indexes
        // ========================================
        
        // Index for user lookups by email (unique)
        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true,
            filter: "[Email] IS NOT NULL");

        // Index for user lookups by username (unique)
        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true,
            filter: "[Username] IS NOT NULL");

        // Index for active user queries
        migrationBuilder.CreateIndex(
            name: "IX_Users_IsActive_LastLogin",
            table: "Users",
            columns: new[] { "IsActive", "LastLoginDate" });

        // Index for tenant-based user queries
        migrationBuilder.CreateIndex(
            name: "IX_Users_TenantId_IsActive",
            table: "Users",
            columns: new[] { "TenantId", "IsActive" });

        // ========================================
        // Group Table Indexes
        // ========================================
        
        // Index for group hierarchy queries
        migrationBuilder.CreateIndex(
            name: "IX_Groups_ParentGroupId",
            table: "Groups",
            column: "ParentGroupId");

        // Index for tenant-based group queries
        migrationBuilder.CreateIndex(
            name: "IX_Groups_TenantId_IsActive",
            table: "Groups",
            columns: new[] { "TenantId", "IsActive" });

        // Index for group name lookups within tenant
        migrationBuilder.CreateIndex(
            name: "IX_Groups_TenantId_Name",
            table: "Groups",
            columns: new[] { "TenantId", "Name" });

        // ========================================
        // Role Table Indexes
        // ========================================
        
        // Index for role lookups by name within tenant
        migrationBuilder.CreateIndex(
            name: "IX_Roles_TenantId_Name",
            table: "Roles",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        // Index for active roles
        migrationBuilder.CreateIndex(
            name: "IX_Roles_IsActive",
            table: "Roles",
            column: "IsActive");

        // ========================================
        // Permission Table Indexes
        // ========================================
        
        // Index for permission lookups by resource
        migrationBuilder.CreateIndex(
            name: "IX_Permissions_ResourceId",
            table: "Permissions",
            column: "ResourceId");

        // Index for permission lookups by name
        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Name",
            table: "Permissions",
            column: "Name");

        // Composite index for permission checks
        migrationBuilder.CreateIndex(
            name: "IX_Permissions_ResourceId_Action",
            table: "Permissions",
            columns: new[] { "ResourceId", "Action" });

        // ========================================
        // UserRole Junction Table Indexes
        // ========================================
        
        // Index for finding all roles for a user
        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_UserId_IsActive",
            table: "UserRoles",
            columns: new[] { "UserId", "IsActive" });

        // Index for finding all users with a specific role
        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId_IsActive",
            table: "UserRoles",
            columns: new[] { "RoleId", "IsActive" });

        // Index for temporal queries
        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_ValidFrom_ValidTo",
            table: "UserRoles",
            columns: new[] { "ValidFrom", "ValidTo" });

        // ========================================
        // UserGroup Junction Table Indexes
        // ========================================
        
        // Index for finding all groups for a user
        migrationBuilder.CreateIndex(
            name: "IX_UserGroups_UserId",
            table: "UserGroups",
            column: "UserId");

        // Index for finding all users in a group
        migrationBuilder.CreateIndex(
            name: "IX_UserGroups_GroupId",
            table: "UserGroups",
            column: "GroupId");

        // ========================================
        // RolePermission Junction Table Indexes
        // ========================================
        
        // Index for finding all permissions for a role
        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_RoleId",
            table: "RolePermissions",
            column: "RoleId");

        // Index for finding all roles with a specific permission
        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_PermissionId",
            table: "RolePermissions",
            column: "PermissionId");

        // ========================================
        // AuditLog Table Indexes
        // ========================================
        
        // Index for audit log queries by entity
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EntityType_EntityId",
            table: "AuditLogs",
            columns: new[] { "EntityType", "EntityId" });

        // Index for audit log queries by user
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_UserId_Timestamp",
            table: "AuditLogs",
            columns: new[] { "UserId", "Timestamp" });

        // Index for audit log queries by action
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Action_Timestamp",
            table: "AuditLogs",
            columns: new[] { "Action", "Timestamp" });

        // Index for tenant-based audit queries
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TenantId_Timestamp",
            table: "AuditLogs",
            columns: new[] { "TenantId", "Timestamp" });

        // ========================================
        // Session Table Indexes
        // ========================================
        
        // Index for active session lookups
        migrationBuilder.CreateIndex(
            name: "IX_Sessions_Token",
            table: "Sessions",
            column: "Token",
            unique: true);

        // Index for user session queries
        migrationBuilder.CreateIndex(
            name: "IX_Sessions_UserId_IsActive",
            table: "Sessions",
            columns: new[] { "UserId", "IsActive" });

        // Index for session expiry cleanup
        migrationBuilder.CreateIndex(
            name: "IX_Sessions_ExpiresAt",
            table: "Sessions",
            column: "ExpiresAt");

        // ========================================
        // Resource Table Indexes
        // ========================================
        
        // Index for resource type queries
        migrationBuilder.CreateIndex(
            name: "IX_Resources_Type",
            table: "Resources",
            column: "Type");

        // Index for resource URI lookups
        migrationBuilder.CreateIndex(
            name: "IX_Resources_Uri",
            table: "Resources",
            column: "Uri");

        // Index for tenant-based resource queries
        migrationBuilder.CreateIndex(
            name: "IX_Resources_TenantId_Type",
            table: "Resources",
            columns: new[] { "TenantId", "Type" });

        // ========================================
        // RefreshToken Table Indexes
        // ========================================
        
        // Index for refresh token lookups
        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token",
            unique: true);

        // Index for user refresh token queries
        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId_IsActive",
            table: "RefreshTokens",
            columns: new[] { "UserId", "IsActive" });

        // Index for refresh token expiry cleanup
        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_ExpiresAt",
            table: "RefreshTokens",
            column: "ExpiresAt");

        // ========================================
        // ApiKey Table Indexes
        // ========================================
        
        // Index for API key lookups
        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_Key",
            table: "ApiKeys",
            column: "Key",
            unique: true);

        // Index for user API key queries
        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_UserId_IsActive",
            table: "ApiKeys",
            columns: new[] { "UserId", "IsActive" });

        // ========================================
        // Full-Text Search Indexes (SQL Server specific)
        // ========================================
        
        // Create full-text catalog if not exists
        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ACS_FullTextCatalog')
            BEGIN
                CREATE FULLTEXT CATALOG ACS_FullTextCatalog AS DEFAULT;
            END
        ");

        // Create full-text index on Users table
        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Users'))
            BEGIN
                CREATE FULLTEXT INDEX ON Users
                (
                    FirstName LANGUAGE 1033,
                    LastName LANGUAGE 1033,
                    Email LANGUAGE 1033,
                    Username LANGUAGE 1033
                )
                KEY INDEX PK_Users
                ON ACS_FullTextCatalog
                WITH STOPLIST = SYSTEM;
            END
        ");

        // Create full-text index on Groups table
        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Groups'))
            BEGIN
                CREATE FULLTEXT INDEX ON Groups
                (
                    Name LANGUAGE 1033,
                    Description LANGUAGE 1033
                )
                KEY INDEX PK_Groups
                ON ACS_FullTextCatalog
                WITH STOPLIST = SYSTEM;
            END
        ");

        // Create full-text index on AuditLogs table
        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('AuditLogs'))
            BEGIN
                CREATE FULLTEXT INDEX ON AuditLogs
                (
                    Details LANGUAGE 1033,
                    Metadata LANGUAGE 1033
                )
                KEY INDEX PK_AuditLogs
                ON ACS_FullTextCatalog
                WITH STOPLIST = SYSTEM;
            END
        ");

        // ========================================
        // Statistics Updates
        // ========================================
        
        // Update statistics for better query plan optimization
        migrationBuilder.Sql("UPDATE STATISTICS Users WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS Groups WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS Roles WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS Permissions WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS UserRoles WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS UserGroups WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS RolePermissions WITH FULLSCAN;");
        migrationBuilder.Sql("UPDATE STATISTICS AuditLogs WITH FULLSCAN;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop full-text indexes
        migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('AuditLogs')) DROP FULLTEXT INDEX ON AuditLogs;");
        migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Groups')) DROP FULLTEXT INDEX ON Groups;");
        migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Users')) DROP FULLTEXT INDEX ON Users;");
        migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ACS_FullTextCatalog') DROP FULLTEXT CATALOG ACS_FullTextCatalog;");

        // Drop indexes in reverse order
        migrationBuilder.DropIndex("IX_ApiKeys_UserId_IsActive", "ApiKeys");
        migrationBuilder.DropIndex("IX_ApiKeys_Key", "ApiKeys");
        
        migrationBuilder.DropIndex("IX_RefreshTokens_ExpiresAt", "RefreshTokens");
        migrationBuilder.DropIndex("IX_RefreshTokens_UserId_IsActive", "RefreshTokens");
        migrationBuilder.DropIndex("IX_RefreshTokens_Token", "RefreshTokens");
        
        migrationBuilder.DropIndex("IX_Resources_TenantId_Type", "Resources");
        migrationBuilder.DropIndex("IX_Resources_Uri", "Resources");
        migrationBuilder.DropIndex("IX_Resources_Type", "Resources");
        
        migrationBuilder.DropIndex("IX_Sessions_ExpiresAt", "Sessions");
        migrationBuilder.DropIndex("IX_Sessions_UserId_IsActive", "Sessions");
        migrationBuilder.DropIndex("IX_Sessions_Token", "Sessions");
        
        migrationBuilder.DropIndex("IX_AuditLogs_TenantId_Timestamp", "AuditLogs");
        migrationBuilder.DropIndex("IX_AuditLogs_Action_Timestamp", "AuditLogs");
        migrationBuilder.DropIndex("IX_AuditLogs_UserId_Timestamp", "AuditLogs");
        migrationBuilder.DropIndex("IX_AuditLogs_EntityType_EntityId", "AuditLogs");
        
        migrationBuilder.DropIndex("IX_RolePermissions_PermissionId", "RolePermissions");
        migrationBuilder.DropIndex("IX_RolePermissions_RoleId", "RolePermissions");
        
        migrationBuilder.DropIndex("IX_UserGroups_GroupId", "UserGroups");
        migrationBuilder.DropIndex("IX_UserGroups_UserId", "UserGroups");
        
        migrationBuilder.DropIndex("IX_UserRoles_ValidFrom_ValidTo", "UserRoles");
        migrationBuilder.DropIndex("IX_UserRoles_RoleId_IsActive", "UserRoles");
        migrationBuilder.DropIndex("IX_UserRoles_UserId_IsActive", "UserRoles");
        
        migrationBuilder.DropIndex("IX_Permissions_ResourceId_Action", "Permissions");
        migrationBuilder.DropIndex("IX_Permissions_Name", "Permissions");
        migrationBuilder.DropIndex("IX_Permissions_ResourceId", "Permissions");
        
        migrationBuilder.DropIndex("IX_Roles_IsActive", "Roles");
        migrationBuilder.DropIndex("IX_Roles_TenantId_Name", "Roles");
        
        migrationBuilder.DropIndex("IX_Groups_TenantId_Name", "Groups");
        migrationBuilder.DropIndex("IX_Groups_TenantId_IsActive", "Groups");
        migrationBuilder.DropIndex("IX_Groups_ParentGroupId", "Groups");
        
        migrationBuilder.DropIndex("IX_Users_TenantId_IsActive", "Users");
        migrationBuilder.DropIndex("IX_Users_IsActive_LastLogin", "Users");
        migrationBuilder.DropIndex("IX_Users_Username", "Users");
        migrationBuilder.DropIndex("IX_Users_Email", "Users");
    }
}