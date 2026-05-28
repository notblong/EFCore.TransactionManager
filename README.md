# TransactionManager

A simple lightweight nuget package to manage EF Core transaction in your .NET project.

A way to Commit or Rollback all changes by keeping all changes in a single transaction even some of changes are happened in nested methods.

```cs
// `OrderService.CreateOrderAsync`

// Explicit transaction via TransactionManager
await using var transaction = await transactionManager.BeginTransactionAsync(ct);
try
{
    var order = new Order();
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(ct);

    // Explicit transaction inside `UpdateStatusAsync`, that transaction will be joined into this `transaction`
    await statusService.UpdateStatusAsync(order.Id, "Confirmed", ct);

    // Implicit transaction — enrolls in ambient transaction
    await auditService.LogAsync(order.Id, "OrderCreated", customerName, ct);

    await transaction.CommitAsync(ct);

    return order;
}
catch (Exception ex)
{
    await transaction.RollbackAsync(CancellationToken.None);
    throw;
}
```

```cs
// `StatusService.UpdateStatusAsync`
await using var transaction = await transactionManager.BeginTransactionAsync(ct);

var order = await context.Orders.FindAsync([orderId], ct)
    ?? throw new InvalidOperationException($"Order {orderId} not found.");

order.Status = status;
await context.SaveChangesAsync(ct);

await transaction.CommitAsync(ct);
```

## How to use
Register via DI

```cs
// `Program.cs`
services.AddScoped<ITransactionManager<AppDbContext>, TransactionManager<AppDbContext>>();
```
