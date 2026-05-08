using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EgyptTax.Infrastructure.Persistence;

/// <summary>
/// Centralised EF Core value-object conversions for the SharedKernel
/// primitives. Called from <see cref="AppDbContext.OnModelCreating"/> via
/// <see cref="RegisterAll"/>. Per the project's Clean-Architecture boundary,
/// the Domain owns the value objects; Infrastructure owns how they map to
/// columns.
/// </summary>
public static class ValueObjectConversions
{
    /// <summary>SQL column type for <see cref="MoneyEgp"/>: decimal(19,4).</summary>
    public const string MoneyEgpColumnType = "decimal(19,4)";

    /// <summary>Converter: <see cref="MoneyEgp"/> &lt;-&gt; <see cref="decimal"/>.</summary>
    public static readonly ValueConverter<MoneyEgp, decimal> MoneyEgpConverter = new(
        v => v.Amount,
        v => MoneyEgp.From(v)
    );

    /// <summary>Converter: <see cref="EgyptianTin"/> &lt;-&gt; <see cref="string"/>.</summary>
    public static readonly ValueConverter<EgyptianTin, string> EgyptianTinConverter = new(
        v => v.Value,
        v => EgyptianTin.Parse(v)
    );

    /// <summary>
    /// Apply default conversions and column types globally for the registered
    /// value-object primitive types. Entity-type configurations may override
    /// individual properties when they need special precision/length.
    /// </summary>
    public static void RegisterAll(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(MoneyEgp))
                {
                    property.SetValueConverter(MoneyEgpConverter);
                    property.SetColumnType(MoneyEgpColumnType);
                }
                else if (property.ClrType == typeof(EgyptianTin))
                {
                    property.SetValueConverter(EgyptianTinConverter);
                    property.SetMaxLength(9);
                    property.SetIsUnicode(false);
                }
            }
        }

        // ArabicEnglishText is a value-object struct (readonly record struct).
        // Per-aggregate entity configurations introduced in US1 (T086+) map
        // it via EF Core 8's ComplexProperty API to two columns
        // ({baseName}_Arabic, {baseName}_English). Centralising here would
        // either require ArabicEnglishText to be a reference type (changes
        // the SharedKernel design) or require ComplexProperty<T> calls that
        // need a property selector specific to each entity. Either is
        // pre-mature; entity configurations handle it when they need it.
    }
}
