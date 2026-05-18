using System.Security.Claims;
using EgyptTax.Portal.Application.Organisations;
using EgyptTax.Portal.Infrastructure.Identity;
using EgyptTax.Portal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Portal.Web.Middleware;

/// <summary>
/// T029 per FR-021. Resolves the active <see cref="IOrganisationContext"/> for
/// every authenticated portal request. Reads the signed-in PortalUser, looks
/// up their active (non-revoked) OrganisationMembership rows, picks the one
/// that matches the <c>org</c> route parameter (if present) OR the user's
/// only org (if there's exactly one).
///
/// If the request targets a <c>/portal/*</c> route AND the user is signed in
/// AND no matching active membership exists, the middleware short-circuits
/// with 403 — prevents a member of org A from reading org B's data via a
/// URL like <c>/portal/orgs/{orgB-id}/...</c>.
///
/// Only runs after the auth middleware has populated <see cref="HttpContext.User"/>.
/// Unauthenticated requests + non-portal routes pass through untouched —
/// org context is only meaningful for the authenticated portal surface.
/// </summary>
public sealed class OrganisationScopeMiddleware
{
    private const string PortalPathPrefix = "/portal";

    private readonly RequestDelegate _next;

    public OrganisationScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOrganisationContext orgContext,
        UserManager<PortalUser> userManager,
        PortalDbContext db)
    {
        if (!context.Request.Path.StartsWithSegments(PortalPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Unauthenticated portal request — the cookie-auth challenge
            // middleware will redirect to /Identity/Account/Login.
            await _next(context).ConfigureAwait(false);
            return;
        }

        var teamMemberIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(teamMemberIdClaim, out var teamMemberId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var user = await userManager.FindByIdAsync(teamMemberIdClaim).ConfigureAwait(false);
        if (user is null || user.SoftDeletedAtUtc is not null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Find an active membership. If the route carries an explicit
        // organisation id, match it; otherwise pick the single one the
        // user belongs to.
        Guid? requestedOrgId = null;
        if (context.Request.RouteValues.TryGetValue("organisationId", out var raw)
            && Guid.TryParse(raw?.ToString(), out var parsed))
        {
            requestedOrgId = parsed;
        }

        var activeMemberships = await db.OrganisationMemberships
            .AsNoTracking()
            .Where(m => m.TeamMemberId == teamMemberId && m.RevokedAtUtc == null && m.AcceptedAtUtc != null)
            .ToListAsync(context.RequestAborted)
            .ConfigureAwait(false);

        if (activeMemberships.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("No active organisation membership.", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var match = requestedOrgId.HasValue
            ? activeMemberships.FirstOrDefault(m => m.OrganisationId == requestedOrgId.Value)
            : (activeMemberships.Count == 1 ? activeMemberships[0] : null);

        if (match is null)
        {
            // Route asked for an org the user doesn't belong to — classic
            // URL-tampering attempt. 403 + log.
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Not a member of the requested organisation.", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        orgContext.SetScope(match.OrganisationId, teamMemberId, match.Role, user.DisplayName ?? user.Email ?? string.Empty);

        await _next(context).ConfigureAwait(false);
    }
}
