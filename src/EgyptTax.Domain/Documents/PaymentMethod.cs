namespace EgyptTax.Domain.Documents;

/// <summary>
/// Phase 9 / Round 5 — payment-method enum on supplier payment +
/// customer receipt vouchers. MVP scope: Cash + BankTransfer
/// (the two an Egyptian SME reaches for first); cheque + card
/// land in a Near-term batch per the Round-5 carve-out.
/// </summary>
public enum PaymentMethod
{
    Cash,
    BankTransfer,
}
