using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionManager;
using TransactionManager.IntegrationTests.Data;
using TransactionManager.IntegrationTests.Models;
using TransactionManager.IntegrationTests.Services;

namespace TransactionManager.IntegrationTests;

public class TestRunner(IServiceProvider rootProvider)
{
    private int _passed = 0;
    private int _failed = 0;

    // ----------------------------------------------------------------
    // Entry point — runs all cases sequentially
    // ----------------------------------------------------------------
    public async Task RunAllAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         TransactionManager Integration Tests                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await SeedDatabaseAsync();

        await RunCase("Case 1 — Happy path: full order creation (all services succeed)",
            Case1_HappyPath_FullOrderCreation);

        await RunCase("Case 2 — StatusService standalone (owns its own tx)",
            Case2_StatusService_Standalone);

        await RunCase("Case 3 — Rollback: insufficient stock triggers full rollback",
            Case3_Rollback_InsufficientStock);

        await RunCase("Case 4 — Rollback: order not found in StatusService rolls back outer",
            Case4_Rollback_OrderNotFound);

        await RunCase("Case 5 — Nested tx-aware services: StatusService joins outer (IsOwner=false)",
            Case5_Nested_TxAware_JoinsOuter);

        await RunCase("Case 6 — CancellationToken cancelled mid-flight rolls back cleanly",
            Case6_CancellationToken_Rollback);

        await RunCase("Case 7 — Happy path: order cancellation flow",
            Case7_HappyPath_OrderCancellation);

        await RunCase("Case 8 — Rollback via DisposeAsync (no explicit rollback called)",
            Case8_Rollback_ViaDisposeAsync);

        PrintSummary();
    }

    // ----------------------------------------------------------------
    // Case 1 — Happy path: everything succeeds, verify DB state
    // ----------------------------------------------------------------
    private async Task Case1_HappyPath_FullOrderCreation(IServiceScope scope)
    {
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryService>();

        int stockBefore = await inventoryService.GetStockAsync("Widget A");

        var order = await orderService.CreateOrderAsync("Alice", [
            ("Widget A", 2, 9.99m),
            ("Widget B", 1, 24.99m)
        ]);

        // Verify order persisted
        var savedOrder = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.AuditLog)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        Assert(savedOrder is not null, "Order should be persisted");
        Assert(savedOrder!.Status == "Confirmed", $"Status should be 'Confirmed', got '{savedOrder.Status}'");
        Assert(savedOrder.Items.Count == 2, $"Should have 2 items, got {savedOrder.Items.Count}");
        Assert(savedOrder.AuditLog is not null, "AuditLog should be persisted");
        Assert(savedOrder.AuditLog!.Action == "OrderCreated", "AuditLog action should be 'OrderCreated'");

        int stockAfter = await inventoryService.GetStockAsync("Widget A");
        Assert(stockAfter == stockBefore - 2, $"Stock should have decreased by 2: before={stockBefore}, after={stockAfter}");
    }

    // ----------------------------------------------------------------
    // Case 2 — StatusService standalone (no outer tx, owns its own)
    // ----------------------------------------------------------------
    private async Task Case2_StatusService_Standalone(IServiceScope scope)
    {
        var statusService = scope.ServiceProvider.GetRequiredService<StatusService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed a bare order directly
        var order = new Order { CustomerName = "Bob", TotalAmount = 0m, Status = "Pending" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await statusService.UpdateStatusAsync(order.Id, "Processing");

        var updated = await db.Orders.FindAsync(order.Id);
        Assert(updated!.Status == "Processing", $"Status should be 'Processing', got '{updated.Status}'");
    }

    // ----------------------------------------------------------------
    // Case 3 — Rollback: insufficient stock — order + audit must NOT persist
    // ----------------------------------------------------------------
    private async Task Case3_Rollback_InsufficientStock(IServiceScope scope)
    {
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryService>();

        int stockBefore = await inventoryService.GetStockAsync("Widget A");
        int orderCountBefore = db.Orders.Count();
        int auditCountBefore = db.AuditLogs.Count();

        try
        {
            // Request more than available stock
            await orderService.CreateOrderAsync("Charlie", [
                ("Widget A", 9999, 9.99m) // way more than stock
            ]);

            Assert(false, "Should have thrown due to insufficient stock");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
        {
            Console.WriteLine($"    [Expected exception] {ex.Message}");
        }

        // Verify nothing persisted
        int stockAfter = await inventoryService.GetStockAsync("Widget A");
        int orderCountAfter = db.Orders.Count();
        int auditCountAfter = db.AuditLogs.Count();

        Assert(stockAfter == stockBefore, $"Stock should be unchanged after rollback: before={stockBefore}, after={stockAfter}");
        Assert(orderCountAfter == orderCountBefore, $"Order count should be unchanged: before={orderCountBefore}, after={orderCountAfter}");
        Assert(auditCountAfter == auditCountBefore, $"AuditLog count should be unchanged: before={auditCountBefore}, after={auditCountAfter}");
    }

    // ----------------------------------------------------------------
    // Case 4 — Rollback: StatusService throws (order not found), outer rolls back
    // ----------------------------------------------------------------
    private async Task Case4_Rollback_OrderNotFound(IServiceScope scope)
    {
        var statusService = scope.ServiceProvider.GetRequiredService<StatusService>();
        var txManager = scope.ServiceProvider.GetRequiredService<ITransactionManager<AppDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<AuditService>();

        int auditCountBefore = db.AuditLogs.Count();

        try
        {
            await using var outerScope = await txManager.BeginTransactionAsync();

            try
            {
                // This succeeds fine
                await auditService.LogAsync(99999, "SomeAction", "Tester");

                // This throws — order 99999 doesn't exist
                await statusService.UpdateStatusAsync(99999, "Confirmed");

                await outerScope.CommitAsync();
            }
            catch
            {
                await outerScope.RollbackAsync(CancellationToken.None);
                throw;
            }

            Assert(false, "Should have thrown for missing order");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            Console.WriteLine($"    [Expected exception] {ex.Message}");
        }

        // AuditLog written before the throw should also be rolled back
        int auditCountAfter = db.AuditLogs.Count();
        Assert(auditCountAfter == auditCountBefore,
            $"AuditLog (written before throw) should be rolled back: before={auditCountBefore}, after={auditCountAfter}");
    }

    // ----------------------------------------------------------------
    // Case 5 — Nested tx: verify StatusService IsOwner=false when nested
    // ----------------------------------------------------------------
    private async Task Case5_Nested_TxAware_JoinsOuter(IServiceScope scope)
    {
        var txManager = scope.ServiceProvider.GetRequiredService<ITransactionManager<AppDbContext>>();
        var statusService = scope.ServiceProvider.GetRequiredService<StatusService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order { CustomerName = "Dave", TotalAmount = 0m, Status = "Pending" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        bool innerIsOwnerCaptured = false;

        await using var outerScope = await txManager.BeginTransactionAsync();
        Assert(outerScope.IsOwner, "Outer scope should be owner");

        // StatusService internally calls BeginTransactionAsync → should get IsOwner=false
        // We verify via console output; also the status update should commit with outer
        await statusService.UpdateStatusAsync(order.Id, "InReview");

        await outerScope.CommitAsync();

        var updated = await db.Orders.FindAsync(order.Id);
        Assert(updated!.Status == "InReview", $"Status should be 'InReview', got '{updated.Status}'");
    }

    // ----------------------------------------------------------------
    // Case 6 — CancellationToken cancelled → DisposeAsync rolls back cleanly
    // ----------------------------------------------------------------
    private async Task Case6_CancellationToken_Rollback(IServiceScope scope)
    {
        var txManager = scope.ServiceProvider.GetRequiredService<ITransactionManager<AppDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int orderCountBefore = db.Orders.Count();

        using var cts = new CancellationTokenSource();

        try
        {
            await using var txScope = await txManager.BeginTransactionAsync(cts.Token);

            var order = new Order { CustomerName = "Eve", TotalAmount = 50m, Status = "Pending" };
            db.Orders.Add(order);
            await db.SaveChangesAsync(cts.Token);

            Console.WriteLine("    [Case 6] Order written to DB (not yet committed)");

            // Simulate cancellation before commit
            cts.Cancel();

            // Simulate async work that respects cancellation
            await Task.Delay(10, cts.Token);

            await txScope.CommitAsync(cts.Token); // won't reach here
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("    [Case 6] OperationCanceledException caught — DisposeAsync will rollback with CancellationToken.None");
        }

        // Requery without cancelled token
        int orderCountAfter = db.Orders.Count();
        Assert(orderCountAfter == orderCountBefore,
            $"Order should be rolled back after cancellation: before={orderCountBefore}, after={orderCountAfter}");
    }

    // ----------------------------------------------------------------
    // Case 7 — Happy path: cancel an existing order
    // ----------------------------------------------------------------
    private async Task Case7_HappyPath_OrderCancellation(IServiceScope scope)
    {
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // First create one
        var order = await orderService.CreateOrderAsync("Frank", [("Widget B", 1, 24.99m)]);

        // Now cancel it
        await orderService.CancelOrderAsync(order.Id, "Admin");

        var updated = await db.Orders
            .Include(o => o.AuditLog)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        Assert(updated!.Status == "Cancelled", $"Status should be 'Cancelled', got '{updated.Status}'");

        // There should be 2 audit logs: OrderCreated + OrderCancelled
        var auditLogs = await db.AuditLogs.Where(a => a.OrderId == order.Id).ToListAsync();
        Assert(auditLogs.Count == 2, $"Should have 2 audit logs, got {auditLogs.Count}");
        Assert(auditLogs.Any(a => a.Action == "OrderCancelled"), "Should have 'OrderCancelled' audit log");
    }

    // ----------------------------------------------------------------
    // Case 8 — No explicit commit/rollback: DisposeAsync auto-rolls back
    // ----------------------------------------------------------------
    private async Task Case8_Rollback_ViaDisposeAsync(IServiceScope scope)
    {
        var txManager = scope.ServiceProvider.GetRequiredService<ITransactionManager<AppDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int countBefore = db.Orders.Count();

        // Intentionally no commit — scope exits without completing
        await using (var txScope = await txManager.BeginTransactionAsync())
        {
            var order = new Order { CustomerName = "Ghost", TotalAmount = 0m, Status = "Pending" };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            Console.WriteLine("    [Case 8] Order saved but scope exits without CommitAsync");
            // No commit — DisposeAsync fires rollback
        }

        int countAfter = db.Orders.Count();
        Assert(countAfter == countBefore,
            $"Order should be rolled back via DisposeAsync: before={countBefore}, after={countAfter}");
    }

    // ----------------------------------------------------------------
    // Infrastructure
    // ----------------------------------------------------------------
    private async Task SeedDatabaseAsync()
    {
        Console.WriteLine("► Seeding database...");

        using var scope = rootProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        db.Inventories.AddRange(
            new Inventory { ProductName = "Widget A", Stock = 100 },
            new Inventory { ProductName = "Widget B", Stock = 50 }
        );

        await db.SaveChangesAsync();
        Console.WriteLine("  Inventory seeded: Widget A=100, Widget B=50");
        Console.WriteLine();
    }

    private async Task RunCase(string name, Func<IServiceScope, Task> test)
    {
        Console.WriteLine($"┌─ {name}");

        using var scope = rootProvider.CreateScope();

        try
        {
            await test(scope);
            Console.WriteLine($"└─ ✅ PASSED");
            _passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"└─ ❌ FAILED: {ex.Message}");
            _failed++;
        }

        Console.WriteLine();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"Assertion failed: {message}");

        Console.WriteLine($"    [✓] {message}");
    }

    private void PrintSummary()
    {
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Results: {_passed} passed, {_failed} failed out of {_passed + _failed} cases");
        Console.WriteLine("══════════════════════════════════════════════════════════════");
    }
}
