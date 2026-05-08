namespace EgyptTax.Application.Expenses;

/// <summary>
/// US2 / FR-026 — request to transition a draft Expense into Posted
/// state. Mirrors the PurchaseInvoice command shape so the MediatR
/// pipeline behaviours apply uniformly.
/// </summary>
public sealed record PostExpenseCommand(Guid ExpenseId, Guid PostedByUserId);
