using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Identity;

/// <summary>
/// FR-003 — a named permission grant. Granted to roles; users hold
/// permissions transitively via their role memberships. The
/// <see cref="Code"/> is the canonical identifier (e.g.
/// <c>Invoice.Sales.PostDirect</c>); <see cref="Description"/> is the
/// bilingual user-facing label rendered in role-management screens.
/// </summary>
#pragma warning disable CA1711 // "Permission" is the canonical domain name; not an attribute.
public sealed class Permission
#pragma warning restore CA1711
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Description { get; init; }

    private Permission() { }

    public Permission(string code, ArabicEnglishText description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Description = description;
    }
}
