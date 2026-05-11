using EgyptTax.Domain.Invoices;

namespace EgyptTax.Application.Compliance.PreFlight;

/// <summary>
/// P1.7 — Pre-flight ETA validator. Replicates a curated subset of
/// the 8 ETA validators locally so the operator sees rejections BEFORE
/// the document is submitted (and BEFORE the Penalty Shield clock
/// starts ticking).
///
/// First cut covers the rule families that account for ~80% of real
/// ETA rejections per the research:
///   - MATH:  per-line totals + invoice totals to the cent
///   - CODE:  TIN required + format check for B2B
///   - ITEM:  item presence per line
///   - GEN:   document structure (lines exist, date sane)
///   - VAT:   recognised VAT rate
///
/// Out of scope for v1 (deferred):
///   - Schema validation (XML/JSON) — done at submission anyway
///   - Live taxpayer-status lookup — needs ETA Code Validator API
///   - EGS code "active" check — needs ETA Items API
///   - Signature validation — covered by certificate monitor
///
/// All findings carry a stable code so we can correlate with ETA
/// rejection logs over time and tune the catalog.
/// </summary>
public static class PreFlightValidator
{
    /// <summary>Tolerance for money equality (0.01 EGP = 1 piastre).</summary>
    private const decimal MoneyEpsilon = 0.01m;

    public static IReadOnlyList<ValidationFinding> Validate(SalesInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var findings = new List<ValidationFinding>();

        ValidateStructure(invoice, findings);
        ValidateMath(invoice, findings);
        ValidateCode(invoice, findings);
        ValidateItems(invoice, findings);
        ValidateVat(invoice, findings);
        ValidateDocumentDate(invoice, findings);

        return findings;
    }

    // ---------- GEN: structure ----------
    private static void ValidateStructure(SalesInvoice inv, List<ValidationFinding> f)
    {
        if (inv.Lines.Count == 0)
        {
            f.Add(new ValidationFinding(
                "GEN-001", FindingSeverity.Error,
                "الفاتورة بدون أي بنود",
                "Invoice has no lines",
                "أضف بنداً واحداً على الأقل قبل المتابعة. ETA يرفض الفواتير الفارغة.",
                "Add at least one line before continuing. ETA rejects empty invoices.",
                FieldPath: "lines"));
        }
    }

    // ---------- MATH: per-line + invoice totals ----------
    private static void ValidateMath(SalesInvoice inv, List<ValidationFinding> f)
    {
        for (var i = 0; i < inv.Lines.Count; i++)
        {
            var line = inv.Lines.ElementAt(i);
            var lineNum = i + 1;

            // Quantity must be positive
            if (line.Quantity <= 0m)
            {
                f.Add(new ValidationFinding(
                    "MATH-005", FindingSeverity.Error,
                    $"الكمية في البند رقم {lineNum} يجب أن تكون أكبر من صفر",
                    $"Quantity in line {lineNum} must be greater than zero",
                    "صحّح كمية البند أو احذفه إذا كان غير مطلوب.",
                    "Fix the quantity or remove the line if unwanted.",
                    FieldPath: $"lines[{i}].quantity"));
            }

            // Unit price must be non-negative
            if (line.UnitPrice.Amount < 0m)
            {
                f.Add(new ValidationFinding(
                    "MATH-006", FindingSeverity.Error,
                    $"سعر الوحدة في البند رقم {lineNum} لا يمكن أن يكون سالباً",
                    $"Unit price in line {lineNum} cannot be negative",
                    "صحّح السعر — للخصم استخدم خصم البند أو خصم الفاتورة.",
                    "Fix the price — use line/invoice discount for reductions.",
                    FieldPath: $"lines[{i}].unitPrice"));
            }

            // Line subtotal = qty * unit price
            var expectedSubtotal = Math.Round(line.Quantity * line.UnitPrice.Amount, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(expectedSubtotal - line.LineSubtotal.Amount) > MoneyEpsilon)
            {
                f.Add(new ValidationFinding(
                    "MATH-001", FindingSeverity.Error,
                    $"إجمالي البند رقم {lineNum} لا يطابق (الكمية × سعر الوحدة)",
                    $"Line {lineNum} subtotal doesn't match (qty × unit price)",
                    $"المتوقع {expectedSubtotal:F2} ج.م. والمسجّل {line.LineSubtotal.Amount:F2} ج.م. — أعد حفظ الفاتورة لإعادة الحساب.",
                    $"Expected {expectedSubtotal:F2} EGP, got {line.LineSubtotal.Amount:F2} EGP — re-save the invoice to recompute.",
                    FieldPath: $"lines[{i}].lineSubtotal"));
            }

            // Line VAT = LineNetSubtotal * VatRate / 100
            var expectedVat = Math.Round(line.LineNetSubtotal.Amount * line.VatRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(expectedVat - line.LineVat.Amount) > MoneyEpsilon)
            {
                f.Add(new ValidationFinding(
                    "MATH-002", FindingSeverity.Error,
                    $"ضريبة البند رقم {lineNum} لا تطابق (الصافي × النسبة)",
                    $"Line {lineNum} VAT doesn't match (net × rate)",
                    $"المتوقع {expectedVat:F2} ج.م. والمسجّل {line.LineVat.Amount:F2} ج.م.",
                    $"Expected {expectedVat:F2} EGP, got {line.LineVat.Amount:F2} EGP.",
                    FieldPath: $"lines[{i}].lineVat"));
            }
        }

        if (inv.Lines.Count == 0) return;

        // Sum of line subtotals = invoice subtotal
        var sumLineSubtotal = inv.Lines.Sum(l => l.LineSubtotal.Amount);
        if (Math.Abs(sumLineSubtotal - inv.Subtotal.Amount) > MoneyEpsilon)
        {
            f.Add(new ValidationFinding(
                "MATH-003", FindingSeverity.Error,
                "إجمالي الفاتورة لا يساوي مجموع البنود",
                "Invoice subtotal doesn't equal sum of lines",
                $"المتوقع {sumLineSubtotal:F2} ج.م. والمسجّل {inv.Subtotal.Amount:F2} ج.م.",
                $"Expected {sumLineSubtotal:F2} EGP, got {inv.Subtotal.Amount:F2} EGP.",
                FieldPath: "subtotal"));
        }

        // Sum of line VAT = invoice VAT total
        var sumLineVat = inv.Lines.Sum(l => l.LineVat.Amount);
        if (Math.Abs(sumLineVat - inv.VatTotal.Amount) > MoneyEpsilon)
        {
            f.Add(new ValidationFinding(
                "MATH-004", FindingSeverity.Error,
                "إجمالي الضريبة لا يساوي مجموع ضرائب البنود",
                "Invoice VAT total doesn't equal sum of line VATs",
                $"المتوقع {sumLineVat:F2} ج.م. والمسجّل {inv.VatTotal.Amount:F2} ج.م.",
                $"Expected {sumLineVat:F2} EGP, got {inv.VatTotal.Amount:F2} EGP.",
                FieldPath: "vatTotal"));
        }

        // GrandTotal = NetBeforeVat + VatTotal
        var expectedGrand = Math.Round(inv.NetBeforeVat.Amount + inv.VatTotal.Amount, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(expectedGrand - inv.GrandTotal.Amount) > MoneyEpsilon)
        {
            f.Add(new ValidationFinding(
                "MATH-007", FindingSeverity.Error,
                "الإجمالي الكلي لا يساوي (الصافي قبل الضريبة + الضريبة)",
                "Grand total doesn't equal (net + VAT)",
                $"المتوقع {expectedGrand:F2} ج.م. والمسجّل {inv.GrandTotal.Amount:F2} ج.م.",
                $"Expected {expectedGrand:F2} EGP, got {inv.GrandTotal.Amount:F2} EGP.",
                FieldPath: "grandTotal"));
        }
    }

    // ---------- CODE: TIN format + B2B requirements ----------
    private static void ValidateCode(SalesInvoice inv, List<ValidationFinding> f)
    {
        var snapshot = inv.CustomerTaxProfileSnapshot;
        var isB2B = snapshot.ProfileType.ToString().Equals("RegisteredTaxpayer", StringComparison.OrdinalIgnoreCase);

        if (isB2B)
        {
            if (string.IsNullOrWhiteSpace(snapshot.TinValue))
            {
                f.Add(new ValidationFinding(
                    "CODE-001", FindingSeverity.Error,
                    "الرقم الضريبي للعميل مطلوب (B2B)",
                    "Customer TIN required (B2B)",
                    "افتح ملف العميل وأدخل الرقم الضريبي قبل إعادة ترحيل الفاتورة.",
                    "Open the customer profile and add the TIN before re-posting.",
                    FieldPath: "customer.tin"));
            }
            else if (!IsValidEgyptianTin(snapshot.TinValue))
            {
                f.Add(new ValidationFinding(
                    "CODE-002", FindingSeverity.Warning,
                    $"شكل الرقم الضريبي للعميل غير معتاد ({snapshot.TinValue})",
                    $"Customer TIN format is unusual ({snapshot.TinValue})",
                    "الرقم الضريبي المصري المعتاد 9 أرقام. تأكد من الرقم لتفادي رفض ETA.",
                    "Egyptian TIN is typically 9 digits. Verify to avoid an ETA reject.",
                    FieldPath: "customer.tin"));
            }
        }
    }

    private static bool IsValidEgyptianTin(string tin)
    {
        var digits = new string(tin.Where(char.IsDigit).ToArray());
        return digits.Length is 9 or 14;
    }

    // ---------- ITEM: item presence + duplicates ----------
    private static void ValidateItems(SalesInvoice inv, List<ValidationFinding> f)
    {
        for (var i = 0; i < inv.Lines.Count; i++)
        {
            var line = inv.Lines.ElementAt(i);
            if (line.ItemId == Guid.Empty)
            {
                f.Add(new ValidationFinding(
                    "ITEM-001", FindingSeverity.Error,
                    $"البند رقم {i + 1} غير مرتبط بصنف",
                    $"Line {i + 1} has no item",
                    "اختر صنفاً من القائمة لكل بند.",
                    "Pick an item from the catalog for each line.",
                    FieldPath: $"lines[{i}].itemId"));
            }
        }

        // Duplicate items in same invoice (warning only)
        var dupes = inv.Lines.Where(l => l.ItemId != Guid.Empty)
            .GroupBy(l => l.ItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (dupes.Count > 0)
        {
            f.Add(new ValidationFinding(
                "ITEM-002", FindingSeverity.Warning,
                $"الفاتورة فيها {dupes.Count} صنف مكرر",
                $"Invoice has {dupes.Count} duplicate item(s)",
                "غالباً تقصد بند واحد بكمية مجمّعة بدل بنود متعددة لنفس الصنف.",
                "Consider consolidating duplicate items into a single line with combined quantity.",
                FieldPath: "lines"));
        }
    }

    // ---------- VAT: rate sanity ----------
    private static readonly decimal[] AllowedVatRates = { 0m, 5m, 14m };

    private static void ValidateVat(SalesInvoice inv, List<ValidationFinding> f)
    {
        for (var i = 0; i < inv.Lines.Count; i++)
        {
            var line = inv.Lines.ElementAt(i);
            if (line.VatCategoryId == Guid.Empty)
            {
                f.Add(new ValidationFinding(
                    "VAT-001", FindingSeverity.Error,
                    $"البند رقم {i + 1} بدون فئة ضريبة",
                    $"Line {i + 1} has no VAT category",
                    "اختر فئة الضريبة من القائمة (الأربع الافتراضية: STANDARD-14 / REDUCED-5 / ZERO-RATED / EXEMPT).",
                    "Pick a VAT category (defaults: STANDARD-14 / REDUCED-5 / ZERO-RATED / EXEMPT).",
                    FieldPath: $"lines[{i}].vatCategoryId"));
            }

            if (!AllowedVatRates.Contains(line.VatRatePercent))
            {
                f.Add(new ValidationFinding(
                    "VAT-002", FindingSeverity.Warning,
                    $"نسبة الضريبة في البند رقم {i + 1} غير معتادة ({line.VatRatePercent}%)",
                    $"Line {i + 1} VAT rate is unusual ({line.VatRatePercent}%)",
                    "النسب المعتمدة في مصر: 0% (صفرية/معفاة) و 5% و 14%. تأكد من فئة الضريبة.",
                    "Egyptian rates: 0% (zero/exempt), 5%, 14%. Verify the category.",
                    FieldPath: $"lines[{i}].vatRatePercent"));
            }
        }
    }

    // ---------- DATE: not in future, not absurd past ----------
    private static void ValidateDocumentDate(SalesInvoice inv, List<ValidationFinding> f)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (inv.DocumentDate > today.AddDays(1))
        {
            f.Add(new ValidationFinding(
                "DATE-001", FindingSeverity.Error,
                "تاريخ الفاتورة في المستقبل",
                "Invoice date is in the future",
                "ETA يرفض الفواتير بتاريخ مستقبلي. استخدم تاريخ اليوم أو أقرب تاريخ سابق.",
                "ETA rejects future-dated invoices. Use today's date or an earlier business date.",
                FieldPath: "documentDate"));
        }
        if (inv.DocumentDate < today.AddYears(-2))
        {
            f.Add(new ValidationFinding(
                "DATE-002", FindingSeverity.Warning,
                "تاريخ الفاتورة قديم جداً (أكثر من سنتين)",
                "Invoice date is very old (more than 2 years)",
                "تأكد من التاريخ — الفواتير الأقدم من سنتين قد ترفض من ETA حسب نشاطك الضريبي.",
                "Verify the date — invoices older than 2 years may be rejected by ETA depending on your tax setup.",
                FieldPath: "documentDate"));
        }
    }
}

public sealed record ValidationFinding(
    string Code,
    FindingSeverity Severity,
    string TitleAr,
    string TitleEn,
    string FixActionAr,
    string FixActionEn,
    string FieldPath);

public enum FindingSeverity
{
    Info,
    Warning,
    Error,
}
