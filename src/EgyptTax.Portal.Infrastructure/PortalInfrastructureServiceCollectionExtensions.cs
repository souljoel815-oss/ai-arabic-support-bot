using EgyptTax.Portal.Application.Audit;
using EgyptTax.Portal.Application.Email;
using EgyptTax.Portal.Application.Licences;
using EgyptTax.Portal.Application.Organisations;
using EgyptTax.Portal.Infrastructure.Audit;
using EgyptTax.Portal.Infrastructure.Email;
using EgyptTax.Portal.Infrastructure.Identity;
using EgyptTax.Portal.Infrastructure.Licences;
using EgyptTax.Portal.Infrastructure.Middleware;
using EgyptTax.Portal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace EgyptTax.Portal.Infrastructure;

/// <summary>
/// T017 + T020 + T027 + T028 + T029 + T031. Single entry point so the Web
/// layer doesn't need to know about Infrastructure's internal class
/// names. <c>Program.cs</c> calls <c>builder.Services.AddPortalInfrastructure(...)</c>
/// + <c>builder.Services.AddPortalDbContext(...)</c> and the rest of the
/// wiring is opaque from outside.
/// </summary>
public static class PortalInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPortalDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PortalDbContext>(opts =>
        {
            var conn = configuration.GetConnectionString("PortalDb")
                ?? throw new InvalidOperationException("ConnectionStrings:PortalDb is not configured.");
            var provider = configuration["PortalDbProvider"] ?? "SqlServer";
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                opts.UseSqlite(conn);
            }
            else
            {
                opts.UseSqlServer(conn);
            }
        });

        return services;
    }

    public static IServiceCollection AddPortalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // T020 — TOTP MFA helper.
        services.AddSingleton<ITotpMfaService, TotpMfaService>();

        // T027 — audit log writer (scoped, talks to DbContext).
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        // T029 — per-request organisation context.
        services.AddScoped<IOrganisationContext, OrganisationContext>();

        // T028 — Ed25519 licence-signing service. Implements both
        // ILicenceSigningService + ILicenceSignatureVerifier so the
        // contract tests can round-trip without standing up the on-prem
        // verifier.
        services.Configure<LicenceSigningOptions>(configuration.GetSection("LicenceSigning"));
        services.AddSingleton<Ed25519LicenceSigningService>();
        services.AddSingleton<ILicenceSigningService>(sp => sp.GetRequiredService<Ed25519LicenceSigningService>());
        services.AddSingleton<ILicenceSignatureVerifier>(sp => sp.GetRequiredService<Ed25519LicenceSigningService>());

        // T031 — email service. Production is Resend with Polly retries;
        // dev is the stdout sink so docker-compose works without API keys.
        services.Configure<ResendOptions>(configuration.GetSection("Resend"));
        var resendMode = configuration["Resend:Mode"] ?? "Resend";
        if (string.Equals(resendMode, "Stdout", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailService, DevStdoutEmailService>();
        }
        else
        {
            services
                .AddHttpClient<IEmailService, ResendTransactionalEmailService>((sp, http) =>
                {
                    var opts = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
                    http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                })
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(r => (int)r.StatusCode == 429)
                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
        }

        return services;
    }
}
