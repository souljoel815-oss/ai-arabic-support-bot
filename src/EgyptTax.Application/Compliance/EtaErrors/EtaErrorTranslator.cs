using System.Text.RegularExpressions;

namespace EgyptTax.Application.Compliance.EtaErrors;

/// <summary>
/// Maps cryptic ETA error responses to operator-friendly explanations
/// in Arabic + a concrete fix action. Replaces the current pattern of
/// the bookkeeper screen-shotting the raw error and forwarding to IT.
///
/// Catalog grows over time as new error patterns emerge; the lookup is
/// pattern-based (regex on code OR message) so we can match families
/// of related errors with one entry. Falls back to the raw error when
/// no pattern matches — never lies to the user.
/// </summary>
public static class EtaErrorTranslator
{
    public static EtaErrorExplanation? Translate(string? errorCode, string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorCode) && string.IsNullOrWhiteSpace(errorMessage))
        {
            return null;
        }

        var haystack = $"{errorCode} {errorMessage}".Trim();

        foreach (var entry in _catalog)
        {
            if (entry.MatchPattern.IsMatch(haystack))
            {
                return entry.Explanation;
            }
        }

        return null;
    }

    private sealed record CatalogEntry(Regex MatchPattern, EtaErrorExplanation Explanation);

    private static readonly CatalogEntry[] _catalog = new[]
    {
        // ---- Authentication / signature ----
        new CatalogEntry(
            new Regex(@"\b401\b|Unauthorized|invalid_token|access[_ ]denied", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "مشكلة شهادة التوقيع",
                CategoryEn: "Signing certificate problem",
                ExplanationAr: "شهادة التوقيع الإلكتروني (eSeal) منتهية أو غير صالحة. كل محاولات الإرسال هتفشل لحد ما تتجدّد.",
                ExplanationEn: "The eSeal signing certificate is expired or invalid. All submissions will fail until renewed.",
                FixActionAr: "افتح صفحة الشهادات (/certificates) واتأكد من تاريخ انتهاء شهادة التوقيع. لو منتهية، تواصل مع Egypt Trust أو MCDR — التجديد بياخد 3-5 أيام عمل.",
                FixActionEn: "Open /certificates and check the signing cert expiry. If expired, contact Egypt Trust or MCDR — renewal takes 3-5 business days.",
                Severity: EtaErrorSeverity.Critical)),

        new CatalogEntry(
            new Regex(@"signature[_ ]?(?:failed|invalid)|InvalidSignature", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "فشل التوقيع",
                CategoryEn: "Signature failed",
                ExplanationAr: "ETA رفض الـsignature في الفاتورة — إما الشهادة مش متطابقة مع الـTIN أو الـHSM فيه مشكلة.",
                ExplanationEn: "ETA rejected the invoice signature — either cert/TIN mismatch or HSM issue.",
                FixActionAr: "اتأكد إن الشهادة المرتبطة بالخدمة هي نفسها المسجلة لدى ETA لـTIN الشركة. لو الـHSM، اعمل restart للـHSM driver.",
                FixActionEn: "Verify the cert matches the TIN registered with ETA. If using HSM, restart the HSM driver.",
                Severity: EtaErrorSeverity.Critical)),

        // ---- TIN / taxpayer validation ----
        new CatalogEntry(
            new Regex(@"TIN(?:\s|_)?(?:not[_ ]?found|invalid|deregistered)|tax[_ ]?payer[_ ]?(?:not[_ ]?found|invalid)|InvalidIssuer|InvalidReceiver", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "رقم ضريبي غير صحيح",
                CategoryEn: "Invalid TIN",
                ExplanationAr: "ETA لم يجد أو رفض الرقم الضريبي للعميل/المورد. ربما الـTIN انتهى تسجيله أو فيه خطأ في الكتابة.",
                ExplanationEn: "ETA can't find or rejected the buyer/seller TIN. Could be deregistered or mistyped.",
                FixActionAr: "افتح ملف العميل (/customers) وتأكد من رقمه الضريبي. لو صحيح، اطلب من العميل يتأكد من تسجيله النشط في مصلحة الضرائب.",
                FixActionEn: "Open the customer profile, verify the TIN. If correct, ask the customer to confirm active ETA registration.",
                Severity: EtaErrorSeverity.High)),

        // ---- Math / totals validation ----
        new CatalogEntry(
            new Regex(@"totalSalesAmount|totalAmount|Math|math[_ ]?validation|amount[_ ]?mismatch", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "مجاميع الفاتورة غير متطابقة",
                CategoryEn: "Invoice totals don't match",
                ExplanationAr: "ETA حسب مجموع البنود وطلع رقم مختلف عن المسجل في الفاتورة. غالباً السبب فرق تقريب 0.01 ج.م.",
                ExplanationEn: "ETA recomputed line totals and got a different number than what was sent. Usually a 0.01 EGP rounding diff.",
                FixActionAr: "افتح الفاتورة وراجع مجاميع البنود — تأكد إن (الكمية × سعر الوحدة) تطابق إجمالي البند بدون تقريب. لو فيه discount، اعمل reposting للفاتورة بعد إعادة الحساب.",
                FixActionEn: "Open the invoice and review line totals — make sure (qty × unit price) matches line total without rounding. If discount is involved, repost after recomputation.",
                Severity: EtaErrorSeverity.High)),

        // ---- EGS / item code ----
        new CatalogEntry(
            new Regex(@"EGS[_ ]?(?:code|invalid|not[_ ]?found)|item[_ ]?code[_ ]?(?:invalid|not[_ ]?found)|GS1[_ ]?(?:invalid|not[_ ]?found)|InvalidItem", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "كود الصنف غير مفعّل",
                CategoryEn: "Item code not active",
                ExplanationAr: "كود EGS أو GS1 لأحد الأصناف غير مفعّل في نظام مصلحة الضرائب بعد. EGS بياخد 15 يوم تقريباً للتفعيل، GS1 بياخد 24-48 ساعة.",
                ExplanationEn: "An item's EGS or GS1 code isn't active in ETA's catalog yet. EGS takes ~15 days, GS1 takes 24-48h.",
                FixActionAr: "افتح صفحة الأصناف (/items) وتأكد من حالة الكود. لو EGS لسه pending، استخدم كود GS1 مؤقت لحد ما يفعّل.",
                FixActionEn: "Open /items and check the code status. If EGS is still pending, use a temporary GS1 code until it activates.",
                Severity: EtaErrorSeverity.Medium)),

        // ---- VAT category / rate ----
        new CatalogEntry(
            new Regex(@"VAT[_ ]?(?:invalid|category|rate)|tax[_ ]?(?:type|code)[_ ]?invalid|InvalidTaxableItems", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "فئة ضريبة غير صحيحة",
                CategoryEn: "Invalid VAT category",
                ExplanationAr: "فئة الضريبة المختارة لأحد البنود مش صحيحة لنشاط الشركة أو لنوع البضاعة في ETA.",
                ExplanationEn: "VAT category for an item doesn't match ETA's allowed categories for your business activity or item type.",
                FixActionAr: "افتح فئات الضريبة (/settings/vat-categories) وتأكد من الـcode. الفئات الافتراضية: STANDARD-14 (14%) و REDUCED-5 (5%) و ZERO-RATED (تصدير) و EXEMPT (معفاة).",
                FixActionEn: "Check VAT categories at /settings/vat-categories. Defaults: STANDARD-14, REDUCED-5, ZERO-RATED, EXEMPT.",
                Severity: EtaErrorSeverity.Medium)),

        // ---- Date / period ----
        new CatalogEntry(
            new Regex(@"date[_ ]?(?:invalid|future|outside)|period[_ ]?(?:closed|locked)|InvalidDateTimeIssued", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "تاريخ الفاتورة خارج النطاق المسموح",
                CategoryEn: "Invoice date outside allowed range",
                ExplanationAr: "تاريخ الفاتورة في المستقبل، أو في فترة ضريبية مقفولة. ETA لا يقبل فواتير برّه نافذة الإصدار.",
                ExplanationEn: "Invoice date is in the future, or in a locked tax period. ETA rejects out-of-window invoices.",
                FixActionAr: "افتح الفاتورة وراجع التاريخ. لو في فترة مقفولة، يحتاج المسؤول يفتح الفترة من /settings/tax-periods قبل إعادة المحاولة.",
                FixActionEn: "Open the invoice and review the date. If in a locked period, an Administrator must reopen the period from /settings/tax-periods.",
                Severity: EtaErrorSeverity.Medium)),

        // ---- Schema / structure ----
        new CatalogEntry(
            new Regex(@"BadArgument|schema[_ ]?(?:invalid|validation)|missing[_ ]?(?:required|field)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "خطأ في تركيب الفاتورة",
                CategoryEn: "Invoice structure error",
                ExplanationAr: "ETA رفض شكل الفاتورة لأن في حقل ناقص أو غير صحيح. تفاصيل تقنية بالأسفل.",
                ExplanationEn: "ETA rejected the invoice structure — a required field is missing or malformed. Technical details below.",
                FixActionAr: "افتح الفاتورة وتأكد من اكتمال بيانات العميل (الاسم، العنوان، الـTIN) وكل بنود الفاتورة (وحدة قياس، كمية، سعر).",
                FixActionEn: "Open the invoice and check that all customer fields (name, address, TIN) and line fields (unit, qty, price) are complete.",
                Severity: EtaErrorSeverity.High)),

        // ---- Network / timeout ----
        new CatalogEntry(
            new Regex(@"timeout|connection[_ ]?(?:failed|refused|reset)|network[_ ]?error|503|504|gateway", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "مشكلة اتصال بـETA",
                CategoryEn: "ETA connection problem",
                ExplanationAr: "خوادم ETA لم ترد في الوقت المناسب. غالباً مشكلة مؤقتة من جهتهم.",
                ExplanationEn: "ETA servers didn't respond in time. Usually a temporary issue on their side.",
                FixActionAr: "ارجع للفاتورة بعد 5-10 دقايق واضغط 'إعادة الإرسال'. لو استمرت المشكلة لساعات، تأكد من اتصال السيرفر بالإنترنت.",
                FixActionEn: "Wait 5-10 minutes and retry submission. If it persists for hours, check the server's internet connection.",
                Severity: EtaErrorSeverity.Medium)),

        // ---- Duplicate / already submitted ----
        new CatalogEntry(
            new Regex(@"duplicate|already[_ ]?(?:submitted|exists)|InvalidUuid|UuidAlreadyExists", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new EtaErrorExplanation(
                CategoryAr: "الفاتورة تم إرسالها قبل كده",
                CategoryEn: "Invoice already submitted",
                ExplanationAr: "ETA عنده الفاتورة دي بنفس الـUUID. الإرسال السابق نجح فعلياً ولكن الرد ضاع منّا.",
                ExplanationEn: "ETA already has this invoice with the same UUID. The earlier submission succeeded but we missed the response.",
                FixActionAr: "افتح لوحة ETA (/eta-dashboard) وحدّث حالة الإرسال يدوياً لـSubmitted. الفاتورة موجودة عند المصلحة بالفعل.",
                FixActionEn: "Open /eta-dashboard and manually update the submission to Submitted. The invoice is already at ETA.",
                Severity: EtaErrorSeverity.Low)),
    };
}

public sealed record EtaErrorExplanation(
    string CategoryAr,
    string CategoryEn,
    string ExplanationAr,
    string ExplanationEn,
    string FixActionAr,
    string FixActionEn,
    EtaErrorSeverity Severity);

public enum EtaErrorSeverity
{
    Low,
    Medium,
    High,
    Critical,
}
