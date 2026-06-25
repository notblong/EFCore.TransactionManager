using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TransactionManager.IntegrationTests.SqlServer;

[Collection("SqlServer")]
public class SqlServerIntegrationTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task Case1_HappyPath_FullOrderCreation()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case1_HappyPath_FullOrderCreation(scope);
    }

    [Fact]
    public async Task Case2_StatusService_Standalone()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case2_StatusService_Standalone(scope);
    }

    [Fact]
    public async Task Case3_Rollback_InsufficientStock()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case3_Rollback_InsufficientStock(scope);
    }

    [Fact]
    public async Task Case4_Rollback_OrderNotFound()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case4_Rollback_OrderNotFound(scope);
    }

    [Fact]
    public async Task Case5_Nested_TxAware_JoinsOuter()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case5_Nested_TxAware_JoinsOuter(scope);
    }

    [Fact]
    public async Task Case6_CancellationToken_Rollback()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case6_CancellationToken_Rollback(scope);
    }

    [Fact]
    public async Task Case7_HappyPath_OrderCancellation()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case7_HappyPath_OrderCancellation(scope);
    }

    [Fact]
    public async Task Case8_Rollback_ViaDisposeAsync()
    {
        using var scope = fixture.ServiceProvider.CreateScope();
        var runner = new TestRunner();
        await runner.Case8_Rollback_ViaDisposeAsync(scope);
    }
}
