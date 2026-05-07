// Stage 1+2 entry point. MediatR pipeline (T037), FR-039 cookie auth
// (T050), and Hangfire background-job host (T054) are wired here.
// Full Blazor surface lands as Stage 2 continues (T058, T073-T076).

using EgyptTax.Application;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.Identity;
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

var app = builder.Build();

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

app.MapGet("/", () =>
    "EgyptTax — Stage 1+2 scaffold. MediatR pipeline + cookie auth + Hangfire wired; full Blazor application ships in subsequent stages.");

app.Run();
return 0;
