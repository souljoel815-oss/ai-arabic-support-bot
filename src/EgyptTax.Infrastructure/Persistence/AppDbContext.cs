using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
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
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ValueObjectConversions.RegisterAll(modelBuilder);

        // Apply all IEntityTypeConfiguration<> classes discovered in this
        // assembly. Entity configurations land alongside their domain
        // aggregates as Stage 2-onward tasks introduce them.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Single-file portable mode runs on SQLite. The entity
        // configurations declare SQL-Server-only column types
        // ("datetime2(3)", "decimal(19,2)", "nvarchar(max)", "date").
        // SQLite has dynamic type affinity so it would silently store
        // values incorrectly (datetime2 → no affinity → BLOB). Strip
        // the explicit types here and let EF's SQLite type mapper pick
        // the right storage type per CLR property.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                // SQLite has no schemas — strip the SQL-Server-style
                // schema annotations so the table name doesn't end up
                // as the literal "audit.audit_log" with a dot in it.
                entity.SetSchema(null);

                foreach (var property in entity.GetProperties())
                {
                    var colType = property.GetColumnType();
                    if (string.IsNullOrEmpty(colType)) continue;
                    if (colType.StartsWith("datetime2", StringComparison.OrdinalIgnoreCase)
                        || colType.StartsWith("decimal", StringComparison.OrdinalIgnoreCase)
                        || colType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)
                        || colType.Equals("date", StringComparison.OrdinalIgnoreCase))
                    {
                        property.SetColumnType(null);
                    }
                }

                // Translate filtered-index WHERE clauses from SQL Server
                // syntax to SQLite. Patterns we encounter in this code
                // base: "[col] IS NOT NULL", "[col] IS NULL", and
                // "[col] = N'literal'". SQLite uses double-quoted
                // identifiers and has no N'...' Unicode prefix.
                foreach (var index in entity.GetIndexes())
                {
                    var filter = index.GetFilter();
                    if (string.IsNullOrEmpty(filter)) continue;
                    var rewritten = System.Text.RegularExpressions.Regex.Replace(
                        filter,
                        @"\[([^\]]+)\]",
                        "\"$1\"");
                    rewritten = System.Text.RegularExpressions.Regex.Replace(
                        rewritten,
                        @"N'([^']*)'",
                        "'$1'");
                    index.SetFilter(rewritten);
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
