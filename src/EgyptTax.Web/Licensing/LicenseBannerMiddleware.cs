using System.Net;
using System.Text;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// When the license gate failed, every request returns the banner
/// page (HTTP 451) showing the HWID + sales contact info — except
/// the public health probe (kept open so monitoring dashboards
/// don't false-positive on a perfectly-running-but-unlicensed
/// service).
///
/// Bilingual banner (Arabic + English). Renders inline so it works
/// with no static files / templates / fonts.
/// </summary>
public sealed class LicenseBannerMiddleware
{
    private readonly RequestDelegate _next;

    public LicenseBannerMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (LicenseStatus.IsLicensed)
        {
            await _next(ctx);
            return;
        }

        // Health probe stays open — monitoring should still see "up".
        var path = ctx.Request.Path.Value ?? "";
        if (path.StartsWith("/api/v1/health/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        ctx.Response.StatusCode = (int)HttpStatusCode.UnavailableForLegalReasons;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(RenderBanner());
    }

    private static string RenderBanner()
    {
        var hwid = LicenseStatus.Hwid;
        var phone = LicenseStatus.SalesPhone;
        var email = LicenseStatus.SalesEmail;
        var reason = LicenseStatus.FailureReason;
        var (titleAr, bodyAr) = ArabicCopy(reason);
        var (titleEn, bodyEn) = EnglishCopy(reason);

        var sb = new StringBuilder(2048);
        sb.AppendLine("<!doctype html><html lang=\"ar\" dir=\"rtl\"><head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<title>DaftarX — التفعيل مطلوب / Activation required</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',Tahoma,sans-serif;background:#0b1d3a;color:#fff;margin:0;padding:40px;line-height:1.7}");
        sb.AppendLine(".card{max-width:760px;margin:40px auto;background:#16284a;border:1px solid #1e3a5f;border-radius:14px;padding:32px;box-shadow:0 12px 28px rgba(0,0,0,0.3)}");
        sb.AppendLine("h1{font-size:24px;margin:0 0 8px;color:#fff}");
        sb.AppendLine("h2{font-size:18px;margin:24px 0 8px;color:#3b82f6}");
        sb.AppendLine(".hwid{font-family:'JetBrains Mono','Consolas',monospace;font-size:18px;background:#0b1d3a;padding:12px 16px;border-radius:8px;border:1px solid #3b82f6;display:inline-block;margin:8px 0}");
        sb.AppendLine(".contact{background:#0b1d3a;border-radius:8px;padding:16px;margin-top:16px}");
        sb.AppendLine(".contact a{color:#3b82f6;text-decoration:none;font-weight:600}");
        sb.AppendLine(".divider{height:1px;background:#1e3a5f;margin:32px 0}");
        sb.AppendLine("section[lang=en]{direction:ltr;text-align:left}");
        sb.AppendLine("</style></head><body><div class=\"card\">");

        sb.Append("<section lang=\"ar\" dir=\"rtl\">");
        sb.Append("<h1>🔒 ").Append(WebEncode(titleAr)).Append("</h1>");
        sb.Append("<p>").Append(WebEncode(bodyAr)).Append("</p>");
        sb.Append("<h2>معرّف الجهاز (HWID)</h2>");
        sb.Append("<div class=\"hwid\">").Append(WebEncode(hwid)).Append("</div>");
        sb.Append("<div class=\"contact\">");
        sb.Append("<strong>للاتصال بالمبيعات:</strong><br>");
        sb.Append("هاتف: <a href=\"tel:").Append(WebEncode(phone)).Append("\">").Append(WebEncode(phone)).Append("</a><br>");
        sb.Append("بريد إلكتروني: <a href=\"mailto:").Append(WebEncode(email)).Append("\">").Append(WebEncode(email)).Append("</a>");
        sb.Append("</div>");
        sb.Append("</section>");

        sb.Append("<div class=\"divider\"></div>");

        sb.Append("<section lang=\"en\" dir=\"ltr\">");
        sb.Append("<h1>🔒 ").Append(WebEncode(titleEn)).Append("</h1>");
        sb.Append("<p>").Append(WebEncode(bodyEn)).Append("</p>");
        sb.Append("<h2>Hardware ID (HWID)</h2>");
        sb.Append("<div class=\"hwid\">").Append(WebEncode(hwid)).Append("</div>");
        sb.Append("<div class=\"contact\">");
        sb.Append("<strong>Contact sales:</strong><br>");
        sb.Append("Phone: <a href=\"tel:").Append(WebEncode(phone)).Append("\">").Append(WebEncode(phone)).Append("</a><br>");
        sb.Append("Email: <a href=\"mailto:").Append(WebEncode(email)).Append("\">").Append(WebEncode(email)).Append("</a>");
        sb.Append("</div>");
        sb.Append("</section>");

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static (string title, string body) ArabicCopy(LicenseFailureReason r) => r switch
    {
        LicenseFailureReason.EnvelopeMissingOrEmpty => (
            "مطلوب التفعيل قبل استخدام DaftarX",
            "هذه نسخة غير مفعّلة من DaftarX. أرسل معرّف الجهاز التالي إلى فريق المبيعات للحصول على ملف التفعيل (license.token)."),
        LicenseFailureReason.HwidMismatch => (
            "الترخيص لا يطابق هذا الجهاز",
            "تم نقل النسخة من جهاز آخر. الترخيص الحالي مرتبط بـ HWID مختلف. اتصل بالمبيعات لإعادة الإصدار."),
        LicenseFailureReason.Expired => (
            "انتهت صلاحية الترخيص",
            "ترخيص DaftarX انتهى. اتصل بالمبيعات للتجديد."),
        LicenseFailureReason.SignatureInvalid => (
            "تم اكتشاف ترخيص مزوّر",
            "ملف الترخيص لا يمكن التحقق منه. اتصل بالمبيعات."),
        LicenseFailureReason.SharesUnreadable => (
            "تم اكتشاف نسخ غير مصرّح بها",
            "بيانات التفعيل المخزّنة على الجهاز غير مكتملة. هذا يحدث عند نسخ مجلد البرنامج إلى جهاز آخر دون إعادة التفعيل. اتصل بالمبيعات."),
        LicenseFailureReason.DecryptionFailed => (
            "فشل فك تشفير الترخيص",
            "ملف الترخيص النشط مشفّر بمفتاح مختلف عن مفتاح هذا الجهاز. اتصل بالمبيعات."),
        _ => (
            "خطأ في التحقق من الترخيص",
            "DaftarX لم يستطع التحقق من الترخيص الحالي. اتصل بالمبيعات."),
    };

    private static (string title, string body) EnglishCopy(LicenseFailureReason r) => r switch
    {
        LicenseFailureReason.EnvelopeMissingOrEmpty => (
            "Activation required",
            "This DaftarX install is not activated. Send the Hardware ID below to sales to receive a license.token file."),
        LicenseFailureReason.HwidMismatch => (
            "License does not match this machine",
            "The install appears to have been copied from another machine. The activated license is bound to a different HWID. Contact sales for re-issue."),
        LicenseFailureReason.Expired => (
            "License expired",
            "The DaftarX license has expired. Contact sales to renew."),
        LicenseFailureReason.SignatureInvalid => (
            "Forged license detected",
            "The license file failed signature verification. Contact sales."),
        LicenseFailureReason.SharesUnreadable => (
            "Unauthorized copy detected",
            "The activation material on this machine is incomplete. This happens when the install folder is copied to a different machine without re-activation. Contact sales."),
        LicenseFailureReason.DecryptionFailed => (
            "License decryption failed",
            "The activated license file is encrypted with a key that doesn't match this machine. Contact sales."),
        _ => (
            "License verification error",
            "DaftarX could not verify the current license. Contact sales."),
    };

    private static string WebEncode(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
