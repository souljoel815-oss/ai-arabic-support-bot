using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Persistence;

/// <summary>
/// Single application <see cref="DbContext"/>. Aggregates land in their owning
/// stage's task batch (US1 master data, US2 documents, US3 audit log, etc.).
/// Schemas the audit + audit_meta layers will live in are created by the
/// initial EF migration (T025).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ValueObjectConversions.RegisterAll(modelBuilder);

        // Apply all IEntityTypeConfiguration<> classes discovered in this
        // assembly. Entity configurations land alongside their domain
        // aggregates as Stage 2-onward tasks introduce them.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
