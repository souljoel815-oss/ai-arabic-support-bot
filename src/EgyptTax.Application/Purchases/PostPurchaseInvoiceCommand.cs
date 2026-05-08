namespace EgyptTax.Application.Purchases;

/// <summary>
/// US2 / FR-026 — request to transition a draft PurchaseInvoice
/// into Posted state. Mirrors the SalesInvoice command shape so the
/// MediatR pipeline behaviours apply uniformly.
/// </summary>
public sealed record PostPurchaseInvoiceCommand(Guid PurchaseInvoiceId, Guid PostedByUserId);
