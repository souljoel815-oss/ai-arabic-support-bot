namespace EgyptTax.Application.Invoices;

/// <summary>
/// US1 — request to transition a sales-invoice draft to Posted.
/// Validation, document-number allocation, audit emission, and the
/// FR-026 transition all live in <c>PostSalesInvoiceHandler</c>.
/// </summary>
public sealed record PostSalesInvoiceCommand(Guid SalesInvoiceId, Guid PostedByUserId);
