using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionManager.Core;
using TransactionManager.IntegrationTests;
using TransactionManager.IntegrationTests.Data;
using TransactionManager.IntegrationTests.Services;

var connectionString = "Host=localhost;Port=5432;Database=txdemo;Username=postgres;Password=demo1234";

var services = new ServiceCollection();

services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString));

services.AddScoped<ITransactionManager<AppDbContext>, TransactionManager<AppDbContext>>();
services.AddScoped<StatusService>();
services.AddScoped<AuditService>();
services.AddScoped<InventoryService>();
services.AddScoped<OrderService>();

var provider = services.BuildServiceProvider();
var runner = new TestRunner(provider);
await runner.RunAllAsync();
