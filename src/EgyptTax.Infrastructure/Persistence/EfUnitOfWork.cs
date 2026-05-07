using EgyptTax.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EgyptTax.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed unit of work used by <c>TransactionBehavior</c>. Each
/// scoped <see cref="EfUnitOfWork"/> holds at most one
/// <see cref="IDbContextTransaction"/>; nested calls within the same
/// scope reuse the existing transaction (Behaviour wraps every command
/// at the outermost layer of the pipeline).
/// </summary>
public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork, IAsyncDisposable
{
    private readonly AppDbContext _db = db;
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            return;
        }
        _transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
