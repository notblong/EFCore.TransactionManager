using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionManager.IntegrationTests.Data;
using TransactionManager.IntegrationTests.Models;

namespace TransactionManager.IntegrationTests.Seeding
{
    public static class DatabaseSeeder
    {
        private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;

        public static async Task<SeedResult> SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            Console.WriteLine("Seed: starting database seed process.");

            try
            {
                await using var db = await services
                    .GetRequiredService<IDbContextFactory<AppDbContext>>()
                    .CreateDbContextAsync(ct);

                var seedPath = ResolveSeedPath();
                Console.WriteLine($"Seed: loading CSV files from {seedPath}");

                var inventories = ReadInventories(Path.Combine(seedPath, "Inventories.csv"));
                var orders = ReadOrders(Path.Combine(seedPath, "Orders.csv"));
                var orderItems = ReadOrderItems(Path.Combine(seedPath, "OrderItems.csv"));
                var auditLogs = ReadAuditLogs(Path.Combine(seedPath, "AuditLogs.csv"));

                await db.Database.EnsureCreatedAsync(ct);

                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                await ClearTablesAsync(db, ct);

                await InsertWithIdentityAsync(db, "Inventories", inventories, ct);
                await InsertWithIdentityAsync(db, "Orders", orders, ct);
                await InsertWithIdentityAsync(db, "OrderItems", orderItems, ct);
                await InsertWithIdentityAsync(db, "AuditLogs", auditLogs, ct);

                await ResetGeneratedKeysAsync(db, ct);

                await transaction.CommitAsync(ct);

                Console.WriteLine($"Seed: inventories inserted: {inventories.Count}");
                Console.WriteLine($"Seed: orders inserted: {orders.Count}");
                Console.WriteLine($"Seed: order items inserted: {orderItems.Count}");
                Console.WriteLine($"Seed: audit logs inserted: {auditLogs.Count}");
                Console.WriteLine("Seed: completed successfully.");
                Console.WriteLine();

                return SeedResult.Success();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Seed: failed.");
                Console.Error.WriteLine(ex);
                return SeedResult.Failure(ex);
            }
        }

        private static async Task ClearTablesAsync(AppDbContext db, CancellationToken ct)
        {
            Console.WriteLine("Seed: clearing existing data.");

            var providerName = db.Database.ProviderName ?? string.Empty;

            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [AuditLogs]", ct);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [OrderItems]", ct);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [Orders]", ct);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [Inventories]", ct);
                return;
            }

            await db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "AuditLogs";
                DELETE FROM "OrderItems";
                DELETE FROM "Orders";
                DELETE FROM "Inventories";
                """, ct);
        }

        private static async Task InsertWithIdentityAsync<TEntity>(
            AppDbContext db,
            string tableName,
            IReadOnlyCollection<TEntity> rows,
            CancellationToken ct)
            where TEntity : class
        {
            if (rows.Count == 0)
            {
                return;
            }

            var providerName = db.Database.ProviderName ?? string.Empty;
            var isSqlServer = providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);

            if (isSqlServer)
            {
                var identityInsertOnSql = $"SET IDENTITY_INSERT [{tableName}] ON";
                await db.Database.ExecuteSqlRawAsync(identityInsertOnSql, ct);
            }

            try
            {
                db.Set<TEntity>().AddRange(rows);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
            finally
            {
                if (isSqlServer)
                {
                    var identityInsertOffSql = $"SET IDENTITY_INSERT [{tableName}] OFF";
                    await db.Database.ExecuteSqlRawAsync(identityInsertOffSql, ct);
                }
            }
        }

        private static async Task ResetGeneratedKeysAsync(AppDbContext db, CancellationToken ct)
        {
            var providerName = db.Database.ProviderName ?? string.Empty;

            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                await db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('[Inventories]', RESEED, 200)", ct);
                await db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('[Orders]', RESEED, 300)", ct);
                await db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('[OrderItems]', RESEED, 400)", ct);
                await db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('[AuditLogs]', RESEED, 100)", ct);
                return;
            }

            if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                await db.Database.ExecuteSqlRawAsync("""
                    SELECT setval(pg_get_serial_sequence('"Inventories"', 'Id'), COALESCE(MAX("Id"), 1), true) FROM "Inventories";
                    SELECT setval(pg_get_serial_sequence('"Orders"', 'Id'), COALESCE(MAX("Id"), 1), true) FROM "Orders";
                    SELECT setval(pg_get_serial_sequence('"OrderItems"', 'Id'), COALESCE(MAX("Id"), 1), true) FROM "OrderItems";
                    SELECT setval(pg_get_serial_sequence('"AuditLogs"', 'Id'), COALESCE(MAX("Id"), 1), true) FROM "AuditLogs";
                    """, ct);
            }
        }

        private static string ResolveSeedPath()
        {
            var baseDirectory = AppContext.BaseDirectory;

            foreach (var path in GetCandidateSeedPaths(baseDirectory))
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            throw new DirectoryNotFoundException(
                $"Could not locate Seed directory. Started from '{baseDirectory}'.");
        }

        private static IEnumerable<string> GetCandidateSeedPaths(string baseDirectory)
        {
            var current = new DirectoryInfo(baseDirectory);

            while (current is not null)
            {
                yield return Path.Combine(current.FullName, "Seed");
                yield return Path.Combine(current.FullName, "TransactionManager.IntegrationTests", "Seed");
                current = current.Parent;
            }
        }

        private static List<Inventory> ReadInventories(string path)
        {
            return ReadDataLines(path)
                .Select(columns => new Inventory
                {
                    Id = int.Parse(columns[0], CsvCulture),
                    ProductName = columns[1],
                    Stock = int.Parse(columns[2], CsvCulture)
                })
                .ToList();
        }

        private static List<Order> ReadOrders(string path)
        {
            return ReadDataLines(path)
                .Select(columns => new Order
                {
                    Id = int.Parse(columns[0], CsvCulture),
                    CustomerName = columns[1],
                    TotalAmount = decimal.Parse(columns[2], CsvCulture),
                    Status = columns[3],
                    CreatedAt = DateTime.Parse(columns[4], CsvCulture, DateTimeStyles.AdjustToUniversal)
                })
                .ToList();
        }

        private static List<OrderItem> ReadOrderItems(string path)
        {
            return ReadDataLines(path)
                .Select(columns => new OrderItem
                {
                    Id = int.Parse(columns[0], CsvCulture),
                    OrderId = int.Parse(columns[1], CsvCulture),
                    ProductName = columns[2],
                    Quantity = int.Parse(columns[3], CsvCulture),
                    UnitPrice = decimal.Parse(columns[4], CsvCulture)
                })
                .ToList();
        }

        private static List<AuditLog> ReadAuditLogs(string path)
        {
            return ReadDataLines(path)
                .Select(columns => new AuditLog
                {
                    Id = int.Parse(columns[0], CsvCulture),
                    OrderId = int.Parse(columns[1], CsvCulture),
                    Action = columns[2],
                    PerformedBy = columns[3],
                    PerformedAt = DateTime.Parse(columns[4], CsvCulture, DateTimeStyles.AdjustToUniversal)
                })
                .ToList();
        }

        private static IEnumerable<string[]> ReadDataLines(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Seed CSV file was not found: {path}", path);
            }

            return File.ReadLines(path)
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(','));
        }
    }
}
