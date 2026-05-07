// Stage 1+2 entry point. The MediatR pipeline (T037) and the FR-039
// cookie-authentication scheme (T050) are wired here. Full Blazor /
// Hangfire wiring lands as Stage 2 progresses (T054, T058, T073-T076).

using EgyptTax.Application;
using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.Identity;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.Web;
using EgyptTax.Web.Tools;
using Microsoft.AspNetCore.Authentication.Cookies;

if (AdminRecover.IsRecoveryInvocation(args))
{
    return await AdminRecoveryHost.RunAsync(args, CancellationToken.None);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IMfaSecretProtector, DataProtectionMfaSecretProtector>();

// FR-039 — cookie authentication. The cookie carries only the session
// id; absolute expiry, revocation, and audit emission live on the
// server-side Session row via SessionService.ValidateAsync (called
// from OnValidatePrincipal in the production wire-up that lands when
// the Blazor surface ships in T058+).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "egtsess";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = SessionService.DefaultInactivityWindow;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () =>
    "EgyptTax — Stage 1+2 scaffold. MediatR pipeline + cookie auth wired; full Blazor application ships in subsequent stages.");

app.Run();
return 0;
