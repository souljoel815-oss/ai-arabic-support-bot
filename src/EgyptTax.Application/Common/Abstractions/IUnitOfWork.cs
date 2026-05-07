namespace EgyptTax.Application.Common.Abstractions;

/// <summary>
/// Transaction-scoped unit of work used by <c>TransactionBehavior</c>. The
/// EF Core implementation in Infrastructure wraps
/// <c>DbContext.Database.BeginTransactionAsync</c>; tests substitute via
/// NSubstitute. Per INV-001 the transaction commits AFTER the audit row
/// is appended, so AuditEmitBehavior is registered INSIDE
/// TransactionBehavior in the pipeline.
/// </summary>
public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
