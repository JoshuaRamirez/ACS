using Microsoft.EntityFrameworkCore;
using ACS.Service.Data.Models;

namespace ACS.Service.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public static ApplicationDbContext Instance { get; set; } = null!;

        public DbSet<Entity> Entities { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<VerbType> VerbTypes { get; set; }
        public DbSet<SchemeType> SchemeTypes { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<PermissionScheme> EntityPermissions { get; set; }
        public DbSet<UriAccess> UriAccesses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        
        // Junction table DbSets
        public DbSet<UserGroup> UserGroups { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<GroupRole> GroupRoles { get; set; }
        public DbSet<GroupHierarchy> GroupHierarchies { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Entity configuration
            modelBuilder.Entity<Entity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                
                entity.HasCheckConstraint("CK_Entity_EntityType", 
                    "[EntityType] IN ('User', 'Group', 'Role')");
            });

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
                entity.Property(u => u.Salt).HasMaxLength(100);
                entity.Property(u => u.CreatedAt).IsRequired();
                entity.Property(u => u.UpdatedAt).IsRequired();
                entity.Property(u => u.IsActive).IsRequired();
                entity.Property(u => u.FailedLoginAttempts).IsRequired();
                
                entity.HasIndex(u => u.Email).IsUnique();
                
                entity.HasOne(u => u.Entity)
                    .WithMany(e => e.Users)
                    .HasForeignKey(u => u.EntityId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Ignore computed navigation properties
                entity.Ignore(u => u.Groups);
                entity.Ignore(u => u.Roles);
            });

            // Group configuration
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.Name).IsRequired().HasMaxLength(100);
                entity.Property(g => g.CreatedAt).IsRequired();
                entity.Property(g => g.UpdatedAt).IsRequired();
                
                entity.HasOne(g => g.Entity)
                    .WithMany(e => e.Groups)
                    .HasForeignKey(g => g.EntityId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Ignore computed navigation properties
                entity.Ignore(g => g.Users);
                entity.Ignore(g => g.Roles);
                entity.Ignore(g => g.ParentGroups);
                entity.Ignore(g => g.ChildGroups);
            });

            // Role configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
                entity.Property(r => r.CreatedAt).IsRequired();
                entity.Property(r => r.UpdatedAt).IsRequired();
                
                entity.HasOne(r => r.Entity)
                    .WithMany(e => e.Roles)
                    .HasForeignKey(r => r.EntityId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Ignore computed navigation properties
                entity.Ignore(r => r.Users);
                entity.Ignore(r => r.Groups);
            });

            // UserGroup junction table configuration
            modelBuilder.Entity<UserGroup>(entity =>
            {
                entity.HasKey(ug => ug.Id);
                entity.Property(ug => ug.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(ug => ug.CreatedAt).IsRequired();
                
                entity.HasIndex(ug => new { ug.UserId, ug.GroupId }).IsUnique();
                
                entity.HasOne(ug => ug.User)
                    .WithMany(u => u.UserGroups)
                    .HasForeignKey(ug => ug.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(ug => ug.Group)
                    .WithMany(g => g.UserGroups)
                    .HasForeignKey(ug => ug.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserRole junction table configuration
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(ur => ur.Id);
                entity.Property(ur => ur.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(ur => ur.CreatedAt).IsRequired();
                
                entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
                
                entity.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // GroupRole junction table configuration
            modelBuilder.Entity<GroupRole>(entity =>
            {
                entity.HasKey(gr => gr.Id);
                entity.Property(gr => gr.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(gr => gr.CreatedAt).IsRequired();
                
                entity.HasIndex(gr => new { gr.GroupId, gr.RoleId }).IsUnique();
                
                entity.HasOne(gr => gr.Group)
                    .WithMany(g => g.GroupRoles)
                    .HasForeignKey(gr => gr.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(gr => gr.Role)
                    .WithMany(r => r.GroupRoles)
                    .HasForeignKey(gr => gr.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // GroupHierarchy junction table configuration
            modelBuilder.Entity<GroupHierarchy>(entity =>
            {
                entity.HasKey(gh => gh.Id);
                entity.Property(gh => gh.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(gh => gh.CreatedAt).IsRequired();
                
                entity.HasIndex(gh => new { gh.ParentGroupId, gh.ChildGroupId }).IsUnique();
                entity.HasCheckConstraint("CK_GroupHierarchy_NoSelfReference", 
                    "[ParentGroupId] != [ChildGroupId]");
                
                entity.HasOne(gh => gh.ParentGroup)
                    .WithMany(g => g.ChildGroupRelations)
                    .HasForeignKey(gh => gh.ParentGroupId)
                    .OnDelete(DeleteBehavior.NoAction);
                
                entity.HasOne(gh => gh.ChildGroup)
                    .WithMany(g => g.ParentGroupRelations)
                    .HasForeignKey(gh => gh.ChildGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SchemeType configuration
            modelBuilder.Entity<SchemeType>(entity =>
            {
                entity.HasKey(st => st.Id);
                entity.Property(st => st.SchemeName).IsRequired().HasMaxLength(100);
            });

            // VerbType configuration
            modelBuilder.Entity<VerbType>(entity =>
            {
                entity.HasKey(vt => vt.Id);
                entity.Property(vt => vt.VerbName).IsRequired().HasMaxLength(50);
            });

            // Resource configuration
            modelBuilder.Entity<Resource>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Uri).IsRequired().HasMaxLength(500);
            });

            // PermissionScheme configuration
            modelBuilder.Entity<PermissionScheme>(entity =>
            {
                entity.HasKey(ps => ps.Id);
                
                entity.HasOne(ps => ps.Entity)
                    .WithMany()
                    .HasForeignKey(ps => ps.EntityId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(ps => ps.SchemeType)
                    .WithMany()
                    .HasForeignKey(ps => ps.SchemeTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // UriAccess configuration
            modelBuilder.Entity<UriAccess>(entity =>
            {
                entity.HasKey(ua => ua.Id);
                entity.Property(ua => ua.Grant).IsRequired();
                entity.Property(ua => ua.Deny).IsRequired();
                
                entity.HasCheckConstraint("CK_UriAccess_Grant_Deny_Exclusive", 
                    "([Grant] = 1 AND [Deny] = 0) OR ([Grant] = 0 AND [Deny] = 1)");
                
                entity.HasOne(ua => ua.Resource)
                    .WithMany()
                    .HasForeignKey(ua => ua.ResourceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(ua => ua.VerbType)
                    .WithMany()
                    .HasForeignKey(ua => ua.VerbTypeId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(ua => ua.PermissionScheme)
                    .WithMany()
                    .HasForeignKey(ua => ua.PermissionSchemeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AuditLog configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(al => al.Id);
                entity.Property(al => al.EntityType).IsRequired().HasMaxLength(50);
                entity.Property(al => al.ChangeType).IsRequired().HasMaxLength(10);
                entity.Property(al => al.ChangedBy).IsRequired().HasMaxLength(100);
                entity.Property(al => al.ChangeDate).IsRequired();
                entity.Property(al => al.ChangeDetails).IsRequired().HasMaxLength(4000);
            });
        }
    }
}
