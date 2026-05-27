using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace TransactionManager.Core
{
    public interface ITransactionManager<TContext> where TContext : DbContext
    {
        Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default);
    }

    public class TransactionManager<TContext>(TContext context)
        : ITransactionManager<TContext> where TContext : DbContext
    {
        private IDbContextTransaction? _currentTransaction;

        public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default)
        {
            bool isOwner = _currentTransaction is null;
            if (isOwner)
            {
                _currentTransaction = await context.Database.BeginTransactionAsync(ct);
            }

            return new TransactionScope(
                isOwner: isOwner,
                onCommit: CommitInternalAsync,
                onRollback: RollbackInternalAsync
            );
        }

        private async Task CommitInternalAsync(bool isOwner, CancellationToken ct)
        {
            if (isOwner && _currentTransaction is not null)
            {
                await _currentTransaction.CommitAsync(ct);
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        private async Task RollbackInternalAsync(bool isOwner, CancellationToken ct)
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.RollbackAsync(ct);
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }
}
