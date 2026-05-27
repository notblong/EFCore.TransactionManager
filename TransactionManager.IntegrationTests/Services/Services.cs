using Microsoft.EntityFrameworkCore;
using TransactionManager.Core;
using TransactionManager.IntegrationTests.Data;
using TransactionManager.IntegrationTests.Models;

namespace TransactionManager.IntegrationTests.Services;

// -----------------------------------------------------------------------
// StatusService — tx-aware, can run standalone OR be nested inside outer
// -----------------------------------------------------------------------
public class StatusService(
    ITransactionManager<AppDbContext> txManager,
    AppDbContext context)
{
    public async Task UpdateStatusAsync(int orderId, string status, CancellationToken ct = default)
    {
        await using var scope = await txManager.BeginTransactionAsync(ct);

        var order = await context.Orders.FindAsync([orderId], ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        order.Status = status;
        await context.SaveChangesAsync(ct);

        await scope.CommitAsync(ct);

        Console.WriteLine($"    [StatusService] Order {orderId} status → '{status}' (IsOwner={scope.IsOwner})");
    }
}

// -----------------------------------------------------------------------
// AuditService — NOT tx-aware, plain DbContext usage
// Automatically enrolls in ambient transaction if one exists
// -----------------------------------------------------------------------
public class AuditService(AppDbContext context)
{
    public async Task LogAsync(int orderId, string action, string by, CancellationToken ct = default)
    {
        context.AuditLogs.Add(new AuditLog
        {
            OrderId = orderId,
            Action = action,
            PerformedBy = by,
            PerformedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);

        Console.WriteLine($"    [AuditService] Logged '{action}' by '{by}' for order {orderId} (no tx awareness)");
    }
}

// -----------------------------------------------------------------------
// InventoryService — NOT tx-aware, plain DbContext usage
// -----------------------------------------------------------------------
public class InventoryService(AppDbContext context)
{
    public async Task DeductStockAsync(string productName, int quantity, CancellationToken ct = default)
    {
        var inventory = await context.Inventories
            .FirstOrDefaultAsync(x => x.ProductName == productName, ct)
            ?? throw new InvalidOperationException($"Product '{productName}' not found in inventory.");

        if (inventory.Stock < quantity)
            throw new InvalidOperationException(
                $"Insufficient stock for '{productName}': available={inventory.Stock}, requested={quantity}.");

        inventory.Stock -= quantity;
        await context.SaveChangesAsync(ct);

        Console.WriteLine($"    [InventoryService] Deducted {quantity} units of '{productName}', remaining={inventory.Stock}");
    }

    public async Task<int> GetStockAsync(string productName, CancellationToken ct = default)
    {
        var inventory = await context.Inventories
            .FirstOrDefaultAsync(x => x.ProductName == productName, ct);
        return inventory?.Stock ?? -1;
    }
}

// -----------------------------------------------------------------------
// OrderService — owns the outer transaction, orchestrates all services
// -----------------------------------------------------------------------
public class OrderService(
    ITransactionManager<AppDbContext> txManager,
    AppDbContext context,
    StatusService statusService,
    AuditService auditService,
    InventoryService inventoryService)
{
    public async Task<Order> CreateOrderAsync(
        string customerName,
        List<(string Product, int Qty, decimal Price)> items,
        CancellationToken ct = default)
    {
        await using var scope = await txManager.BeginTransactionAsync(ct);

        try
        {
            var order = new Order
            {
                CustomerName = customerName,
                TotalAmount = items.Sum(i => i.Qty * i.Price),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Items = items.Select(i => new OrderItem
                {
                    ProductName = i.Product,
                    Quantity = i.Qty,
                    UnitPrice = i.Price
                }).ToList()
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync(ct);

            Console.WriteLine($"    [OrderService] Created order #{order.Id} for '{customerName}'");

            // Deduct inventory (plain DbContext service — enrolls in ambient tx)
            foreach (var (product, qty, _) in items)
            {
                await inventoryService.DeductStockAsync(product, qty, ct);
            }

            // Update status (tx-aware service — joins existing tx, IsOwner=false)
            await statusService.UpdateStatusAsync(order.Id, "Confirmed", ct);

            // Audit log (plain DbContext service — enrolls in ambient tx)
            await auditService.LogAsync(order.Id, "OrderCreated", customerName, ct);

            await scope.CommitAsync(ct);

            Console.WriteLine($"    [OrderService] Transaction committed for order #{order.Id}");
            return order;
        }
        catch (Exception ex)
        {
            await scope.RollbackAsync(CancellationToken.None);
            Console.WriteLine($"    [OrderService] Transaction rolled back.");
            throw;
        }
    }

    public async Task CancelOrderAsync(int orderId, string by, CancellationToken ct = default)
    {
        await using var scope = await txManager.BeginTransactionAsync(ct);

        try
        {
            // Status update (tx-aware, joins outer)
            await statusService.UpdateStatusAsync(orderId, "Cancelled", ct);

            // Audit (plain DbContext, joins outer)
            await auditService.LogAsync(orderId, "OrderCancelled", by, ct);

            await scope.CommitAsync(ct);
            Console.WriteLine($"    [OrderService] Cancellation committed for order #{orderId}");
        }
        catch
        {
            await scope.RollbackAsync(CancellationToken.None);
            Console.WriteLine($"    [OrderService] Cancellation rolled back.");
            throw;
        }
    }
}
