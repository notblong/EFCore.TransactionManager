using Microsoft.EntityFrameworkCore;
using TransactionManager.IntegrationTests.Models;

namespace TransactionManager.IntegrationTests.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Inventory> Inventories => Set<Inventory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(e =>
            {
                e.HasKey(x => x.Id);
                
                e.Property(x => x.TotalAmount).HasPrecision(18, 2);
                
                e.Property(x => x.Status).HasMaxLength(50);
                
                e.Property(x => x.CustomerName).HasMaxLength(200);
                
                e.HasMany(x => x.Items)
                 .WithOne(x => x.Order)
                 .HasForeignKey(x => x.OrderId);

                e.HasOne(x => x.AuditLog)
                 .WithOne(x => x.Order)
                 .HasForeignKey<AuditLog>(x => x.OrderId);
            });

            modelBuilder.Entity<OrderItem>(e =>
            {
                e.HasKey(x => x.Id);
                
                e.Property(x => x.UnitPrice).HasPrecision(18, 2);
                
                e.Property(x => x.ProductName).HasMaxLength(200);
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Action).HasMaxLength(100);
                
                e.Property(x => x.PerformedBy).HasMaxLength(200);
            });

            modelBuilder.Entity<Inventory>(e =>
            {
                e.HasKey(x => x.Id);
                
                e.Property(x => x.ProductName).HasMaxLength(200);
            });
        }
    }

}
