using EgyptTax.Application.Eta;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// P1.5 — in-process mock of ETA's "Get Received Documents" feed.
/// Returns a stable fixture set across the most common Egyptian
/// SMB supplier scenarios (utility bill, raw-material supplier,
/// SaaS subscription, freelancer service) so the inbox UI demos
/// the variety of incoming docs an operator handles weekly.
///
/// The mock returns the same envelopes on every call. The pulling
/// job dedupes by RegulatorLongUuid, so a second tick is a no-op
/// and the inbox shows each document exactly once.
/// </summary>
public sealed class MockEtaReceivedDocumentSource : IEtaReceivedDocumentSource
{
    private static readonly EtaReceivedDocumentEnvelope[] Fixtures =
    {
        new(
            RegulatorLongUuid: "ETA-FIXTURE-INV-2026-04-001",
            SupplierTin: "200300400",
            SupplierLegalName: "الشركة المصرية للكهرباء",
            DocumentNumber: "EGY-EL-2026-040123",
            DocumentDate: new DateOnly(2026, 4, 28),
            NetBeforeVatEgp: 4_385.96m,
            VatTotalEgp: 614.04m,
            GrandTotalEgp: 5_000.00m),
        new(
            RegulatorLongUuid: "ETA-FIXTURE-INV-2026-05-002",
            SupplierTin: "555000111",
            SupplierLegalName: "مصنع النيل للأقمشة",
            DocumentNumber: "NIL-2026-00789",
            DocumentDate: new DateOnly(2026, 5, 3),
            NetBeforeVatEgp: 18_421.05m,
            VatTotalEgp: 2_578.95m,
            GrandTotalEgp: 21_000.00m),
        new(
            RegulatorLongUuid: "ETA-FIXTURE-INV-2026-05-003",
            SupplierTin: "987654321",
            SupplierLegalName: "أحمد محمد للاستشارات",
            DocumentNumber: "AMC-2026-014",
            DocumentDate: new DateOnly(2026, 5, 6),
            NetBeforeVatEgp: 7_017.54m,
            VatTotalEgp: 982.46m,
            GrandTotalEgp: 8_000.00m),
        new(
            RegulatorLongUuid: "ETA-FIXTURE-INV-2026-05-004",
            SupplierTin: "100200300",
            SupplierLegalName: "وي للاتصالات المتكاملة",
            DocumentNumber: "WE-B2B-9920381",
            DocumentDate: new DateOnly(2026, 5, 1),
            NetBeforeVatEgp: 1_315.79m,
            VatTotalEgp: 184.21m,
            GrandTotalEgp: 1_500.00m),
        new(
            RegulatorLongUuid: "ETA-FIXTURE-INV-2026-05-005",
            SupplierTin: "111222333",
            SupplierLegalName: "Cloudways Egypt LLC",
            DocumentNumber: "CW-2026-INV-44012",
            DocumentDate: new DateOnly(2026, 5, 8),
            NetBeforeVatEgp: 526.32m,
            VatTotalEgp: 73.68m,
            GrandTotalEgp: 600.00m),
    };

    public Task<IReadOnlyList<EtaReceivedDocumentEnvelope>> FetchSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        // Filter by document date >= sinceUtc - 30 days as a stand-in
        // for the regulator's server-side filter; dev demos with a
        // generous lookback so all five fixtures land on first poll.
        var cutoff = DateOnly.FromDateTime(sinceUtc.AddDays(-30));
        var matched = Fixtures
            .Where(f => f.DocumentDate >= cutoff)
            .ToList();
        return Task.FromResult<IReadOnlyList<EtaReceivedDocumentEnvelope>>(matched);
    }
}
