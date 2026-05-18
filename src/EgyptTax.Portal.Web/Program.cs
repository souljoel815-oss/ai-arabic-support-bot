using System.Globalization;
using EgyptTax.Portal.Infrastructure;
using EgyptTax.Portal.Infrastructure.Identity;
using EgyptTax.Portal.Web.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Serilog;

// T032 — Serilog bootstrap logger. Captures startup errors that happen
// before the full host is built.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DaftarX Portal Web");

    var builder = WebApplication.CreateBuilder(args);

    // T032 — full Serilog wiring with a daily file sink. Application
    // Insights sink is wired in production-only when the connection
    // string is set; deferred to the polish phase (T146) so the dev
    // boot stays portable.
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EgyptTax.Portal")
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "logs", "portal-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                formatProvider: CultureInfo.InvariantCulture);
    });

    // T017 — PortalDbContext (SQL Server in prod, SQLite override for dev).
    builder.Services.AddPortalDbContext(builder.Configuration);

    // T021 — AspNetCore.Identity. Email confirmation required (FR-010).
    builder.Services
        .AddIdentity<PortalUser, PortalRole>(options =>
        {
            options.SignIn.RequireConfirmedEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<EgyptTax.Portal.Infrastructure.Persistence.PortalDbContext>()
        .AddDefaultTokenProviders();

    // Separate cookie name so a dev box can boot the on-prem product
    // alongside the portal on localhost without colliding on the
    // default .AspNetCore.Identity.Application cookie name (FR-032).
    builder.Services.ConfigureApplicationCookie(opts =>
    {
        opts.Cookie.Name = ".DaftarXPortal.Identity";
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opts.ExpireTimeSpan = TimeSpan.FromDays(14);
        opts.SlidingExpiration = true;
        opts.LoginPath = "/Identity/Account/Login";
        opts.AccessDeniedPath = "/Identity/Account/AccessDenied";
    });

    // T020 + T027 + T028 + T029 + T031 — Infrastructure wires its own
    // service registrations so the Web layer doesn't need internal-class
    // visibility into Infrastructure.
    builder.Services.AddPortalInfrastructure(builder.Configuration);

    // T030 — locale routing. ar-EG default RTL, en-US fallback. URL-prefix
    // routing (/ar/* and /en/*) lands in Phase 3 (US1, T052) which adds
    // the Microsoft.AspNetCore.Localization.Routing package + the route
    // template. Phase 2 just configures the default culture so the layout
    // renders RTL ar-EG out of the box.
    var supportedCultures = new[] { new CultureInfo("ar-EG"), new CultureInfo("en-US") };
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture("ar-EG");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
    });
    builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");

    // T035 — two-surface routing: Razor Pages for marketing, Blazor Server for /portal/*.
    builder.Services
        .AddRazorPages()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddRouting(opts => opts.LowercaseUrls = true);

    // T036 — health checks. EF Core DbContext + Resend reachability
    // checks land in Phase 5 (T077+) when those integrations stabilise;
    // the Phase 2 health endpoint just answers 200 so the docker-compose
    // healthcheck has something to hit.
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();

    app.UseStaticFiles();

    app.UseRequestLocalization();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // T029 — must run after auth so HttpContext.User is populated.
    app.UseMiddleware<OrganisationScopeMiddleware>();

    // Marketing Razor Pages on /*. Blazor Server hub for the portal on /portal/*.
    // Host.cshtml carries @page "/portal/{*path:nonfile}" — Razor Pages
    // routing serves it for every portal URL, and the App.razor Router
    // resolves the path to the right component on the client side.
    app.MapRazorPages();
    app.MapBlazorHub();

    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Portal Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
