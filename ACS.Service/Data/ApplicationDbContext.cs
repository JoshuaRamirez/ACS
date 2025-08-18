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
        public DbSet<Resource> Resources { get; set; }
        public DbSet<PermissionScheme> EntityPermissions { get; set; }
        public DbSet<UriAccess> UriAccesses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Additional configurations can be added here if necessary
        }
    }
}
