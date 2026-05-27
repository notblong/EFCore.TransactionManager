using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionManager.Core;
using TransactionManager.IntegrationTests;
using TransactionManager.IntegrationTests.Data;
using TransactionManager.IntegrationTests.Seeding;
using TransactionManager.IntegrationTests.Services;

var connectionString = "Host=localhost;Port=5432;Database=txdemo;Username=postgres;Password=demo1234";

var services = new ServiceCollection();

services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

services.AddScoped<ITransactionManager<AppDbContext>, TransactionManager<AppDbContext>>();
services.AddScoped<StatusService>();
services.AddScoped<AuditService>();
services.AddScoped<InventoryService>();
services.AddScoped<OrderService>();

var provider = services.BuildServiceProvider();
//var seedResult = await DatabaseSeeder.SeedAsync(provider);
//if (!seedResult.Succeeded)
//{
//    return 1;
//}

var runner = new TestRunner(provider);
await runner.RunAllAsync();

return 0;
