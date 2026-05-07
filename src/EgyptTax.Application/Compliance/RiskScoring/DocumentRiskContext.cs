using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;

namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// Bundle of inputs each <see cref="IDocumentRiskRule"/> evaluates.
/// Built fresh per scoring call so rules are stateless. Captures
/// the just-in-time view of the world: the document, the items
/// referenced by its lines, the open ETA submission row (if any),
/// and the wall clock — rules that compare deadlines need a
/// nowUtc that is consistent across all rules in one pass.
/// </summary>
public sealed record DocumentRiskContext(
    SalesInvoice Invoice,
    IReadOnlyDictionary<Guid, Item> Items,
    EtaSubmission? EtaSubmission,
    DateTime NowUtc);
