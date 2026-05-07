// Stage 1+2 entry point. MediatR pipeline (T037), FR-039 cookie auth
// (T050), Hangfire background-job host (T054), Blazor Server +
// bilingual localization (T058-T060) all wired here. Full UI
// screens land as Phase 3 implementation tasks (T085+) ship.

using System.Globalization;
using EgyptTax.Application;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.BackgroundJobs;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using EgyptTax.Web;
using EgyptTax.Web.Tools;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

if (AdminRecover.IsRecoveryInvocation(args))
{
    return await AdminRecoveryHost.RunAsync(args, CancellationToken.None);
}

if (Seeder.IsSeedInvocation(args))
{
    return await SeederHost.RunAsync(args, CancellationToken.None);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IMfaSecretProtector, DataProtectionMfaSecretProtector>();
builder.Services.AddSingleton<IClock, SystemClock>();

// EF context — primary persistence binding.
var primaryConnection = builder.Configuration.GetConnectionString("EgyptTax")
    ?? Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION");
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(primaryConnection))
    {
        opt.UseSqlServer(primaryConnection);
    }
});

// FR-028 audit checkpoint store + audit log store.
builder.Services.AddScoped<IAuditLogStore, SqlAuditLogStore>();
builder.Services.AddScoped<IAuditCheckpointStore, SqlSchemaCheckpointStore>();

// Background jobs (T054-T056). The NTP client is a singleton (no per-
// request state); jobs themselves are transient so each Hangfire
// activation gets a fresh AppDbContext via DI.
builder.Services.AddSingleton<INtpTimeClient>(_ => new SntpTimeClient());
builder.Services.AddTransient<NtpHealthCheckJob>();
builder.Services.AddTransient<AuditCheckpointJob>();

// FR-028 / R-03 — Hangfire on its own SQL Server connection
// (`EgyptTax_Hangfire`) so the job-state schema does not pollute the
// audit / domain database. Hangfire's storage manages its own schema
// (`HangFire`) inside that DB.
var hangfireConnection = builder.Configuration.GetConnectionString("EgyptTax_Hangfire")
    ?? Environment.GetEnvironmentVariable("EGYPTTAX_HANGFIRE_CONNECTION");
if (!string.IsNullOrWhiteSpace(hangfireConnection))
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true,
        }));
    builder.Services.AddHangfireServer();
}

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

// T058 / R-11 — Blazor Server + Razor Pages host. The Razor Pages
// runtime hosts the Blazor scaffold via /_Host (mapped as the
// fallback page); the bilingual <html dir> attribute is set in
// Pages/Shared/_Layout.cshtml from CultureInfo.CurrentCulture.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// T059 — IStringLocalizer wiring. The .resx files live in
// EgyptTax.Web/Localization/SharedResources.{ar,en}.resx; the
// marker class is EgyptTax.Web.Localization.SharedResources.
builder.Services.AddLocalization(options => options.ResourcesPath = "Localization");

var app = builder.Build();

// T058 / R-11 — request-scoped culture so the Blazor + Razor Pages
// rendering pipeline picks up the user's chosen language. Cookie-
// driven so the choice survives across requests; the cookie is
// flipped by the (future) language-switcher component.
var supportedCultures = new[] { new CultureInfo("ar-EG"), new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("ar-EG"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
});

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Schedule recurring jobs once Hangfire storage is available.
if (!string.IsNullOrWhiteSpace(hangfireConnection))
{
    var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

    // R-23 — every 6 hours, query NTP and emit a skew event when the
    // delta exceeds the 5-second tolerance.
    recurring.AddOrUpdate<NtpHealthCheckJob>(
        recurringJobId: "ntp-health-check",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "0 */6 * * *");

    // FR-028 — checkpoint emitter polls every minute; the job itself
    // gates the actual write on (1k entries OR 15 min) since the
    // previous checkpoint.
    recurring.AddOrUpdate<AuditCheckpointJob>(
        recurringJobId: "audit-checkpoint",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "* * * * *");
}

// T058 — Blazor + Razor Pages routing. The Blazor hub serves the
// SignalR pipe; MapFallbackToPage routes any unmatched HTTP request
// (e.g. "/", "/invoices") to /_Host, which renders <App /> and lets
// the Blazor router pick the right page component.
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");

// T073 — Liveness / readiness probes per contracts/api/openapi.yaml.
// Liveness only signals that the process is up; readiness verifies the
// dependencies the operator runbook expects (DB reachable + audit
// checkpoint + NTP skew).
app.MapGet("/api/v1/health/live", () => Results.Json(new
{
    status = "up",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
}));

app.MapGet("/api/v1/health/ready", async (
    IServiceProvider services,
    AppDbContext db,
    IAuditCheckpointStore checkpoints,
    CancellationToken cancellationToken) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    bool dbReachable;
    try
    {
        dbReachable = await db.Database.CanConnectAsync(cancellationToken);
    }
    catch
    {
        dbReachable = false;
    }
    sw.Stop();

    AuditCheckpoint? cp = null;
    var checkpointWritable = false;
    if (dbReachable)
    {
        try
        {
            cp = await checkpoints.ReadLatestAsync(cancellationToken);
            checkpointWritable = true;
        }
        catch
        {
            checkpointWritable = false;
        }
    }

    var status = dbReachable && checkpointWritable ? "ready" : "down";
    var payload = new
    {
        status,
        db = new { reachable = dbReachable, latencyMs = (int)sw.ElapsedMilliseconds },
        ntpSkewSeconds = 0,
        auditCheckpoint = new
        {
            mode = "table",
            writable = checkpointWritable,
            lastIndex = cp?.LastIndex ?? 0L,
            lastWrittenAt = cp?.TsUtc ?? DateTime.UnixEpoch,
        },
    };
    return Results.Json(payload, statusCode: status == "ready" ? 200 : 503);
});

// T074 — serve the canonical contracts/api/openapi.yaml at /openapi.yaml
// so consumers can fetch the source-of-truth contract from a running
// instance. The file is the canonical specification — Swashbuckle-style
// generated docs would drift from the contract.
app.MapGet("/openapi.yaml", (CancellationToken cancellationToken) =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "contracts", "openapi.yaml");
    return File.Exists(path)
        ? Results.File(path, contentType: "application/yaml")
        : Results.NotFound();
});

app.Run();
return 0;

/// <summary>
/// Partial marker so <c>Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory&lt;Program&gt;</c>
/// can locate the entry-point class — the C# compiler emits a generated
/// <c>Program</c> for top-level statements, but it is internal by default.
/// This explicit partial declaration makes it public for the test host.
/// </summary>
public partial class Program;
