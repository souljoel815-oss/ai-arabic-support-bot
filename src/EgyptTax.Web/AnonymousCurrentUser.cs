using EgyptTax.Application.Common.Abstractions;

namespace EgyptTax.Web;

/// <summary>
/// Stage 2 placeholder <see cref="ICurrentUser"/>. Until identity wiring
/// lands in T044+/T050, every web request is treated as anonymous so the
/// MediatR pipeline can resolve the dependency. Replaced by an
/// HttpContext-backed implementation when authentication ships.
/// </summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
    public Guid? CompanyId => null;
    public string? FirmName => null;
    public bool IsAuthenticated => false;
}
