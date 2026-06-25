using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;
using TransactionManager.Core;
using TransactionManager.IntegrationTests.Data;
using TransactionManager.IntegrationTests.Seeding;
using TransactionManager.IntegrationTests.Services;

namespace TransactionManager.IntegrationTests.PostgreSql;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();

        services.AddDbContextFactory<AppDbContext>(opt =>
            opt.UseNpgsql(_container.GetConnectionString()));

        services.AddScoped<ITransactionManager<AppDbContext>, TransactionManager<AppDbContext>>();
        services.AddScoped<StatusService>();
        services.AddScoped<AuditService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<OrderService>();

        ServiceProvider = services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var seedResult = await DatabaseSeeder.SeedAsync(ServiceProvider);
        if (!seedResult.Succeeded)
            throw new InvalidOperationException($"Database seeding failed: {seedResult.Exception?.Message}", seedResult.Exception);
    }

    public async Task DisposeAsync()
    {
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();

        await _container.DisposeAsync();
    }
}

[CollectionDefinition("PostgreSql")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlContainerFixture> { }
