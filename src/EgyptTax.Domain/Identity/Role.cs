using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Identity;

/// <summary>
/// FR-001 — a named permission bundle (Administrator, Accountant,
/// Bookkeeper, Approver, Auditor). The <see cref="RequiresMfa"/> flag is
/// true for the elevated roles per FR-002; the seeded role set turns
/// it on for Administrator and Approver.
/// </summary>
public sealed class Role
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public bool RequiresMfa { get; init; }

    public ICollection<Permission> Permissions { get; init; } = new List<Permission>();

    private Role() { }

    public Role(string code, ArabicEnglishText name, bool requiresMfa)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Name = name;
        RequiresMfa = requiresMfa;
    }
}
