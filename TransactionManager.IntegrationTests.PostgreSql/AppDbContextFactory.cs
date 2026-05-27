using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TransactionManager.IntegrationTests.Data;

namespace TransactionManager.IntegrationTests.PostgreSql
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(
                "Host=localhost;Port=5432;Database=txdemo;Username=postgres;Password=demo1234");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
