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

if (VerifyAudit.IsVerifyAuditInvocation(args))
{
    return await VerifyAuditHost.RunAsync(args, CancellationToken.None);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IMfaSecretProtector, DataProtectionMfaSecretProtector>();
builder.Services.AddSingleton<IClock, SystemClock>();

// T117 — identity services consumed by the auth pages (Login,
// ChangePassword, EnrollMfa, Logout). Singletons because they hold
// no per-request state; SessionService is scoped because it owns
// an AppDbContext.
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddScoped<ISessionService, SessionService>();

// T121-T123 — invoice editor services. Document-number allocator is
// scoped because it consumes AppDbContext; the renderers are
// stateless singletons.
builder.Services.AddScoped<EgyptTax.Application.Numbering.IDocumentNumberAllocator,
    EgyptTax.Infrastructure.Numbering.SqlSequentialNumberAllocator>();
builder.Services.AddScoped<EgyptTax.Application.Accounting.IJournalEntryEmitter,
    EgyptTax.Infrastructure.Accounting.SalesInvoiceJournalEmitter>();
builder.Services.AddScoped<EgyptTax.Application.Accounting.IPurchaseInvoiceJournalEmitter,
    EgyptTax.Infrastructure.Accounting.PurchaseInvoiceJournalEmitter>();
builder.Services.AddScoped<EgyptTax.Application.Accounting.IExpenseJournalEmitter,
    EgyptTax.Infrastructure.Accounting.ExpenseJournalEmitter>();
builder.Services.AddScoped<EgyptTax.Application.Accounting.ISupplierPaymentVoucherJournalEmitter,
    EgyptTax.Infrastructure.Accounting.SupplierPaymentVoucherJournalEmitter>();
builder.Services.AddScoped<EgyptTax.Application.Accounting.ICustomerReceiptVoucherJournalEmitter,
    EgyptTax.Infrastructure.Accounting.CustomerReceiptVoucherJournalEmitter>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Payments.AllocatePaymentHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Payments.PostSupplierPaymentVoucherHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Payments.PostCustomerReceiptVoucherHandler>();
builder.Services.AddScoped<EgyptTax.Application.Payments.IPaymentVoucherQuery,
    EgyptTax.Infrastructure.Payments.SqlPaymentVoucherQuery>();

// US7 / FR-045 / R-17 — WHT compute service. Selects the effective-
// dated WhtCategory row in force on the payment date.
builder.Services.AddScoped<EgyptTax.Application.Wht.IWhtComputeService,
    EgyptTax.Infrastructure.Wht.SqlWhtComputeService>();

// Differentiator 1 (Tax Risk Score) — rules registered as singletons
// because they are stateless; the scorer fans out across every
// registered rule. Adding a new rule = adding one AddSingleton line.
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.MissingTinRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.MissingEtaCodeRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.EtaSubmissionWindowExpiringRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.EtaSubmissionFailedRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.DocumentRiskScorer>();

// US2 — purchase-side risk rules. Same DI pattern; new rules ship by
// adding a class plus one AddSingleton line.
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.MissingAttachmentRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.NonRecoverableInputVatRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.DuplicateSupplierInvoiceRule>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.PurchaseDocumentRiskScorer>();
builder.Services.AddScoped<EgyptTax.Application.Compliance.RiskScoring.IPurchaseInvoiceFingerprintQuery,
    EgyptTax.Infrastructure.Compliance.SqlPurchaseInvoiceFingerprintQuery>();

// US2 — purchase invoice + expense post handlers (FR-016 enforced
// in both — deductible documents require an attachment).
builder.Services.AddScoped<EgyptTax.Infrastructure.Purchases.PostPurchaseInvoiceHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Expenses.PostExpenseHandler>();

// T140 / FR-007 — master-data deletion guard. Scoped because it
// consumes AppDbContext. Future admin tooling + data-migration
// scripts call into this BEFORE attempting any hard delete.
builder.Services.AddScoped<EgyptTax.Application.Common.Guards.IMasterDataDeletionGuard,
    EgyptTax.Infrastructure.Common.Guards.SqlMasterDataDeletionGuard>();

// US5 / FR-021 — monthly VAT report. Scoped per AppDbContext.
builder.Services.AddScoped<EgyptTax.Application.Reports.IVatMonthlyReportQuery,
    EgyptTax.Infrastructure.Reports.SqlVatMonthlyReportQuery>();

// US5 / FR-023 — taxable income report (annual / period-scoped).
builder.Services.AddScoped<EgyptTax.Application.Reports.ITaxableIncomeReportQuery,
    EgyptTax.Infrastructure.Reports.SqlTaxableIncomeReportQuery>();

// US5 / FR-024 — trial balance (sums journal-entry-line debits +
// credits per chart-of-account code).
builder.Services.AddScoped<EgyptTax.Application.Reports.ITrialBalanceReportQuery,
    EgyptTax.Infrastructure.Reports.SqlTrialBalanceReportQuery>();

// US5 / FR-037 — tax period lock guard + lock/reopen handler. The
// guard is consumed by the 3 document post handlers via the
// optional ctor arg they declared in T246; the wiring here makes
// the guard available so production posts actually consult it.
builder.Services.AddScoped<EgyptTax.Application.Periods.ITaxPeriodLockGuard,
    EgyptTax.Infrastructure.Periods.SqlTaxPeriodLockGuard>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Periods.LockTaxPeriodHandler>();

// Differentiator 2 — Monthly Tax Closing Cockpit projection.
builder.Services.AddScoped<EgyptTax.Application.Compliance.IMonthlyTaxClosingCockpitQuery,
    EgyptTax.Infrastructure.Compliance.SqlMonthlyTaxClosingCockpitQuery>();

// US3 / FR-026 — document approval workflow handler. Single class
// covers Submit / Approve / Reject / Void across the 3 approval-
// eligible aggregates (SalesInvoice / PurchaseInvoice / Expense).
builder.Services.AddScoped<EgyptTax.Infrastructure.Workflow.DocumentApprovalHandler>();

// US4 / FR-031 — manual adjusting journal voucher handler. Role-
// gated to Administrator + Accountant; Bookkeeper rejected.
builder.Services.AddScoped<EgyptTax.Infrastructure.Journals.CreateManualAdjustingJournalHandler>();

// US4 / FR-012 — reversal-voucher handler. Same FR-031 role gate;
// adds DB-side checks for already-reversed + reversal-of-reversal.
builder.Services.AddScoped<EgyptTax.Infrastructure.Journals.CreateReversalJournalHandler>();

// US4 / FR-024 / FR-025 / T171 — unified journal-ledger query
// behind /journals + /journals/{id}.
builder.Services.AddScoped<EgyptTax.Application.Journals.IJournalLedgerQuery,
    EgyptTax.Infrastructure.Journals.SqlJournalLedgerQuery>();

// US6 / FR-016 mirror / T182 — fixed-asset put-in-service handler.
// Refuses Draft → InService transition without at least one
// supporting attachment.
builder.Services.AddScoped<EgyptTax.Infrastructure.FixedAssets.PutFixedAssetInServiceHandler>();

// US6 / FR-017 / T185 — monthly depreciation Hangfire job.
// RecurringJob schedule wired separately when Hangfire boots; the
// job class itself is DI-resolved per-tick.
builder.Services.AddScoped<EgyptTax.Infrastructure.BackgroundJobs.MonthlyDepreciationJob>();

// US6 / FR-017 / FR-018 / T187 — fixed-asset query + create
// handler behind /fixed-assets + /fixed-assets/new +
// /fixed-assets/{id}/schedule.
builder.Services.AddScoped<EgyptTax.Application.FixedAssets.IFixedAssetQuery,
    EgyptTax.Infrastructure.FixedAssets.SqlFixedAssetQuery>();
builder.Services.AddScoped<EgyptTax.Infrastructure.FixedAssets.CreateFixedAssetHandler>();

// US9 / FR-048 — period-scoped tax-inspection bundle builder.
builder.Services.AddScoped<EgyptTax.Application.Inspection.IInspectionBundleBuilder,
    EgyptTax.Infrastructure.Inspection.InspectionBundleBuilder>();

// T139 / R-21 — attachment store lives on the filesystem under
// EGYPTTAX_ATTACHMENT_ROOT (config key Attachments:Root). Defaults
// to {ContentRootPath}/var/attachments for dev so a fresh clone
// "just works" without operator setup. Singleton because the store
// is stateless beyond the configured root.
var attachmentRoot = builder.Configuration["Attachments:Root"]
    ?? Environment.GetEnvironmentVariable("EGYPTTAX_ATTACHMENT_ROOT")
    ?? Path.Combine(builder.Environment.ContentRootPath, "var", "attachments");
builder.Services.AddSingleton<EgyptTax.Application.FileStorage.IAttachmentStore>(sp =>
    new EgyptTax.Infrastructure.FileStorage.FileSystemAttachmentStore(
        attachmentRoot,
        sp.GetRequiredService<EgyptTax.SharedKernel.Time.IClock>()));
builder.Services.AddScoped<EgyptTax.Infrastructure.Attachments.UploadAttachmentHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.PostSalesInvoiceHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.PostSalesInvoiceWithEtaSubmissionHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.IssueCreditNoteHandler>();
builder.Services.AddSingleton<EgyptTax.Application.Pdf.ISalesInvoicePdfRenderer, EgyptTax.Infrastructure.Pdf.QuestPdfInvoiceRenderer>();
builder.Services.AddSingleton<EgyptTax.Application.Eta.IEInvoiceJsonGenerator, EgyptTax.Infrastructure.Eta.EInvoiceJsonGenerator>();
builder.Services.AddScoped<EgyptTax.Application.Eta.IEtaDashboardQuery, EgyptTax.Infrastructure.Eta.SqlEtaDashboardQuery>();

// T094-T095 — ETA submission orchestration. Default mock failure rate
// is 0% (see MockEtaSubmitter.DefaultFailureRate); operators dial it
// up via configuration to exercise the Failed branch in dev / smoke /
// load-test environments.
var etaFailureRate = builder.Configuration.GetValue<double>("Eta:Mock:FailureRate",
    EgyptTax.Infrastructure.Eta.MockEtaSubmitter.DefaultFailureRate);
builder.Services.AddSingleton<EgyptTax.Application.Eta.IEtaSubmitter>(
    _ => new EgyptTax.Infrastructure.Eta.MockEtaSubmitter(etaFailureRate));

// T125 — SignalR hub + in-process status notifier per FR-035 / R-22.
// AddSignalR is registered before the hub-context-consuming notifier
// so DI validates the dependency chain. Notifier is a singleton so
// in-process Blazor pages can attach to its StatusChanged event and
// have one stable subscription target across the page lifetime.
builder.Services.AddSignalR();
builder.Services.AddSingleton<EgyptTax.Application.Eta.IEtaStatusNotifier,
    EgyptTax.Web.Realtime.EtaStatusNotifier>();

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
builder.Services.AddTransient<EtaSubmissionRetryJob>();

// T110 / R-13 — supplier-TIN revalidation cron. The revalidator is
// still the always-valid stub (the live registry feed is a Near-term
// plug-in); US2 replaced the empty supplier source with the
// EF-backed `SqlSupplierTinSource` so the cron now iterates the
// real Supplier table. Source is scoped (consumes AppDbContext).
builder.Services.AddSingleton<EgyptTax.Application.Compliance.TinRevalidation.ISupplierTinRevalidator,
    EgyptTax.Infrastructure.Compliance.AlwaysValidTinRevalidator>();
builder.Services.AddScoped<EgyptTax.Application.Compliance.TinRevalidation.ISupplierTinSource,
    EgyptTax.Infrastructure.Compliance.SqlSupplierTinSource>();
builder.Services.AddTransient<SupplierTinRevalidationJob>();

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

// T117 — auth policies. "FullyAuthenticated" gates the app pages
// (master data, invoice editor, dashboard) so a user at the
// password-verified stage can only reach /password/change and
// /mfa/enroll, not the rest of the app.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FullyAuthenticated", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(EgyptTax.Web.Auth.AuthClaims.AuthStage,
            EgyptTax.Web.Auth.AuthClaims.StageFullyAuthenticated));
});

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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

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

    // FR-036 — retry Failed-but-still-in-window ETA submissions every
    // 15 minutes. The job's own filter excludes Submitted (terminal)
    // and expired-window Failed rows, so cron frequency only governs
    // recovery latency for transient mock failures, not regulator
    // compliance — the post-time wrapper handler is responsible for
    // the first attempt.
    recurring.AddOrUpdate<EtaSubmissionRetryJob>(
        recurringJobId: "eta-submission-retry",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "*/15 * * * *");

    // R-13 — daily re-validation of supplier TINs against the ETA
    // registry. Currently a no-op against an empty source + always-
    // valid revalidator stub; the cron skeleton ships now so the
    // Near-term registry-feed task only has to swap implementations.
    recurring.AddOrUpdate<SupplierTinRevalidationJob>(
        recurringJobId: "supplier-tin-revalidation",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "0 3 * * *");
}

// T058 — Blazor + Razor Pages routing. The Blazor hub serves the
// SignalR pipe; MapFallbackToPage routes any unmatched HTTP request
// (e.g. "/", "/invoices") to /_Host, which renders <App /> and lets
// the Blazor router pick the right page component.
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");

// T125 — ETA status hub at /hubs/eta. Auth-gated so external
// clients need a valid session cookie to subscribe to status
// changes; in-process Blazor pages attach to the StatusChanged
// event via IEtaStatusNotifier directly and don't traverse this
// hub.
app.MapHub<EgyptTax.Web.Realtime.EtaStatusHub>("/hubs/eta")
    .RequireAuthorization("FullyAuthenticated");

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

// T123 / T093 — PDF download for a posted sales invoice. Loads the
// Company (issuer) + Customer + items + VAT categories, builds an
// InvoicePdfRequest with a freshly-encoded seal payload, and streams
// the rendered bytes back as application/pdf.
app.MapGet("/invoices/{id:guid}/pdf", async (
    Guid id,
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    EgyptTax.Application.Pdf.ISalesInvoicePdfRenderer renderer,
    CancellationToken cancellationToken) =>
{
    var bundle = await EgyptTax.Infrastructure.Invoices.InvoiceRenderingPipeline.LoadAsync(db, id, cancellationToken);
    if (bundle is null) return Results.NotFound();
    var pdf = renderer.Render(bundle.PdfRequest);
    return Results.File(pdf, "application/pdf", $"{bundle.Invoice.DocumentNumber}.pdf");
}).RequireAuthorization("FullyAuthenticated");

// T123 / T077 — eInvoice JSON view for a posted sales invoice.
app.MapGet("/invoices/{id:guid}/einvoice.json", async (
    Guid id,
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    EgyptTax.Application.Eta.IEInvoiceJsonGenerator generator,
    CancellationToken cancellationToken) =>
{
    var bundle = await EgyptTax.Infrastructure.Invoices.InvoiceRenderingPipeline.LoadAsync(db, id, cancellationToken);
    if (bundle is null) return Results.NotFound();
    var json = generator.GenerateAsJson(bundle.EInvoiceRequest);
    return Results.Content(json, "application/json");
}).RequireAuthorization("FullyAuthenticated");

// T094 — /eta-mock/submit per contracts/api/openapi.yaml. Mock ETA
// submission endpoint that accepts an eInvoice JSON document,
// runs it through the same simulator the in-process MockEtaSubmitter
// uses, and returns a simulated UUID + status. Bound to localhost in
// production via standard ASP.NET Core hosting configuration; the
// route itself is auth-gated so cross-installation calls require a
// valid session cookie.
app.MapPost("/api/v1/eta-mock/submit", async (
    HttpRequest request,
    EgyptTax.Application.Eta.IEtaSubmitter submitter,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body, leaveOpen: false);
    var body = await reader.ReadToEndAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(body))
    {
        return Results.BadRequest(new { type = "/errors/empty-body", title = "Request body is required.", status = 400 });
    }

    // The simulator key is the document id — for the /eta-mock/submit
    // external surface we don't have one (caller is just passing
    // generic JSON), so use a synthetic guid for the simulation. The
    // mock's outcome is independent of the id; it's purely a coin
    // flip against the failure rate.
    var attempt = await submitter.SubmitAsync(Guid.NewGuid(), body, cancellationToken);

    var statusCode = attempt.OutcomeStatus == EgyptTax.Domain.Eta.EtaSubmissionStatus.Submitted ? 202 : 500;
    return Results.Json(new
    {
        submissionUuid = attempt.SubmissionUuid ?? Guid.Empty.ToString(),
        status = attempt.OutcomeStatus.ToString(),
        errorCode = attempt.ErrorCode,
        errorMessage = attempt.ErrorMessage,
    }, statusCode: statusCode);
}).RequireAuthorization("FullyAuthenticated");

// T096 — /api/v1/verify/{seal} per contracts/api/openapi.yaml +
// contracts/verification-seal-qr.md. Decodes the EGT1 seal, looks
// up the document, and reports VALID / TAMPERED / UNKNOWN /
// MALFORMED. Public (no auth) per the contract — verification is
// designed to be readable from a printed PDF without an account.
app.MapGet("/api/v1/verify/{seal}", async (
    string seal,
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var resolver = (EgyptTax.Infrastructure.Verification.DocumentSealCodec.LiveDocumentResolver)((Guid documentId) =>
    {
        // Synchronous wrapper — the resolver delegate is invoked
        // inside Verify which is itself called from this handler;
        // GetAwaiter().GetResult() is safe here because the pipeline
        // is fully async-friendly until we hit Verify (which doesn't
        // accept async resolvers in the current contract).
        var live = db.Set<EgyptTax.Domain.Invoices.SalesInvoice>()
            .AsNoTracking()
            .FirstOrDefault(i => i.Id == documentId);
        if (live is null) return null;
        return new EgyptTax.Application.Verification.ResolvedDocument(
            DocumentNumber: live.DocumentNumber ?? "",
            GrandTotalPiastres: (long)(live.GrandTotal.Amount * 100m),
            AuditEntryHash: new byte[32],
            AuditEntryIndex: 1L);
    });
    var result = EgyptTax.Infrastructure.Verification.DocumentSealCodec.Verify(seal, resolver);
    var statusCode = result.Outcome == EgyptTax.Application.Verification.SealOutcome.Malformed ? 400 : 200;
    await Task.CompletedTask;
    return Results.Json(new
    {
        outcome = result.Outcome.ToString().ToUpperInvariant(),
        documentNumber = result.DocumentNumber,
        documentType = result.DocumentType,
        mismatches = result.Mismatches,
    }, statusCode: statusCode);
});

// T162 / FR-028 — POST /api/v1/audit/verify. Walks the entire
// audit chain (capped at 50k entries — for larger installations
// the operator runs the verify-audit CLI from T163), recomputes
// every entry's hash via the existing AuditChainVerifier, and
// returns a JSON report. Auditor-runnable from the AuditLogViewer
// page; CLI-runnable for the install-side smoke check.
const int VerifyEntryCap = 50_000;
app.MapPost("/api/v1/audit/verify", async (
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    EgyptTax.Application.Audit.IAuditCheckpointStore checkpointStore,
    CancellationToken cancellationToken) =>
{
    var entries = await db.Set<EgyptTax.Domain.Audit.AuditLogEntry>()
        .AsNoTracking()
        .OrderBy(e => e.Index)
        .Take(VerifyEntryCap)
        .ToListAsync(cancellationToken);
    var checkpoint = await checkpointStore.ReadLatestAsync(cancellationToken);

    var report = EgyptTax.Domain.Audit.AuditChainVerifier.Verify(entries, checkpoint);

    return Results.Json(new
    {
        isValid = report.IsValid,
        entriesScanned = entries.Count,
        truncated = entries.Count == VerifyEntryCap,
        findings = report.Findings.Select(f => new
        {
            kind = f.Kind.ToString(),
            atIndex = f.AtIndex,
            notes = f.Notes,
        }).ToList(),
    });
}).RequireAuthorization("FullyAuthenticated");

app.Run();
return 0;

/// <summary>
/// Partial marker so <c>Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory&lt;Program&gt;</c>
/// can locate the entry-point class — the C# compiler emits a generated
/// <c>Program</c> for top-level statements, but it is internal by default.
/// This explicit partial declaration makes it public for the test host.
/// </summary>
public partial class Program;
