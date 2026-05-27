using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TransactionManager.IntegrationTests.Data;

namespace TransactionManager.IntegrationTests.SqlServer
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=localhost,1433;Database=TransactionManagerIntegrationTests;User Id=sa;Password=Your_password123;TrustServerCertificate=True");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
