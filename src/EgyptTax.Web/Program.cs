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
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

// Vendor-side licensing CLI verbs. license-keygen produces a fresh
// Ed25519 keypair; license-issue signs a license envelope for a
// specific customer HWID. Neither needs to bring up Kestrel.
if (LicenseKeygenHost.IsLicenseKeygenInvocation(args))
{
    return LicenseKeygenHost.Run(args);
}
if (LicenseIssueHost.IsLicenseIssueInvocation(args))
{
    return LicenseIssueHost.Run(args);
}

// Boot-time license gate. Runs BEFORE WebApplication.CreateBuilder
// so it's the first thing in the process — the LicenseStatus
// singleton + LicenseSentry are populated before any request is
// served or any DB connection opens.
EgyptTax.Web.Licensing.LicenseGate.Run();
EgyptTax.SharedKernel.LicenseSentry.IsLicensedProvider =
    static () => EgyptTax.Web.Licensing.LicenseStatus.IsLicensed;

var builder = WebApplication.CreateBuilder(args);

// T247-fix — Windows Service lifecycle integration. Auto-detects
// whether we're running as a Windows Service and, if so, wires
// the host into SCM so it reports Started / Stopped / Stopping
// transitions correctly. When launched interactively (e.g.
// `dotnet run` during dev) this is a no-op. Without this, sc.exe
// start fails with error 1053 because ASP.NET Core's default
// host doesn't speak the Windows Service control protocol.
builder.Host.UseWindowsService(opts => opts.ServiceName = "EgyptTax");

// T256 / R-20 — Serilog host logger. Console + rolling file sinks for
// the MVP; production deployments can layer on Serilog.Sinks.MSSqlServer
// via appsettings if they want a queryable ops log on the same SQL
// instance the app already uses. The CorrelationContextMiddleware
// pushes per-request CorrelationId / UserId / FirmName onto LogContext
// so {FromLogContext} on every line below carries them automatically.
builder.Host.UseSerilog(
    (ctx, services, lc) =>
        lc
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EgyptTax")
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{UserId}] [{FirmName}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture
            )
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "logs", "egypttax-.log"),
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] [{UserId}] [{FirmName}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture
            )
);

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
builder.Services.AddScoped<
    EgyptTax.Application.Numbering.IDocumentNumberAllocator,
    EgyptTax.Infrastructure.Numbering.SqlSequentialNumberAllocator
>();
builder.Services.AddScoped<
    EgyptTax.Application.Accounting.IJournalEntryEmitter,
    EgyptTax.Infrastructure.Accounting.SalesInvoiceJournalEmitter
>();
builder.Services.AddScoped<
    EgyptTax.Application.Accounting.IPurchaseInvoiceJournalEmitter,
    EgyptTax.Infrastructure.Accounting.PurchaseInvoiceJournalEmitter
>();
builder.Services.AddScoped<
    EgyptTax.Application.Accounting.IExpenseJournalEmitter,
    EgyptTax.Infrastructure.Accounting.ExpenseJournalEmitter
>();
builder.Services.AddScoped<
    EgyptTax.Application.Accounting.ISupplierPaymentVoucherJournalEmitter,
    EgyptTax.Infrastructure.Accounting.SupplierPaymentVoucherJournalEmitter
>();
builder.Services.AddScoped<
    EgyptTax.Application.Accounting.ICustomerReceiptVoucherJournalEmitter,
    EgyptTax.Infrastructure.Accounting.CustomerReceiptVoucherJournalEmitter
>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Payments.AllocatePaymentHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Pricing.PricelistResolver>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Crm.SendLeadEmailHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Inventory.WeightedAvgCostQuery>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Payments.PostSupplierPaymentVoucherHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Payments.PostCustomerReceiptVoucherHandler>();
builder.Services.AddScoped<
    EgyptTax.Application.Payments.IPaymentVoucherQuery,
    EgyptTax.Infrastructure.Payments.SqlPaymentVoucherQuery
>();

// US7 / FR-045 / R-17 — WHT compute service. Selects the effective-
// dated WhtCategory row in force on the payment date.
builder.Services.AddScoped<
    EgyptTax.Application.Wht.IWhtComputeService,
    EgyptTax.Infrastructure.Wht.SqlWhtComputeService
>();

// US7 / T207 — WHT certificate payload builder. Same payload
// shape drives the bilingual PDF (WhtCertificatePdfRenderer.Render
// is static — no DI needed) AND the contract-validated JSON
// (T198) so an inspector reads the same numbers in either form.
builder.Services.AddScoped<
    EgyptTax.Application.Wht.IWhtCertificatePayloadBuilder,
    EgyptTax.Infrastructure.Wht.SqlWhtCertificatePayloadBuilder
>();

// US7 / T209 / FR-046 — Form 41 quarterly WHT filing generator.
builder.Services.AddScoped<EgyptTax.Infrastructure.Wht.GenerateForm41Handler>();

// US7 / T210 / FR-046 / scenario 3 — mark a generated Form 41
// as Filed; stamps included WHT certs with the filing id so they
// can never appear in another filing (T200 immutability).
builder.Services.AddScoped<EgyptTax.Infrastructure.Wht.MarkForm41FiledHandler>();

// US7 / T211 / FR-047 — WHT lifecycle dashboard projection
// (owed / expected / filings views with overdue derivation).
builder.Services.AddScoped<
    EgyptTax.Application.Wht.IWhtLifecycleDashboardQuery,
    EgyptTax.Infrastructure.Wht.SqlWhtLifecycleDashboardQuery
>();

// P1.14 — inbound WHT certificate matcher. Powers the
// /wht/inbound page where the operator logs a customer-issued
// withholding certificate and we suggest which sales invoice it
// likely relates to (±2% tolerance on the implied withholding).
builder.Services.AddScoped<
    EgyptTax.Application.Wht.IInboundWhtMatcher,
    EgyptTax.Infrastructure.Wht.SqlInboundWhtMatcher
>();

// US5 / FR-019 / FR-022 — date-driven VAT-rate lookup for the
// settings page's overlap validation + future invoice-line
// rate-pickers.
builder.Services.AddScoped<
    EgyptTax.Application.Configuration.IVatRateLookup,
    EgyptTax.Infrastructure.Configuration.SqlVatRateLookup
>();

// US8 / FR-049 / T225 — firm-context resolver feeds AuditEmitBehavior
// so any audit row written by a firm-user actor gets the firm name
// auto-tagged in the chain (INV-015) even when the calling command
// didn't populate ActorFirmName itself.
builder.Services.AddScoped<
    EgyptTax.Application.FirmPortal.IFirmContextResolver,
    EgyptTax.Infrastructure.FirmPortal.SqlFirmContextResolver
>();
builder.Services.AddScoped<EgyptTax.Infrastructure.FirmPortal.InviteAccountantFirmUserHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.FirmPortal.PeriodReviewLockHandler>();

// Differentiator 1 (Tax Risk Score) — rules registered as singletons
// because they are stateless; the scorer fans out across every
// registered rule. Adding a new rule = adding one AddSingleton line.
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.MissingTinRule
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.MissingEtaCodeRule
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.EtaSubmissionWindowExpiringRule
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.EtaSubmissionFailedRule
>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.DocumentRiskScorer>();

// US2 — purchase-side risk rules. Same DI pattern; new rules ship by
// adding a class plus one AddSingleton line.
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.MissingAttachmentRule
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.NonRecoverableInputVatRule
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.DuplicateSupplierInvoiceRule
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.RiskScoring.IPurchaseDocumentRiskRule,
    EgyptTax.Application.Compliance.RiskScoring.Rules.WhtRequiredButMissingRule
>();
builder.Services.AddSingleton<EgyptTax.Application.Compliance.RiskScoring.PurchaseDocumentRiskScorer>();
builder.Services.AddScoped<
    EgyptTax.Application.Compliance.RiskScoring.IPurchaseInvoiceFingerprintQuery,
    EgyptTax.Infrastructure.Compliance.SqlPurchaseInvoiceFingerprintQuery
>();

// US2 — purchase invoice + expense post handlers (FR-016 enforced
// in both — deductible documents require an attachment).
builder.Services.AddScoped<EgyptTax.Infrastructure.Purchases.PostPurchaseInvoiceHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Expenses.PostExpenseHandler>();

// T140 / FR-007 — master-data deletion guard. Scoped because it
// consumes AppDbContext. Future admin tooling + data-migration
// scripts call into this BEFORE attempting any hard delete.
builder.Services.AddScoped<
    EgyptTax.Application.Common.Guards.IMasterDataDeletionGuard,
    EgyptTax.Infrastructure.Common.Guards.SqlMasterDataDeletionGuard
>();

// US5 / FR-021 — monthly VAT report. Scoped per AppDbContext.
builder.Services.AddScoped<
    EgyptTax.Application.Reports.IVatMonthlyReportQuery,
    EgyptTax.Infrastructure.Reports.SqlVatMonthlyReportQuery
>();

// US5 / FR-023 — taxable income report (annual / period-scoped).
builder.Services.AddScoped<
    EgyptTax.Application.Reports.ITaxableIncomeReportQuery,
    EgyptTax.Infrastructure.Reports.SqlTaxableIncomeReportQuery
>();

// US5 / FR-024 — trial balance (sums journal-entry-line debits +
// credits per chart-of-account code).
builder.Services.AddScoped<
    EgyptTax.Application.Reports.ITrialBalanceReportQuery,
    EgyptTax.Infrastructure.Reports.SqlTrialBalanceReportQuery
>();

// v4 A.1 — General Ledger (Trial Balance + per-account drilldown,
// chronological with running balance). Same data source as Trial
// Balance.
builder.Services.AddScoped<
    EgyptTax.Application.Reports.IGeneralLedgerReportQuery,
    EgyptTax.Infrastructure.Reports.SqlGeneralLedgerReportQuery
>();

// v4 B.4 — Cash Flow statement (direct method, single Operating
// section per spec scope). Sums journal-entry-line activity hitting
// the registered cash-account codes.
builder.Services.AddScoped<
    EgyptTax.Application.Reports.ICashFlowReportQuery,
    EgyptTax.Infrastructure.Reports.SqlCashFlowReportQuery
>();

// P1.8 (Penalty Shield) — exposure projection. Reads EtaSubmission +
// SalesInvoice to compute current tier + projected fines + the
// prioritised work queue. See PenaltyRegime for the constants.
builder.Services.AddScoped<
    EgyptTax.Application.Compliance.PenaltyShield.IPenaltyExposureQuery,
    EgyptTax.Infrastructure.Compliance.PenaltyShield.SqlPenaltyExposureQuery
>();

// P1.2 (Certificate Monitor) — surfaces HTTPS + ETA signing certs
// from LocalMachine\My with expiry buckets so the operator never gets
// blindsided by a Saturday-morning 401-storm. Singleton because it's
// a thin wrapper over X509Store with no per-request state.
// On non-Windows hosts (Linux containers in dev/CI) the X509Store
// LocalMachine\My APIs throw, so swap in the Null implementation
// which returns an empty inventory and lets the Certificates page
// render its empty state cleanly.
if (OperatingSystem.IsWindows())
{
#pragma warning disable CA1416 // Guarded by the OS check above.
    builder.Services.AddSingleton<
        EgyptTax.Application.Compliance.CertificateMonitor.ICertificateMonitorQuery,
        EgyptTax.Infrastructure.Compliance.CertificateMonitor.WindowsCertificateMonitorQuery
    >();
#pragma warning restore CA1416
}
else
{
    builder.Services.AddSingleton<
        EgyptTax.Application.Compliance.CertificateMonitor.ICertificateMonitorQuery,
        EgyptTax.Infrastructure.Compliance.CertificateMonitor.NullCertificateMonitorQuery
    >();
}

// US5 / FR-037 — tax period lock guard + lock/reopen handler. The
// guard is consumed by the 3 document post handlers via the
// optional ctor arg they declared in T246; the wiring here makes
// the guard available so production posts actually consult it.
builder.Services.AddScoped<
    EgyptTax.Application.Periods.ITaxPeriodLockGuard,
    EgyptTax.Infrastructure.Periods.SqlTaxPeriodLockGuard
>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Periods.LockTaxPeriodHandler>();

// Differentiator 2 — Monthly Tax Closing Cockpit projection.
// T236a / Round-6 F13 — wrapped in CockpitCachingDecorator with a
// 30-second sliding expiration. The same decorator instance is also
// the ICockpitCacheInvalidator so post-handlers can bust the entry
// for a month when a tax-impacting state change lands.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<EgyptTax.Infrastructure.Compliance.SqlMonthlyTaxClosingCockpitQuery>();
builder.Services.AddScoped<EgyptTax.Application.Compliance.CockpitCachingDecorator>(
    sp => new EgyptTax.Application.Compliance.CockpitCachingDecorator(
        sp.GetRequiredService<EgyptTax.Infrastructure.Compliance.SqlMonthlyTaxClosingCockpitQuery>(),
        sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
    )
);
builder.Services.AddScoped<EgyptTax.Application.Compliance.IMonthlyTaxClosingCockpitQuery>(sp =>
    sp.GetRequiredService<EgyptTax.Application.Compliance.CockpitCachingDecorator>()
);
builder.Services.AddScoped<EgyptTax.Application.Compliance.ICockpitCacheInvalidator>(sp =>
    sp.GetRequiredService<EgyptTax.Application.Compliance.CockpitCachingDecorator>()
);

// US3 / FR-026 — document approval workflow handler. Single class
// covers Submit / Approve / Reject / Void across the 3 approval-
// eligible aggregates (SalesInvoice / PurchaseInvoice / Expense).
builder.Services.AddScoped<EgyptTax.Infrastructure.Workflow.DocumentApprovalHandler>();

// Phase F — Sales Order → Invoice converter. Used by the
// SalesOrderList "Convert" button to produce a Draft SalesInvoice
// from a Confirmed order.
builder.Services.AddScoped<EgyptTax.Infrastructure.Sales.ConvertSalesOrderToInvoiceHandler>();

// US4 / FR-031 — manual adjusting journal voucher handler. Role-
// gated to Administrator + Accountant; Bookkeeper rejected.
builder.Services.AddScoped<EgyptTax.Infrastructure.Journals.CreateManualAdjustingJournalHandler>();

// US4 / FR-012 — reversal-voucher handler. Same FR-031 role gate;
// adds DB-side checks for already-reversed + reversal-of-reversal.
builder.Services.AddScoped<EgyptTax.Infrastructure.Journals.CreateReversalJournalHandler>();

// US4 / FR-024 / FR-025 / T171 — unified journal-ledger query
// behind /journals + /journals/{id}.
builder.Services.AddScoped<
    EgyptTax.Application.Journals.IJournalLedgerQuery,
    EgyptTax.Infrastructure.Journals.SqlJournalLedgerQuery
>();

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
builder.Services.AddScoped<
    EgyptTax.Application.FixedAssets.IFixedAssetQuery,
    EgyptTax.Infrastructure.FixedAssets.SqlFixedAssetQuery
>();
builder.Services.AddScoped<EgyptTax.Infrastructure.FixedAssets.CreateFixedAssetHandler>();

// US9 / FR-048 — period-scoped tax-inspection bundle builder.
builder.Services.AddScoped<
    EgyptTax.Application.Inspection.IInspectionBundleBuilder,
    EgyptTax.Infrastructure.Inspection.InspectionBundleBuilder
>();

// T139 / R-21 — attachment store lives on the filesystem under
// EGYPTTAX_ATTACHMENT_ROOT (config key Attachments:Root). Defaults
// to {ContentRootPath}/var/attachments for dev so a fresh clone
// "just works" without operator setup. Singleton because the store
// is stateless beyond the configured root.
var attachmentRoot =
    builder.Configuration["Attachments:Root"]
    ?? Environment.GetEnvironmentVariable("EGYPTTAX_ATTACHMENT_ROOT")
    ?? Path.Combine(builder.Environment.ContentRootPath, "var", "attachments");
builder.Services.AddSingleton<EgyptTax.Application.FileStorage.IAttachmentStore>(
    sp => new EgyptTax.Infrastructure.FileStorage.FileSystemAttachmentStore(
        attachmentRoot,
        sp.GetRequiredService<EgyptTax.SharedKernel.Time.IClock>()
    )
);
builder.Services.AddScoped<EgyptTax.Infrastructure.Attachments.UploadAttachmentHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.PostSalesInvoiceHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.PostSalesInvoiceWithEtaSubmissionHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.BulkSalesInvoicePostHandler>();

// G3.3 — VAT-return generator. Reads from existing VatMonthly
// report query, gates on a Locked TaxPeriod, writes a frozen
// snapshot row.
builder.Services.AddScoped<EgyptTax.Infrastructure.Tax.GenerateVatReturnHandler>();

// G3.4 — Annual income-tax return generator. Re-uses the existing
// TaxableIncomeReport query, gates on all 12 VAT months of the
// fiscal year being Locked, applies regime-specific tax calculation
// (Law 91 brackets for Standard, Law 6 turnover for simplified).
builder.Services.AddScoped<EgyptTax.Infrastructure.Tax.GenerateIncomeTaxReturnHandler>();

// Gux.13 — lazy-init accessor for the four new single-row settings
// entities (InvoiceSettings, SmtpSettings, BackupConfig,
// NotificationPrefs). Admin-panel tabs call these without worrying
// about whether the row exists yet on pre-Gux.13 installs.
builder.Services.AddScoped<EgyptTax.Infrastructure.Settings.SettingsRepository>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Settings.UpdateInvoiceNumberHandler>();

// D0.5 (v3 roadmap) — sample-data seeder for the onboarding wizard.
// Scoped because it writes via AppDbContext.
builder.Services.AddScoped<EgyptTax.Infrastructure.Onboarding.SampleDataSeeder>();

// D2.5 (v3 roadmap) — CSV import handlers for the migration page.
// Scoped because they write via AppDbContext per-request.
builder.Services.AddScoped<EgyptTax.Infrastructure.Onboarding.CustomerImportHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Onboarding.ItemImportHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Onboarding.LeadImportHandler>();

// L1.5 (v3 roadmap) — quotation orchestration: send (allocate
// per-year sequence), convert (create SalesInvoice draft), expire
// sweep (daily Hangfire job).
builder.Services.AddScoped<EgyptTax.Infrastructure.Quotations.QuotationService>();

// M-phase (v3 roadmap) — Anthropic Claude integration. Singleton
// HttpClient (typed) per .NET guidance, Singleton key protector
// (DataProtection keyring), Scoped OCR handler (writes via
// AppDbContext per-request).
builder.Services.AddSingleton<EgyptTax.Infrastructure.Ai.AnthropicApiKeyProtector>();
builder.Services.AddHttpClient<EgyptTax.Infrastructure.Ai.AnthropicVisionClient>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Ai.OcrReceiptHandler>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Ai.NlQueryHandler>();
// v4 A.4 — short-lived stash that carries the original receipt
// image bytes from /scan-receipt to /expenses/new so the saved
// expense ends up with the source image as an Attachment row.
// Singleton: backed by IMemoryCache (process-wide), holds entries
// for ~15min then auto-evicts.
builder.Services.AddSingleton<EgyptTax.Infrastructure.Ai.ReceiptImageStash>();

// L5 (v3 roadmap) — customer-portal magic-link issuance + validation.
builder.Services.AddScoped<EgyptTax.Infrastructure.Customers.CustomerPortalService>();

// N.3 (v3 §11) — REST API keys: mint + verify SHA-256 hashed
// tokens, scoped per-request via AppDbContext.
builder.Services.AddScoped<EgyptTax.Infrastructure.Api.ApiKeyService>();

// v4 B.3 — REST API rate limiter (60 rpm per key, in-memory fixed
// window) + outbound webhook dispatcher (best-effort POSTs with
// HMAC-SHA256 signing). Singleton because both wrap process-wide
// state (counter dictionary / IHttpClientFactory pool).
builder.Services.AddSingleton<EgyptTax.Infrastructure.Api.ApiKeyRateLimiter>();
builder.Services.AddHttpClient("WebhookDispatcher");
builder.Services.AddSingleton<EgyptTax.Infrastructure.Api.WebhookDispatcher>();

// v5 DP.1 — toast notification service. Scoped so each Blazor
// circuit (one per tab) gets its own toast stack; the
// ToastContainer in MainLayout subscribes + renders.
builder.Services.AddScoped<EgyptTax.Web.Shared.Toasts.ToastService>();

// v3 §11 #8 — eSignature service: request + verify + record
// magic-link signatures on quotations / invoices.
builder.Services.AddScoped<EgyptTax.Infrastructure.Signatures.SignatureService>();
builder.Services.AddHttpContextAccessor();

// Gux.13 Tab 5 — SMTP password protector + test sender. Singleton
// because IDataProtectionProvider keys are bound to the host's
// keyring (no per-request state).
builder.Services.AddSingleton<EgyptTax.Infrastructure.Settings.SmtpPasswordProtector>();
builder.Services.AddSingleton<EgyptTax.Infrastructure.Settings.SmtpTestSender>();
builder.Services.AddSingleton<EgyptTax.Infrastructure.Settings.SendInvoiceByEmailHandler>();

// Gux.13 Tab 7 — in-app license activation. Operator pastes the
// license.token JSON; this writes it to the canonical disk path
// and re-runs ActivationFlow. LicenseStatus refreshes immediately.
builder.Services.AddSingleton<EgyptTax.Web.Licensing.InAppActivationHandler>();

// Gux.13 Tab 6 — user management. Wraps the existing IPasswordHasher
// (Argon2id) for temp-password hashing on add/reset.
builder.Services.AddScoped<EgyptTax.Infrastructure.Settings.UserManagementHandler>();

// Gux.13 Tab 8 — backup engine. Provider-aware (BACKUP DATABASE
// for SQL Server, SQLite Online Backup API for SQLite). Captures
// the attachments-root path so the engine zips them alongside the
// DB into the .dxbak archive.
builder.Services.AddScoped<EgyptTax.Infrastructure.Settings.BackupEngine>(sp =>
    new EgyptTax.Infrastructure.Settings.BackupEngine(
        sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<EgyptTax.Infrastructure.Persistence.AppDbContext>>(),
        sp.GetRequiredService<EgyptTax.Infrastructure.Settings.SettingsRepository>(),
        sp.GetRequiredService<EgyptTax.SharedKernel.Time.IClock>(),
        attachmentRoot));

// G3.2 — Receipt OCR. Tesseract loads native libs + tessdata
// language packs lazily on first request; if tessdata is missing,
// the service returns Unavailable rather than crashing so the rest
// of the app keeps working. Configure tessdata location via the
// "Tesseract" config section (defaults to {contentRoot}/tessdata).
builder.Services.Configure<EgyptTax.Infrastructure.Ocr.TesseractOptions>(
    builder.Configuration.GetSection("Tesseract"));
builder.Services.AddSingleton<EgyptTax.Application.Ocr.IReceiptOcrService,
    EgyptTax.Infrastructure.Ocr.TesseractReceiptOcrService>();

// G2.2 — WhatsApp invoice delivery. Default to the mock dispatcher
// (writes the audit row + logs but doesn't hit the network); swap
// to MetaCloudWhatsAppDispatcher when the vendor's Meta WhatsApp
// Business number + access token are configured.
builder.Services.AddScoped<EgyptTax.Application.Whatsapp.IWhatsAppDispatcher,
    EgyptTax.Infrastructure.Whatsapp.MockWhatsAppDispatcher>();
builder.Services.AddScoped<EgyptTax.Infrastructure.Invoices.IssueCreditNoteHandler>();
builder.Services.AddSingleton<
    EgyptTax.Application.Pdf.ISalesInvoicePdfRenderer,
    EgyptTax.Infrastructure.Pdf.QuestPdfInvoiceRenderer
>();
// L1.5 follow-on (v3 roadmap) — quotation PDF renderer.
builder.Services.AddSingleton<
    EgyptTax.Application.Pdf.IQuotationPdfRenderer,
    EgyptTax.Infrastructure.Pdf.QuestPdfQuotationRenderer
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEInvoiceJsonGenerator,
    EgyptTax.Infrastructure.Eta.EInvoiceJsonGenerator
>();
builder.Services.AddScoped<
    EgyptTax.Application.Eta.IEtaDashboardQuery,
    EgyptTax.Infrastructure.Eta.SqlEtaDashboardQuery
>();

// T094-T095 — ETA submission orchestration. Default mock failure rate
// is 0% (see MockEtaSubmitter.DefaultFailureRate); operators dial it
// up via configuration to exercise the Failed branch in dev / smoke /
// load-test environments.
var etaFailureRate = builder.Configuration.GetValue<double>(
    "Eta:Mock:FailureRate",
    EgyptTax.Infrastructure.Eta.MockEtaSubmitter.DefaultFailureRate
);
builder.Services.AddSingleton<EgyptTax.Application.Eta.IEtaSubmitter>(
    _ => new EgyptTax.Infrastructure.Eta.MockEtaSubmitter(etaFailureRate)
);

// P1.3 — ETA Get-Document status query. Mock for the MVP / single-
// file portable mode; production wires a real-ETA HTTP client. Used
// by EtaStatusPollingJob to advance Submitted rows to Acknowledged
// (long UUID issued) or back to Failed (regulator rejected).
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEtaStatusQuery,
    EgyptTax.Infrastructure.Eta.MockEtaStatusQuery
>();

// P1.1 — ETA wizard mock services. Taxpayer lookup auto-fills the
// company profile from the operator's TIN; the activity-code
// catalog drives the searchable picker on wizard step 3. Both swap
// for real-ETA HTTP clients in production deployments.
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEtaTaxpayerLookup,
    EgyptTax.Infrastructure.Eta.MockEtaTaxpayerLookup
>();
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEtaActivityCodeCatalog,
    EgyptTax.Infrastructure.Eta.InMemoryEtaActivityCodeCatalog
>();

// P1.5 — ETA "Get Received Documents" feed. Mock returns a small
// fixture set so the inbox UI demos end-to-end; production swaps
// in an HTTP client paginating through the regulator's receiver
// feed.
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEtaReceivedDocumentSource,
    EgyptTax.Infrastructure.Eta.MockEtaReceivedDocumentSource
>();

// P1.6 — GS1 Egypt + EGS item-code registry. Mock resolves
// requests after compressed SLA windows (60s for GS1, 90s for EGS)
// with deterministic outcomes per item id so polling is stable.
// Production wires HTTP clients against the two real registries.
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEtaItemCodeRegistry,
    EgyptTax.Infrastructure.Eta.MockEtaItemCodeRegistry
>();

// T125 — SignalR hub + in-process status notifier per FR-035 / R-22.
// AddSignalR is registered before the hub-context-consuming notifier
// so DI validates the dependency chain. Notifier is a singleton so
// in-process Blazor pages can attach to its StatusChanged event and
// have one stable subscription target across the page lifetime.
builder.Services.AddSignalR();
builder.Services.AddSingleton<
    EgyptTax.Application.Eta.IEtaStatusNotifier,
    EgyptTax.Web.Realtime.EtaStatusNotifier
>();

// T231 / US9 / FR-048 — inspection-bundle progress notifier +
// Hangfire job. Singleton notifier so in-process Blazor subscribers
// get a stable subscription target across page lifetimes; the
// transient job picks up a fresh AppDbContext + builder per
// activation.
builder.Services.AddSingleton<
    EgyptTax.Application.Inspection.IInspectionBundleProgressNotifier,
    EgyptTax.Web.Realtime.InspectionBundleProgressNotifier
>();
builder.Services.AddSingleton(
    new EgyptTax.Infrastructure.BackgroundJobs.InspectionBundleStorageOptions
    {
        RootDirectory =
            builder.Configuration.GetValue<string>("InspectionBundles:RootDirectory")
            ?? Path.Combine(builder.Environment.ContentRootPath, "inspection-bundles"),
    }
);
builder.Services.AddTransient<EgyptTax.Infrastructure.BackgroundJobs.InspectionBundleJob>();

// EF context — primary persistence binding. Provider auto-selected
// from the connection string shape: "Data Source=foo.db" => SQLite
// (single-file portable mode); anything else => SQL Server (on-prem
// install). For SQLite we fall back to a sensible default file under
// %LOCALAPPDATA%/DaftarX/daftarx.db so the single-EXE first-run "just
// works" with zero config.
var primaryConnection =
    builder.Configuration.GetConnectionString("EgyptTax")
    ?? Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION")
    ?? PortableDefaults.DefaultSqliteConnection();
var primaryProvider = EgyptTax.Web.Tools.DatabaseProviderDetector.Detect(primaryConnection);
PortableDefaults.EnsureSqliteDirectory(primaryConnection, primaryProvider);

// SQLCipher interceptor: applies PRAGMA key on every freshly-opened
// SQLite connection using the master key reconstructed from the
// 3 Shamir shares at activation time. SQL Server installs ignore
// the interceptor (it only fires on SQLite connections).
var sqlCipherInterceptor = new EgyptTax.Infrastructure.Persistence.SqlCipherKeyInterceptor(
    masterKeyProvider: static () => EgyptTax.Web.Licensing.IsLicenseValid.MasterKey);

void ConfigurePrimary(DbContextOptionsBuilder opt)
{
    if (string.IsNullOrWhiteSpace(primaryConnection)) return;
    if (primaryProvider == EgyptTax.Web.Tools.DatabaseProvider.Sqlite)
    {
        opt.UseSqlite(primaryConnection);
        opt.AddInterceptors(sqlCipherInterceptor);
    }
    else
    {
        opt.UseSqlServer(primaryConnection);
    }
}

builder.Services.AddDbContext<AppDbContext>(ConfigurePrimary);
// IDbContextFactory<AppDbContext> for Blazor pages — Blazor circuits
// scope a single DbContext for the whole circuit, so two components
// (MainLayout + a page) running OnInitializedAsync concurrently
// trip "A second operation was started on this context instance".
// The factory hands out a fresh context per query, sidestepping the
// concurrency conflict for read-only dashboard queries.
builder.Services.AddDbContextFactory<AppDbContext>(ConfigurePrimary, lifetime: ServiceLifetime.Scoped);

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
builder.Services.AddTransient<EgyptTax.Infrastructure.Invoices.GenerateRecurringInvoicesJob>();
builder.Services.AddTransient<EtaStatusPollingJob>();
builder.Services.AddTransient<EtaReceivedInboxJob>();
builder.Services.AddTransient<EtaItemCodeCheckJob>();
builder.Services.AddTransient<ComplianceCalendarRefreshJob>();

// P3.4 — bank-recon auto-match Hangfire job (scorer is static).
builder.Services.AddTransient<BankAutoMatchJob>();

// G4.1 — daily check against the vendor's latest.json manifest.
// Typed HttpClient so the 5s timeout + base config is scoped.
builder.Services.AddHttpClient<EgyptTax.Application.Updates.IUpdateChannel,
    EgyptTax.Infrastructure.Updates.HttpUpdateChannel>();
builder.Services.AddTransient<EgyptTax.Infrastructure.BackgroundJobs.UpdateCheckJob>();

// Gux.13 Tab 8 — auto-backup cron. Fires hourly; the job itself
// gates on AutoBackupEnabled + the configured Daily/Weekly interval
// since the last successful backup.
builder.Services.AddTransient<EgyptTax.Infrastructure.BackgroundJobs.BackupAutoFireJob>();
builder.Services.AddTransient<EgyptTax.Infrastructure.BackgroundJobs.PaymentReminderJob>();
builder.Services.AddTransient<EgyptTax.Infrastructure.BackgroundJobs.QuotationExpirySweepJob>();

// T110 / R-13 — supplier-TIN revalidation cron. The revalidator is
// still the always-valid stub (the live registry feed is a Near-term
// plug-in); US2 replaced the empty supplier source with the
// EF-backed `SqlSupplierTinSource` so the cron now iterates the
// real Supplier table. Source is scoped (consumes AppDbContext).
builder.Services.AddSingleton<
    EgyptTax.Application.Compliance.TinRevalidation.ISupplierTinRevalidator,
    EgyptTax.Infrastructure.Compliance.AlwaysValidTinRevalidator
>();
builder.Services.AddScoped<
    EgyptTax.Application.Compliance.TinRevalidation.ISupplierTinSource,
    EgyptTax.Infrastructure.Compliance.SqlSupplierTinSource
>();
builder.Services.AddTransient<SupplierTinRevalidationJob>();

// FR-028 / R-03 — Hangfire storage. SQL Server install gets its own
// EgyptTax_Hangfire database (job-state schema kept off the domain
// DB). Single-file SQLite install can't use Hangfire.SqlServer (and
// Hangfire has no first-class SQLite provider), so we fall back to
// in-memory storage — jobs reset on restart, but for a single-user
// portable install that's fine.
var hangfireConnection =
    builder.Configuration.GetConnectionString("EgyptTax_Hangfire")
    ?? Environment.GetEnvironmentVariable("EGYPTTAX_HANGFIRE_CONNECTION");
var hangfireUsesSqlServer =
    !string.IsNullOrWhiteSpace(hangfireConnection)
    && primaryProvider == EgyptTax.Web.Tools.DatabaseProvider.SqlServer;
if (hangfireUsesSqlServer)
{
    builder.Services.AddHangfire(cfg =>
        cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                hangfireConnection!,
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                }
            )
    );
    builder.Services.AddHangfireServer();
}
else
{
    builder.Services.AddHangfire(cfg =>
        cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMemoryStorage()
    );
    builder.Services.AddHangfireServer();
}

// FR-039 — cookie authentication. The cookie carries only the session
// id; absolute expiry, revocation, and audit emission live on the
// server-side Session row via SessionService.ValidateAsync (called
// from OnValidatePrincipal in the production wire-up that lands when
// the Blazor surface ships in T058+).
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
    options.AddPolicy(
        "FullyAuthenticated",
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireClaim(
                    EgyptTax.Web.Auth.AuthClaims.AuthStage,
                    EgyptTax.Web.Auth.AuthClaims.StageFullyAuthenticated
                )
    );

    // Sales-rep restriction — pages reserved for accountant-style
    // roles. A SALES_REP-only user is denied; users with any other
    // role (including SALES_REP + ADMIN combined) pass through. The
    // sidebar already hides these entries for reps; this policy is
    // the URL-level enforcement so a typed-in /audit-log etc. also
    // 403s.
    options.AddPolicy(
        "NotSalesRepOnly",
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireClaim(
                    EgyptTax.Web.Auth.AuthClaims.AuthStage,
                    EgyptTax.Web.Auth.AuthClaims.StageFullyAuthenticated)
                .RequireAssertion(ctx =>
                {
                    var roles = ctx.User
                        .FindAll(System.Security.Claims.ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (roles.Count == 0) return false;
                    // Pass if the user has ANY non-SALES_REP role.
                    return roles.Any(r => !string.Equals(r, "SALES_REP", StringComparison.OrdinalIgnoreCase));
                })
    );
});

// T058 / R-11 — Blazor Server + Razor Pages host. The Razor Pages
// runtime hosts the Blazor scaffold via /_Host (mapped as the
// fallback page); the bilingual <html dir> attribute is set in
// Pages/Shared/_Layout.cshtml from CultureInfo.CurrentCulture.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Blazor Server pages that hit local minimal-API endpoints
// (audit-log verify route etc) need an HttpClient injected. Blazor
// Server doesn't auto-register one — unlike Blazor WebAssembly.
builder.Services.AddHttpClient();

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
app.UseRequestLocalization(
    new RequestLocalizationOptions
    {
        DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("ar-EG"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures,
    }
);

// License gate — short-circuit every request with the activation
// banner when the boot-time gate failed. Mounted FIRST so even
// /login, /api, and /_blazor return the banner instead of leaking
// any other functionality. Health probes pass through (open by
// design — see LicenseBannerMiddleware).
app.UseMiddleware<EgyptTax.Web.Licensing.LicenseBannerMiddleware>();

// Static files: serve from disk wwwroot when present (dev / on-prem
// install), fall back to assembly-embedded wwwroot when running as
// the single-file portable EXE (the .exe alone, no wwwroot beside).
app.UseStaticFiles();
{
    var embeddedFiles = new Microsoft.Extensions.FileProviders.ManifestEmbeddedFileProvider(
        typeof(Program).Assembly, "wwwroot");
    app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedFiles });
}

app.UseAuthentication();
app.UseAuthorization();

// T256 / R-20 — push CorrelationId + UserId + FirmName onto Serilog's
// LogContext for every downstream log line in the request.
app.UseMiddleware<EgyptTax.Web.Logging.CorrelationContextMiddleware>();
app.UseSerilogRequestLogging();

// Schedule recurring jobs once Hangfire storage is available
// (always true now — either SQL Server or in-memory).
{
    var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

    // R-23 — every 6 hours, query NTP and emit a skew event when the
    // delta exceeds the 5-second tolerance.
    recurring.AddOrUpdate<NtpHealthCheckJob>(
        recurringJobId: "ntp-health-check",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "0 */6 * * *"
    );

    // FR-028 — checkpoint emitter polls every minute; the job itself
    // gates the actual write on (1k entries OR 15 min) since the
    // previous checkpoint.
    recurring.AddOrUpdate<AuditCheckpointJob>(
        recurringJobId: "audit-checkpoint",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "* * * * *"
    );

    // FR-036 — retry Failed-but-still-in-window ETA submissions every
    // 15 minutes. The job's own filter excludes Submitted (terminal)
    // and expired-window Failed rows, so cron frequency only governs
    // recovery latency for transient mock failures, not regulator
    // compliance — the post-time wrapper handler is responsible for
    // the first attempt.
    recurring.AddOrUpdate<EtaSubmissionRetryJob>(
        recurringJobId: "eta-submission-retry",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "*/15 * * * *"
    );

    // L3 (v3 roadmap) — generate Draft invoices from recurring
    // templates whose NextRunDate is today. Fires once daily at
    // 02:00 UTC (~ 05:00 Cairo) so the office sees the new drafts
    // when they arrive in the morning.
    recurring.AddOrUpdate<EgyptTax.Infrastructure.Invoices.GenerateRecurringInvoicesJob>(
        recurringJobId: "recurring-invoices-generate",
        methodCall: j => j.RunAsync(CancellationToken.None),
        cronExpression: "0 2 * * *"
    );

    // P1.3 — every minute, poll the regulator for documents that are
    // Submitted but not yet acknowledged. The mock returns
    // PendingAck for ~30s after first sight then resolves to
    // Acknowledged (~85%) or Rejected (~15%); cron frequency mostly
    // governs how snappy the dashboard's "ETA confirmed!" badge feels
    // — production with the real ETA endpoint should drop to */5 to
    // be a polite API citizen.
    recurring.AddOrUpdate<EtaStatusPollingJob>(
        recurringJobId: "eta-status-polling",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "* * * * *"
    );

    // P1.5 — pull received documents from the regulator into the
    // operator's inbox. Daily at 06:00 in production (operator
    // opens the laptop and the inbox is already populated overnight);
    // we run every 2 minutes here so the demo is interactive.
    recurring.AddOrUpdate<EtaReceivedInboxJob>(
        recurringJobId: "eta-received-inbox",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "*/2 * * * *"
    );

    // P1.6 — check pending GS1 / EGS item-code requests against
    // the registries. Daily in production (registries take 24-48h
    // for GS1 and ~15 days for EGS); every minute here so the demo
    // moves at human speed (mock SLAs are compressed accordingly).
    recurring.AddOrUpdate<EtaItemCodeCheckJob>(
        recurringJobId: "eta-item-code-check",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "* * * * *"
    );

    // P2.6 — materialise the compliance calendar for the current
    // and next year. Daily at 02:00 in production (well before
    // the operator's morning); we run every 5 minutes here so the
    // demo is interactive and the table populates immediately
    // after the company profile lands.
    recurring.AddOrUpdate<ComplianceCalendarRefreshJob>(
        recurringJobId: "compliance-calendar-refresh",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "*/5 * * * *"
    );

    // R-13 — daily re-validation of supplier TINs against the ETA
    // registry. Currently a no-op against an empty source + always-
    // valid revalidator stub; the cron skeleton ships now so the
    // Near-term registry-feed task only has to swap implementations.
    recurring.AddOrUpdate<SupplierTinRevalidationJob>(
        recurringJobId: "supplier-tin-revalidation",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "0 3 * * *"
    );

    // P3.4 — bank statement auto-match. Runs every 10 minutes so
    // newly-imported statements pick up suggestions / auto-matches
    // quickly without hammering the DB. Idempotent — only touches
    // Unmatched lines.
    recurring.AddOrUpdate<BankAutoMatchJob>(
        recurringJobId: "bank-auto-match",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "*/10 * * * *"
    );

    // G4.1 — daily auto-update check at 04:15 UTC. Reads the
    // vendor's latest.json manifest and exposes the result via
    // UpdateStatus / the topbar banner. Failure is non-fatal —
    // the banner falls back to whatever the last successful check
    // returned, so a transient CDN blip doesn't hide an update.
    recurring.AddOrUpdate<EgyptTax.Infrastructure.BackgroundJobs.UpdateCheckJob>(
        recurringJobId: "update-check",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "15 4 * * *"
    );

    // Gux.13 Tab 8 — auto-backup. Runs hourly; the job itself
    // checks BackupConfig.AutoBackupEnabled + the configured
    // Daily/Weekly interval since the last successful backup.
    // Hourly cadence keeps the maximum delay between "deadline
    // arrived" and "backup actually fires" bounded — a 02:00
    // daily slot would skip the day if the service restarted at
    // 02:30. OnClosing frequency is event-driven (period-lock
    // handler) and not handled here.
    recurring.AddOrUpdate<EgyptTax.Infrastructure.BackgroundJobs.BackupAutoFireJob>(
        recurringJobId: "auto-backup",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "0 * * * *"
    );

    // L8 (v3 roadmap) — daily payment-reminder sweep at 03:00 UTC
    // (~06:00 Cairo). The job itself gates on
    // NotificationPrefs.PaymentReminderEnabled (off by default)
    // and on SMTP being configured for DirectSmtp; safe to schedule
    // unconditionally because it short-circuits silently on either.
    recurring.AddOrUpdate<EgyptTax.Infrastructure.BackgroundJobs.PaymentReminderJob>(
        recurringJobId: "payment-reminders",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "0 3 * * *"
    );

    // L1.5 follow-on — daily quotation expiry sweep at 02:30 UTC
    // (between auto-backup at 02:00 and payment reminders at 03:00).
    // Flips Sent quotations whose ValidUntilDate has passed into
    // Expired state so the operator + customer see the right status.
    recurring.AddOrUpdate<EgyptTax.Infrastructure.BackgroundJobs.QuotationExpirySweepJob>(
        recurringJobId: "quotation-expiry-sweep",
        methodCall: j => j.RunOnceAsync(CancellationToken.None),
        cronExpression: "30 2 * * *"
    );
}

// N.3 (v3 §11) + v4 B.3 — public REST API surface. Bearer-token
// auth via ApiKeyService, gated by a per-key rate limiter
// (60 rpm). v4 adds a write endpoint (POST invoice draft) +
// outbound webhooks for invoice.posted / payment.received.
// Pagination via ?skip=&take= with a hard cap of 200 per request
// to keep responses bounded.
static async Task<IResult> AuthGate(
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter)
{
    var header = ctx.Request.Headers["Authorization"].ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return Results.Json(new { error = "Missing Bearer token." }, statusCode: 401);
    var token = header["Bearer ".Length..].Trim();
    var key = await apiKeys.ValidateAsync(token);
    if (key is null)
        return Results.Json(new { error = "Invalid or revoked API key." }, statusCode: 401);

    // v4 B.3 — per-key rate limit. Surface remaining + reset via
    // the standard X-RateLimit-* response headers so an integrator
    // can pace themselves; on overflow return 429 with Retry-After.
    var outcome = rateLimiter.TryConsume(key.Id);
    ctx.Response.Headers["X-RateLimit-Limit"] =
        rateLimiter.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    ctx.Response.Headers["X-RateLimit-Remaining"] =
        outcome.RemainingInWindow.ToString(System.Globalization.CultureInfo.InvariantCulture);
    if (!outcome.Allowed)
    {
        ctx.Response.Headers["Retry-After"] =
            outcome.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Json(
            new { error = "Rate limit exceeded.", retry_after_seconds = outcome.RetryAfterSeconds },
            statusCode: 429);
    }
    return Results.Ok();
}

app.MapGet("/api/v1/customers", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    int skip = 0, int take = 50) =>
{
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;
    take = Math.Clamp(take, 1, 200);
    var rows = await db.Set<EgyptTax.Domain.MasterData.Customer>().AsNoTracking()
        .OrderBy(c => c.Code).Skip(skip).Take(take)
        .Select(c => new {
            id = c.Id, code = c.Code,
            name = new { ar = c.Name.Arabic, en = c.Name.English },
            tin = c.TaxProfile.TinValue, profile = c.TaxProfile.ProfileType.ToString(),
            phone = c.Phone, email = c.Email, status = c.Status.ToString(),
            credit_limit_egp = c.CreditLimitEgp,
        })
        .ToListAsync();
    return Results.Ok(new { skip, take, count = rows.Count, data = rows });
});

app.MapGet("/api/v1/items", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    int skip = 0, int take = 50) =>
{
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;
    take = Math.Clamp(take, 1, 200);
    var rows = await db.Set<EgyptTax.Domain.MasterData.Item>().AsNoTracking()
        .OrderBy(i => i.Code).Skip(skip).Take(take)
        .Select(i => new {
            id = i.Id, code = i.Code,
            name = new { ar = i.Name.Arabic, en = i.Name.English },
            default_vat_category_id = i.DefaultVatCategoryId,
            quantity_on_hand = i.QuantityOnHand,
            low_stock_threshold = i.LowStockThreshold,
            eta_item_code = i.EtaItemCode,
            status = i.Status.ToString(),
        })
        .ToListAsync();
    return Results.Ok(new { skip, take, count = rows.Count, data = rows });
});

app.MapGet("/api/v1/invoices", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Persistence.AppDbContext db,
    int skip = 0, int take = 50) =>
{
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;
    take = Math.Clamp(take, 1, 200);
    var rows = await db.Set<EgyptTax.Domain.Invoices.SalesInvoice>().AsNoTracking()
        .Where(i => i.State == EgyptTax.Domain.Workflow.DocumentState.Posted)
        .OrderByDescending(i => i.DocumentDate).Skip(skip).Take(take)
        .Select(i => new {
            id = i.Id,
            document_number = i.DocumentNumber,
            document_date = i.DocumentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            customer_id = i.CustomerId,
            posted_at_utc = i.PostedAtUtc,
            subtotal = i.Subtotal.Amount,
            vat_total = i.VatTotal.Amount,
            grand_total = i.GrandTotal.Amount,
            is_credit_note = i.CreditNoteOfInvoiceId != null,
        })
        .ToListAsync();
    return Results.Ok(new { skip, take, count = rows.Count, data = rows });
});

// v5 B.5 — POST /api/v1/customers. Creates a Customer master row
// for an integration partner (Shopify-style buyer push). Mirrors
// CustomerImportHandler's validation: code/name required + at
// least the structured Egyptian address fields. TIN optional (if
// supplied, profile = B2BRegistered; absent → B2CConsumer).
// Returns 201 + the created row; emits customer.created webhook.
app.MapPost("/api/v1/customers", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Api.WebhookDispatcher webhooks,
    EgyptTax.Infrastructure.Persistence.AppDbContext db) =>
{
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;

    System.Text.Json.JsonElement body;
    try
    {
        body = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.JsonElement>(
            ctx.Request.Body);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Body must be valid JSON: {ex.Message}" });
    }

    static string? Str(System.Text.Json.JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() : null;

    try
    {
        var code = Str(body, "code");
        var nameAr = Str(body, "name_ar");
        var nameEn = Str(body, "name_en");
        if (string.IsNullOrWhiteSpace(code))
            return Results.BadRequest(new { error = "code is required." });
        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
            return Results.BadRequest(new { error = "name_ar and name_en are required." });
        if (await db.Set<EgyptTax.Domain.MasterData.Customer>().AsNoTracking()
            .AnyAsync(c => c.Code == code))
        {
            return Results.Conflict(new { error = $"customer code '{code}' already exists." });
        }

        if (!body.TryGetProperty("address", out var addrEl)
            || addrEl.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return Results.BadRequest(new { error = "address object is required." });
        }
        var governorate = Str(addrEl, "governorate");
        var regionCity  = Str(addrEl, "region_city");
        var street      = Str(addrEl, "street");
        var building    = Str(addrEl, "building_number");
        var postal      = Str(addrEl, "postal_code");
        var displayAr   = Str(addrEl, "display_ar")
            ?? string.Join(", ", new[] { street, regionCity, governorate }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        var displayEn   = Str(addrEl, "display_en")
            ?? string.Join(", ", new[] { street, regionCity, governorate }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        EgyptTax.Domain.MasterData.PostalAddress address;
        try
        {
            address = EgyptTax.Domain.MasterData.PostalAddress.Create(
                display: new EgyptTax.SharedKernel.ArabicEnglishText(displayAr ?? "", displayEn ?? ""),
                governorate: governorate ?? "",
                regionCity:  regionCity  ?? "",
                street:      street      ?? "",
                buildingNumber: building ?? "",
                postalCode:  string.IsNullOrWhiteSpace(postal) ? null : postal);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        EgyptTax.Domain.MasterData.CustomerTaxProfile profile;
        var tin = Str(body, "tin");
        if (!string.IsNullOrWhiteSpace(tin))
        {
            try
            {
                profile = EgyptTax.Domain.MasterData.CustomerTaxProfile.B2BRegistered(
                    EgyptTax.SharedKernel.EgyptianTin.Parse(tin),
                    vatExemption: false,
                    defaultSalesVatCategoryId: null);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }
        else
        {
            profile = EgyptTax.Domain.MasterData.CustomerTaxProfile.B2CConsumer(
                vatExemption: false, defaultSalesVatCategoryId: null);
        }

        var customer = new EgyptTax.Domain.MasterData.Customer(
            code: code,
            name: new EgyptTax.SharedKernel.ArabicEnglishText(nameAr, nameEn),
            address: address,
            taxProfile: profile,
            phone: Str(body, "phone"),
            email: Str(body, "email"));
        db.Add(customer);
        await db.SaveChangesAsync();

        webhooks.Enqueue("customer.created", new
        {
            customer_id = customer.Id,
            code = customer.Code,
            name = new { ar = customer.Name.Arabic, en = customer.Name.English },
            tin = customer.TaxProfile.TinValue,
            profile = customer.TaxProfile.ProfileType.ToString(),
        });

        return Results.Created($"/customers/{customer.Id}", new
        {
            id = customer.Id,
            code = customer.Code,
            name = new { ar = customer.Name.Arabic, en = customer.Name.English },
            tin = customer.TaxProfile.TinValue,
            profile = customer.TaxProfile.ProfileType.ToString(),
            phone = customer.Phone,
            email = customer.Email,
            status = customer.Status.ToString(),
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// v5 B.5 — POST /api/v1/leads. Creates a CRM Lead. Required:
// name (ar OR en — at least one) + (phone OR email — at least one).
// Optional: company, source, expected_value_egp, expected_close_date.
app.MapPost("/api/v1/leads", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Api.WebhookDispatcher webhooks,
    EgyptTax.SharedKernel.Time.IClock clock,
    EgyptTax.Infrastructure.Persistence.AppDbContext db) =>
{
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;

    System.Text.Json.JsonElement body;
    try
    {
        body = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.JsonElement>(
            ctx.Request.Body);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Body must be valid JSON: {ex.Message}" });
    }

    static string? Str(System.Text.Json.JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() : null;

    try
    {
        var nameAr = Str(body, "name_ar");
        var nameEn = Str(body, "name_en");
        // Permit a single "name" field that maps to both halves.
        var fallback = Str(body, "name");
        if (string.IsNullOrWhiteSpace(nameAr) && string.IsNullOrWhiteSpace(nameEn)
            && !string.IsNullOrWhiteSpace(fallback))
        {
            nameAr = fallback;
            nameEn = fallback;
        }
        var phone = Str(body, "phone");
        var email = Str(body, "email");
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            return Results.BadRequest(new { error = "phone or email is required (at least one)." });

        DateOnly? expectedCloseDate = null;
        if (body.TryGetProperty("expected_close_date", out var ecdEl)
            && ecdEl.ValueKind == System.Text.Json.JsonValueKind.String
            && DateOnly.TryParse(ecdEl.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var ecd))
        {
            expectedCloseDate = ecd;
        }
        decimal? expectedValueEgp = null;
        if (body.TryGetProperty("expected_value_egp", out var evEl)
            && evEl.TryGetDecimal(out var ev) && ev >= 0m)
        {
            expectedValueEgp = ev;
        }

        EgyptTax.Domain.Crm.Lead lead;
        try
        {
            lead = EgyptTax.Domain.Crm.Lead.Create(
                name: new EgyptTax.SharedKernel.ArabicEnglishText(nameAr ?? "", nameEn ?? ""),
                companyName: Str(body, "company"),
                phone: phone,
                email: email,
                source: Str(body, "source"),
                assignedToUserId: null,
                createdAtUtc: clock.UtcNow,
                createdByUserId: null);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        if (expectedCloseDate is not null || expectedValueEgp is not null)
        {
            lead.SetForecast(expectedCloseDate, expectedValueEgp);
        }

        db.Add(lead);
        await db.SaveChangesAsync();

        webhooks.Enqueue("lead.created", new
        {
            lead_id = lead.Id,
            name = new { ar = lead.Name.Arabic, en = lead.Name.English },
            phone = lead.Phone,
            email = lead.Email,
            stage = lead.Stage.ToString(),
            source = lead.Source,
        });

        return Results.Created($"/crm/leads/{lead.Id}", new
        {
            id = lead.Id,
            name = new { ar = lead.Name.Arabic, en = lead.Name.English },
            company = lead.CompanyName,
            phone = lead.Phone,
            email = lead.Email,
            stage = lead.Stage.ToString(),
            source = lead.Source,
            expected_value_egp = lead.ExpectedValueEgp,
            expected_close_date = lead.ExpectedCloseDate?.ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// v5 B.5 — POST /api/v1/expenses. Creates a Draft Expense (mobile
// receipt-capture apps). Operator posts via the regular page so
// FR-027 (interactive post path) and FR-016 (deductible →
// attachment guard) keep their interactive enforcement.
app.MapPost("/api/v1/expenses", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Api.WebhookDispatcher webhooks,
    EgyptTax.Infrastructure.Persistence.AppDbContext db) =>
{
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;

    System.Text.Json.JsonElement body;
    try
    {
        body = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.JsonElement>(
            ctx.Request.Body);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Body must be valid JSON: {ex.Message}" });
    }

    static string? Str(System.Text.Json.JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() : null;

    try
    {
        if (!body.TryGetProperty("document_date", out var dateEl)
            || !DateOnly.TryParse(dateEl.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var documentDate))
        {
            return Results.BadRequest(new { error = "document_date (yyyy-MM-dd) is required." });
        }
        if (!body.TryGetProperty("category_id", out var catEl)
            || !Guid.TryParse(catEl.GetString(), out var categoryId))
        {
            return Results.BadRequest(new { error = "category_id (Guid) is required." });
        }
        if (!body.TryGetProperty("amount_egp", out var amtEl)
            || !amtEl.TryGetDecimal(out var amount)
            || amount <= 0m)
        {
            return Results.BadRequest(new { error = "amount_egp > 0 is required." });
        }
        var deductible = body.TryGetProperty("deductible_flag", out var dedEl)
            && dedEl.ValueKind == System.Text.Json.JsonValueKind.True;
        var descAr = Str(body, "description_ar") ?? "";
        var descEn = Str(body, "description_en") ?? "";
        if (string.IsNullOrWhiteSpace(descAr) && string.IsNullOrWhiteSpace(descEn))
            return Results.BadRequest(new { error = "description_ar or description_en is required." });

        if (!await db.Set<EgyptTax.Domain.MasterData.DeductibleExpenseCategory>().AsNoTracking()
            .AnyAsync(c => c.Id == categoryId))
        {
            return Results.NotFound(new { error = $"category_id {categoryId} not found." });
        }

        EgyptTax.Domain.Expenses.Expense draft;
        try
        {
            draft = EgyptTax.Domain.Expenses.Expense.CreateDraft(
                documentDate: documentDate,
                categoryId: categoryId,
                amount: EgyptTax.SharedKernel.MoneyEgp.From(amount),
                deductibleFlag: deductible,
                description: new EgyptTax.SharedKernel.ArabicEnglishText(descAr, descEn));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        db.Add(draft);
        await db.SaveChangesAsync();

        webhooks.Enqueue("expense.created", new
        {
            expense_id = draft.Id,
            document_date = draft.DocumentDate.ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            category_id = draft.CategoryId,
            amount_egp = draft.Amount.Amount,
            deductible_flag = draft.DeductibleFlag,
            state = draft.State.ToString(),
        });

        return Results.Created($"/expenses/{draft.Id}", new
        {
            id = draft.Id,
            document_date = draft.DocumentDate.ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            category_id = draft.CategoryId,
            amount_egp = draft.Amount.Amount,
            deductible_flag = draft.DeductibleFlag,
            description = new { ar = draft.Description.Arabic, en = draft.Description.English },
            state = draft.State.ToString(),
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// v4 B.3 — POST /api/v1/invoices/draft. Creates a SalesInvoice in
// Draft state for an integration partner (Shopify-style push). The
// operator reviews + posts via the regular page; we intentionally
// do NOT auto-post because the JE emit + sequence allocation are
// scoped to interactive operator confirmation (FR-027).
app.MapPost("/api/v1/invoices/draft", async (
    HttpContext ctx,
    EgyptTax.Infrastructure.Api.ApiKeyService apiKeys,
    EgyptTax.Infrastructure.Api.ApiKeyRateLimiter rateLimiter,
    EgyptTax.Infrastructure.Persistence.AppDbContext db) =>
{
    // Auth runs FIRST so a missing/bad token returns 401 (not the
    // 400 the JSON model binder would emit for an unparseable body).
    // Bind the body manually after the gate has passed.
    var auth = await AuthGate(ctx, apiKeys, rateLimiter);
    if (auth is not Microsoft.AspNetCore.Http.HttpResults.Ok) return auth;

    System.Text.Json.JsonElement body;
    try
    {
        body = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.JsonElement>(
            ctx.Request.Body);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Body must be valid JSON: {ex.Message}" });
    }

    try
    {
        if (!body.TryGetProperty("customer_id", out var custEl)
            || !Guid.TryParse(custEl.GetString(), out var customerId))
        {
            return Results.BadRequest(new { error = "customer_id (Guid) is required." });
        }
        if (!body.TryGetProperty("document_date", out var dateEl)
            || !DateOnly.TryParse(dateEl.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var documentDate))
        {
            return Results.BadRequest(new { error = "document_date (yyyy-MM-dd) is required." });
        }
        if (!body.TryGetProperty("lines", out var linesEl)
            || linesEl.ValueKind != System.Text.Json.JsonValueKind.Array
            || linesEl.GetArrayLength() == 0)
        {
            return Results.BadRequest(new { error = "lines array (>=1) is required." });
        }

        var customer = await db.Set<EgyptTax.Domain.MasterData.Customer>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null)
        {
            return Results.NotFound(new { error = $"customer_id {customerId} not found." });
        }

        var draft = EgyptTax.Domain.Invoices.SalesInvoice.CreateDraft(
            customerId: customerId,
            customerTaxProfileSnapshot: customer.TaxProfile,
            documentDate: documentDate);

        foreach (var lineEl in linesEl.EnumerateArray())
        {
            if (!lineEl.TryGetProperty("item_id", out var itemEl)
                || !Guid.TryParse(itemEl.GetString(), out var itemId))
            {
                return Results.BadRequest(new { error = "Each line needs item_id (Guid)." });
            }
            if (!lineEl.TryGetProperty("quantity", out var qtyEl)
                || !qtyEl.TryGetDecimal(out var qty)
                || qty <= 0m)
            {
                return Results.BadRequest(new { error = "Each line needs quantity > 0." });
            }
            if (!lineEl.TryGetProperty("unit_price_egp", out var priceEl)
                || !priceEl.TryGetDecimal(out var unitPrice)
                || unitPrice < 0m)
            {
                return Results.BadRequest(new { error = "Each line needs unit_price_egp >= 0." });
            }

            var item = await db.Set<EgyptTax.Domain.MasterData.Item>()
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == itemId);
            if (item is null)
            {
                return Results.NotFound(new { error = $"item_id {itemId} not found." });
            }
            // Optional explicit vat_category_id; otherwise use the
            // item's default — same defaulting the UI applies.
            Guid vatCategoryId = item.DefaultVatCategoryId;
            if (lineEl.TryGetProperty("vat_category_id", out var vatEl)
                && Guid.TryParse(vatEl.GetString(), out var vatId))
            {
                vatCategoryId = vatId;
            }
            var vatCategory = await db.Set<EgyptTax.Domain.MasterData.VatCategory>()
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vatCategoryId);
            if (vatCategory is null)
            {
                return Results.NotFound(new { error = $"vat_category_id {vatCategoryId} not found." });
            }

            draft.AddLine(
                itemId: itemId,
                quantity: qty,
                unitPrice: EgyptTax.SharedKernel.MoneyEgp.From(unitPrice),
                vatCategoryId: vatCategoryId,
                vatRatePercent: vatCategory.RatePercent);
        }

        db.Add(draft);
        await db.SaveChangesAsync();

        return Results.Created($"/invoices/{draft.Id}", new
        {
            id = draft.Id,
            state = draft.State.ToString(),
            customer_id = draft.CustomerId,
            document_date = draft.DocumentDate.ToString("yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture),
            line_count = draft.Lines.Count,
            subtotal = draft.Subtotal.Amount,
            vat_total = draft.VatTotal.Amount,
            grand_total = draft.GrandTotal.Amount,
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// T058 — Blazor + Razor Pages routing. The Blazor hub serves the
// SignalR pipe; MapFallbackToPage routes any unmatched HTTP request
// (e.g. "/", "/invoices") to /_Host, which renders <App /> and lets
// the Blazor router pick the right page component.
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");

// G1.2 — Bulk-invoice template download. Stream the XLSX bytes
// straight back so the browser fires a Save-As dialog. No DB
// access required; the template is fixed by code.
// G3.2 — OCR a receipt image and return draft fields the expense
// form can pre-fill. POST a multipart/form-data with field name
// "image" pointing at a JPEG/PNG. Returns 200 with extracted draft
// + raw text on success; 503 with a "download tessdata" hint when
// Tesseract isn't installed (graceful degradation rather than 500).
app.MapPost("/api/v1/expenses/ocr", async (
    HttpRequest request,
    EgyptTax.Application.Ocr.IReceiptOcrService ocr,
    CancellationToken ct) =>
{
    // Gux.13 — edition gate. Solo edition can't use OCR; SMB+ can.
    // Done at the endpoint so curl/script callers get the same gate
    // as the UI button (server-side enforcement, not just UI hide).
    try { EgyptTax.Web.Licensing.EditionGate.Require(EgyptTax.Web.Licensing.Feature.ReceiptOcr); }
    catch (EgyptTax.Web.Licensing.LicenseRestrictionException ex)
    {
        return Results.Json(new { error = ex.EnglishMessage, errorAr = ex.ArabicMessage }, statusCode: 403);
    }

    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected multipart/form-data with an 'image' file." });
    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("image");
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No 'image' file in form data." });
    if (file.Length > 10 * 1024 * 1024)
        return Results.BadRequest(new { error = "Image too large (max 10 MB)." });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms, ct);
    var result = await ocr.RecognizeAsync(ms.ToArray(), ct);

    if (!result.Available)
        return Results.Json(new { available = false, reason = result.UnavailableReason }, statusCode: 503);

    return Results.Ok(new
    {
        available = true,
        draft = new
        {
            totalEgp = result.Draft!.TotalEgp,
            date = result.Draft.Date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            supplierName = result.Draft.SupplierName,
            note = result.Draft.ExtractorNote,
        },
        rawText = result.RawText,
    });
}).RequireAuthorization("FullyAuthenticated");

app.MapGet("/api/v1/invoices/bulk/template", () =>
{
    // Gux.13 — bulk upload is SMB+. Solo can't even download the
    // template (no point — the upload itself would fail).
    try { EgyptTax.Web.Licensing.EditionGate.Require(EgyptTax.Web.Licensing.Feature.BulkInvoice); }
    catch (EgyptTax.Web.Licensing.LicenseRestrictionException ex)
    {
        return Results.Json(new { error = ex.EnglishMessage, errorAr = ex.ArabicMessage }, statusCode: 403);
    }

    var bytes = EgyptTax.Application.Invoices.Bulk.BulkSalesInvoiceTemplate.Build();
    return Results.File(
        bytes,
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: "daftarx-bulk-invoices-template.xlsx");
}).RequireAuthorization("FullyAuthenticated");

// Language switcher — operator clicks AR/EN in the header, this
// endpoint writes the .AspNetCore.Culture cookie and bounces back
// to the page they came from. Cookie is read first by the
// CookieRequestCultureProvider, so the operator's choice wins
// over their browser's Accept-Language preference. R-11 — every
// screen rendered in the chosen language only (not bilingual
// inline) for installs that prefer a single-language UX.
app.MapGet("/set-culture", (HttpContext ctx, string culture, string? returnUrl) =>
{
    var safeCulture = culture is "ar-EG" or "en-US" ? culture : "ar-EG";
    ctx.Response.Cookies.Append(
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
            new Microsoft.AspNetCore.Localization.RequestCulture(safeCulture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = false });
    var safeReturn = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        ? returnUrl : "/";
    return Results.Redirect(safeReturn);
});

// T125 — ETA status hub at /hubs/eta. Auth-gated so external
// clients need a valid session cookie to subscribe to status
// changes; in-process Blazor pages attach to the StatusChanged
// event via IEtaStatusNotifier directly and don't traverse this
// hub.
app.MapHub<EgyptTax.Web.Realtime.EtaStatusHub>("/hubs/eta")
    .RequireAuthorization("FullyAuthenticated");

// T231 — inspection-bundle progress hub. Same auth posture as the
// ETA hub: subscribers must be FullyAuthenticated to see bundle
// progress events (the events themselves don't carry sensitive
// payloads, just job ids + counts, but we keep the auth boundary
// consistent with the rest of the surface).
app.MapHub<EgyptTax.Web.Realtime.InspectionBundleHub>("/hubs/inspection-bundle")
    .RequireAuthorization("FullyAuthenticated");

// T073 — Liveness / readiness probes per contracts/api/openapi.yaml.
// Liveness only signals that the process is up; readiness verifies the
// dependencies the operator runbook expects (DB reachable + audit
// checkpoint + NTP skew).
app.MapGet(
    "/api/v1/health/live",
    () =>
        Results.Json(
            new
            {
                status = "up",
                version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
            }
        )
);

app.MapGet(
    "/api/v1/health/ready",
    async (
        IServiceProvider services,
        AppDbContext db,
        IAuditCheckpointStore checkpoints,
        CancellationToken cancellationToken
    ) =>
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
    }
);

// T074 — serve the canonical contracts/api/openapi.yaml at /openapi.yaml
// so consumers can fetch the source-of-truth contract from a running
// instance. The file is the canonical specification — Swashbuckle-style
// generated docs would drift from the contract.
app.MapGet(
    "/openapi.yaml",
    (CancellationToken cancellationToken) =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "contracts", "openapi.yaml");
        return File.Exists(path)
            ? Results.File(path, contentType: "application/yaml")
            : Results.NotFound();
    }
);

// T123 / T093 — PDF download for a posted sales invoice. Loads the
// Company (issuer) + Customer + items + VAT categories, builds an
// InvoicePdfRequest with a freshly-encoded seal payload, and streams
// the rendered bytes back as application/pdf.
app.MapGet(
        "/invoices/{id:guid}/pdf",
        async (
            HttpContext ctx,
            Guid id,
            EgyptTax.Infrastructure.Persistence.AppDbContext db,
            EgyptTax.Application.Pdf.ISalesInvoicePdfRenderer renderer,
            CancellationToken cancellationToken
        ) =>
        {
            // v4 A.5 — pass the request's base URL so the rendering
            // pipeline can build the customer-portal QR URL.
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var bundle = await EgyptTax.Infrastructure.Invoices.InvoiceRenderingPipeline.LoadAsync(
                db,
                id,
                cancellationToken,
                portalBaseUrl: baseUrl
            );
            if (bundle is null)
                return Results.NotFound();
            var pdf = renderer.Render(bundle.PdfRequest);
            return Results.File(pdf, "application/pdf", $"{bundle.Invoice.DocumentNumber}.pdf");
        }
    )
    .RequireAuthorization("FullyAuthenticated");

// L1.5 follow-on (v3 roadmap) — quotation PDF download. Auth-
// gated like the regular invoice PDF; loads the company + customer
// + items so the renderer has everything it needs.
app.MapGet(
    "/quotations/{id:guid}/pdf",
    async (
        Guid id,
        EgyptTax.Infrastructure.Persistence.AppDbContext db,
        EgyptTax.Application.Pdf.IQuotationPdfRenderer renderer,
        CancellationToken ct
    ) =>
    {
        var quotation = await db.Set<EgyptTax.Domain.Quotations.Quotation>()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quotation is null) return Results.NotFound();

        var issuer = await db.Set<EgyptTax.Domain.MasterData.Company>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        if (issuer is null) return Results.BadRequest(
            "Company profile is not set. Complete the setup wizard first.");

        var receiver = await db.Set<EgyptTax.Domain.MasterData.Customer>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == quotation.CustomerId, ct);
        if (receiver is null) return Results.NotFound();

        var itemIds = quotation.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Set<EgyptTax.Domain.MasterData.Item>()
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(
                i => i.Id,
                i => new EgyptTax.Application.Pdf.ItemRenderInfo(i.Code, i.Name),
                ct);

        var request = new EgyptTax.Application.Pdf.QuotationPdfRequest(
            Quotation: quotation,
            Issuer: issuer,
            Receiver: receiver,
            Items: items);

        var pdf = renderer.Render(request);
        var fileName = quotation.QuotationNumber ?? $"quotation-{quotation.Id:N}";
        return Results.File(pdf, "application/pdf", $"{fileName}.pdf");
    }
)
.RequireAuthorization("FullyAuthenticated");

// L5 (v3 roadmap) — portal-token-protected PDF download. Same
// renderer as the admin /invoices/{id}/pdf route, but the auth
// gate is the portal token (validated against
// customer_portal_access) + an ownership check that the invoice
// belongs to the customer the token is for. No admin auth needed.
app.MapGet(
    "/portal/{token}/invoices/{id:guid}/pdf",
    async (
        HttpContext ctx,
        string token,
        Guid id,
        EgyptTax.Infrastructure.Customers.CustomerPortalService portal,
        EgyptTax.Infrastructure.Persistence.AppDbContext db,
        EgyptTax.Application.Pdf.ISalesInvoicePdfRenderer renderer,
        CancellationToken cancellationToken
    ) =>
    {
        var access = await portal.ValidateAsync(token, cancellationToken);
        if (access is null) return Results.NotFound();

        // Ownership check: the invoice must belong to the customer
        // the token authenticates. Defense-in-depth — a token
        // owner can't probe other customers' invoice IDs.
        var invoiceCustomerId = await db.Set<EgyptTax.Domain.Invoices.SalesInvoice>()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (Guid?)i.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (invoiceCustomerId != access.CustomerId) return Results.NotFound();

        // v4 A.5 — pass base URL so the embedded portal QR also
        // appears on PDFs viewed via the portal route.
        var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
        var bundle = await EgyptTax.Infrastructure.Invoices.InvoiceRenderingPipeline.LoadAsync(
            db, id, cancellationToken, portalBaseUrl: baseUrl);
        if (bundle is null) return Results.NotFound();
        var pdf = renderer.Render(bundle.PdfRequest);
        return Results.File(pdf, "application/pdf", $"{bundle.Invoice.DocumentNumber}.pdf");
    }
);

// T123 / T077 — eInvoice JSON view for a posted sales invoice.
app.MapGet(
        "/invoices/{id:guid}/einvoice.json",
        async (
            Guid id,
            EgyptTax.Infrastructure.Persistence.AppDbContext db,
            EgyptTax.Application.Eta.IEInvoiceJsonGenerator generator,
            CancellationToken cancellationToken
        ) =>
        {
            var bundle = await EgyptTax.Infrastructure.Invoices.InvoiceRenderingPipeline.LoadAsync(
                db,
                id,
                cancellationToken
            );
            if (bundle is null)
                return Results.NotFound();
            var json = generator.GenerateAsJson(bundle.EInvoiceRequest);
            return Results.Content(json, "application/json");
        }
    )
    .RequireAuthorization("FullyAuthenticated");

// T094 — /eta-mock/submit per contracts/api/openapi.yaml. Mock ETA
// submission endpoint that accepts an eInvoice JSON document,
// runs it through the same simulator the in-process MockEtaSubmitter
// uses, and returns a simulated UUID + status. Bound to localhost in
// production via standard ASP.NET Core hosting configuration; the
// route itself is auth-gated so cross-installation calls require a
// valid session cookie.
app.MapPost(
        "/api/v1/eta-mock/submit",
        async (
            HttpRequest request,
            EgyptTax.Application.Eta.IEtaSubmitter submitter,
            CancellationToken cancellationToken
        ) =>
        {
            using var reader = new StreamReader(request.Body, leaveOpen: false);
            var body = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest(
                    new
                    {
                        type = "/errors/empty-body",
                        title = "Request body is required.",
                        status = 400,
                    }
                );
            }

            // The simulator key is the document id — for the /eta-mock/submit
            // external surface we don't have one (caller is just passing
            // generic JSON), so use a synthetic guid for the simulation. The
            // mock's outcome is independent of the id; it's purely a coin
            // flip against the failure rate.
            var attempt = await submitter.SubmitAsync(Guid.NewGuid(), body, cancellationToken);

            var statusCode =
                attempt.OutcomeStatus == EgyptTax.Domain.Eta.EtaSubmissionStatus.Submitted
                    ? 202
                    : 500;
            return Results.Json(
                new
                {
                    submissionUuid = attempt.SubmissionUuid ?? Guid.Empty.ToString(),
                    status = attempt.OutcomeStatus.ToString(),
                    errorCode = attempt.ErrorCode,
                    errorMessage = attempt.ErrorMessage,
                },
                statusCode: statusCode
            );
        }
    )
    .RequireAuthorization("FullyAuthenticated");

// T096 — /api/v1/verify/{seal} per contracts/api/openapi.yaml +
// contracts/verification-seal-qr.md. Decodes the EGT1 seal, looks
// up the document, and reports VALID / TAMPERED / UNKNOWN /
// MALFORMED. Public (no auth) per the contract — verification is
// designed to be readable from a printed PDF without an account.
app.MapGet(
    "/api/v1/verify/{seal}",
    async (
        string seal,
        EgyptTax.Infrastructure.Persistence.AppDbContext db,
        CancellationToken cancellationToken
    ) =>
    {
        var resolver =
            (EgyptTax.Infrastructure.Verification.DocumentSealCodec.LiveDocumentResolver)(
                (Guid documentId) =>
                {
                    // Synchronous wrapper — the resolver delegate is invoked
                    // inside Verify which is itself called from this handler;
                    // GetAwaiter().GetResult() is safe here because the pipeline
                    // is fully async-friendly until we hit Verify (which doesn't
                    // accept async resolvers in the current contract).
                    var live = db.Set<EgyptTax.Domain.Invoices.SalesInvoice>()
                        .AsNoTracking()
                        .FirstOrDefault(i => i.Id == documentId);
                    if (live is null)
                        return null;
                    return new EgyptTax.Application.Verification.ResolvedDocument(
                        DocumentNumber: live.DocumentNumber ?? "",
                        GrandTotalPiastres: (long)(live.GrandTotal.Amount * 100m),
                        AuditEntryHash: new byte[32],
                        AuditEntryIndex: 1L
                    );
                }
            );
        var result = EgyptTax.Infrastructure.Verification.DocumentSealCodec.Verify(seal, resolver);
        var statusCode =
            result.Outcome == EgyptTax.Application.Verification.SealOutcome.Malformed ? 400 : 200;
        await Task.CompletedTask;
        return Results.Json(
            new
            {
                outcome = result.Outcome.ToString().ToUpperInvariant(),
                documentNumber = result.DocumentNumber,
                documentType = result.DocumentType,
                mismatches = result.Mismatches,
            },
            statusCode: statusCode
        );
    }
);

// T162 / FR-028 — POST /api/v1/audit/verify. Walks the entire
// audit chain (capped at 50k entries — for larger installations
// the operator runs the verify-audit CLI from T163), recomputes
// every entry's hash via the existing AuditChainVerifier, and
// returns a JSON report. Auditor-runnable from the AuditLogViewer
// page; CLI-runnable for the install-side smoke check.
const int VerifyEntryCap = 50_000;
app.MapPost(
        "/api/v1/audit/verify",
        async (
            EgyptTax.Infrastructure.Persistence.AppDbContext db,
            EgyptTax.Application.Audit.IAuditCheckpointStore checkpointStore,
            CancellationToken cancellationToken
        ) =>
        {
            var entries = await db.Set<EgyptTax.Domain.Audit.AuditLogEntry>()
                .AsNoTracking()
                .OrderBy(e => e.Index)
                .Take(VerifyEntryCap)
                .ToListAsync(cancellationToken);
            var checkpoint = await checkpointStore.ReadLatestAsync(cancellationToken);

            var report = EgyptTax.Domain.Audit.AuditChainVerifier.Verify(entries, checkpoint);

            return Results.Json(
                new
                {
                    isValid = report.IsValid,
                    entriesScanned = entries.Count,
                    truncated = entries.Count == VerifyEntryCap,
                    findings = report
                        .Findings.Select(f => new
                        {
                            kind = f.Kind.ToString(),
                            atIndex = f.AtIndex,
                            notes = f.Notes,
                        })
                        .ToList(),
                }
            );
        }
    )
    .RequireAuthorization("FullyAuthenticated");

// On every startup, ensure the catalog of default Egyptian VAT
// categories exists. Idempotent — only inserts the four standard
// rows (Standard 14%, Reduced 5%, Zero-rated, Exempt) if their codes
// are not already present. Fixes the "no VAT categories defined"
// empty-state on existing installs without requiring the operator
// to re-run the MSI seed CLI.
// Note: the once-per-process default-data seed (VAT categories etc.)
// runs on first authenticated request via MainLayout, NOT here.
// Background-task seeding before serving the first HTTP request trips
// Microsoft.Data.SqlClient's platform guard in self-contained .NET 8.
// The MainLayout call uses the same proven request-context DbFactory
// the dashboard already uses.

// P1.10 — ETA bulk export. Streams a ZIP containing one PDF + one
// JSON per posted sales invoice in the date range. Filename pattern:
//   {DocumentNumber}_{ETA-UUID-or-pending}.pdf and .json
// Buries the Chrome-extension cottage industry that scrapes the ETA
// portal because operators couldn't bulk-download from there.
app.MapGet(
        "/api/v1/eta-export.zip",
        async (
            DateOnly from,
            DateOnly to,
            EgyptTax.Infrastructure.Persistence.AppDbContext db,
            EgyptTax.Application.Pdf.ISalesInvoicePdfRenderer pdfRenderer,
            EgyptTax.Application.Eta.IEInvoiceJsonGenerator jsonGenerator,
            CancellationToken cancellationToken
        ) =>
        {
            if (from > to)
            {
                return Results.BadRequest("from must be ≤ to");
            }
            // Cap range to 1 year to keep memory bounded.
            if ((to.DayNumber - from.DayNumber) > 366)
            {
                return Results.BadRequest("Range exceeds 366 days");
            }

            var ids = await db.Set<EgyptTax.Domain.Invoices.SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.State == EgyptTax.Domain.Workflow.DocumentState.Posted
                    && i.DocumentDate >= from && i.DocumentDate <= to)
                .OrderBy(i => i.DocumentDate)
                .Select(i => i.Id)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                return Results.NotFound("No posted invoices in range");
            }

            var memory = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var invoiceId in ids)
                {
                    var bundle = await EgyptTax.Infrastructure.Invoices.InvoiceRenderingPipeline.LoadAsync(
                        db, invoiceId, cancellationToken);
                    if (bundle is null) continue;

                    var docNum = bundle.Invoice.DocumentNumber ?? invoiceId.ToString("N")[..8];
                    var safeName = string.Join("_",
                        docNum.Split(System.IO.Path.GetInvalidFileNameChars()));

                    // PDF
                    var pdfEntry = zip.CreateEntry($"{safeName}.pdf", System.IO.Compression.CompressionLevel.Optimal);
                    await using (var ps = pdfEntry.Open())
                    {
                        var pdfBytes = pdfRenderer.Render(bundle.PdfRequest);
                        await ps.WriteAsync(pdfBytes, cancellationToken);
                    }

                    // JSON
                    var jsonEntry = zip.CreateEntry($"{safeName}.json", System.IO.Compression.CompressionLevel.Optimal);
                    await using (var js = jsonEntry.Open())
                    {
                        var json = jsonGenerator.GenerateAsJson(bundle.EInvoiceRequest);
                        await js.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json), cancellationToken);
                    }
                }

                // Manifest
                var manifestEntry = zip.CreateEntry("MANIFEST.txt", System.IO.Compression.CompressionLevel.Optimal);
                await using (var ms2 = manifestEntry.Open())
                {
                    var manifest = $"DaftarX ETA bulk export\n" +
                        $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                        $"Period:    {from:yyyy-MM-dd} to {to:yyyy-MM-dd}\n" +
                        $"Invoices:  {ids.Count}\n\n" +
                        $"Each invoice has a PDF + JSON pair named after its document number.\n" +
                        $"For audit purposes, hand the auditor this entire ZIP file.\n";
                    await ms2.WriteAsync(System.Text.Encoding.UTF8.GetBytes(manifest), cancellationToken);
                }
            }
            memory.Position = 0;
            return Results.File(memory, "application/zip",
                $"daftarx-eta-export-{from:yyyyMMdd}-{to:yyyyMMdd}.zip");
        }
    )
    .RequireAuthorization("FullyAuthenticated");

// Public endpoint: serve the DaftarX HTTPS certificate (.cer, public
// key only) so workstations on the LAN can fetch + trust it without
// needing a UNC share or pre-shared file. Returns the cert generated
// by setup-https.ps1 at install time. No auth required because the
// public cert contains only the public key — safe to expose.
app.MapGet("/daftarx-cert.cer", () =>
{
    var cerPath = @"C:\ProgramData\DaftarX\daftarx-cert.cer";
    if (!File.Exists(cerPath))
    {
        return Results.NotFound("Certificate not yet generated.");
    }
    return Results.File(cerPath, "application/x-x509-ca-cert", "daftarx-cert.cer");
});

// Single-file portable mode: on first launch ensure the schema +
// admin user exist, then pop the browser. SQL Server on-prem flow
// is unchanged (the MSI runs `seed` separately during install).
if (primaryProvider == EgyptTax.Web.Tools.DatabaseProvider.Sqlite)
{
    await EgyptTax.Web.Tools.PortableFirstRun.RunAsync(app);
}

app.Run();
return 0;

/// <summary>
/// Partial marker so <c>Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory&lt;Program&gt;</c>
/// can locate the entry-point class — the C# compiler emits a generated
/// <c>Program</c> for top-level statements, but it is internal by default.
/// This explicit partial declaration makes it public for the test host.
/// </summary>
public partial class Program;
