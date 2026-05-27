namespace TransactionManager.Core
{
    public interface ITransactionScope : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
        bool IsOwner { get; }
    }

    public class TransactionScope(
        bool isOwner,
        Func<bool, CancellationToken, Task> onCommit,
        Func<bool, CancellationToken, Task> onRollback) : ITransactionScope
    {
        private bool _completed = false;

        public bool IsOwner { get; } = isOwner;

        public async Task CommitAsync(CancellationToken ct = default)
        {
            _completed = true;
            await onCommit(IsOwner, ct);
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            _completed = true;
            await onRollback(IsOwner, ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                await onRollback(IsOwner, CancellationToken.None);
            }
        }
    }
}
