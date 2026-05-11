using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// Gux.13 Tab 7 — accepts a license-token JSON pasted into the
/// admin panel, validates the shape, writes it to the canonical
/// <c>license.token</c> path, and re-runs activation. The runtime
/// <see cref="LicenseStatus"/> updates immediately on success — no
/// service restart needed (the spec calls for "upgrade replaces
/// the license key, no reinstall").
///
/// This is the in-app counterpart to dropping the token file at
/// <c>%PROGRAMDATA%\DaftarX\license\license.token</c> manually;
/// both code paths converge on <see cref="ActivationFlow.TryActivate"/>.
///
/// Windows-only — ActivationFlow uses DPAPI + Registry for share
/// storage. On other platforms returns
/// <see cref="ActivationOutcome.NotSupported"/>.
/// </summary>
public sealed class InAppActivationHandler
{
    public ActivationOutcome Activate(string pastedTokenJson)
    {
        if (!OperatingSystem.IsWindows())
            return ActivationOutcome.Failure(
                IsArabicError: "التفعيل من داخل البرنامج متاح فقط على Windows.",
                EnglishError: "In-app activation is only supported on Windows.");

        if (string.IsNullOrWhiteSpace(pastedTokenJson))
            return ActivationOutcome.Failure(
                IsArabicError: "ألصق محتوى ملف license.token أولاً.",
                EnglishError: "Paste the license.token contents first.");

        // Validate JSON shape before writing to disk so we don't
        // overwrite a working token with garbage.
        try
        {
            using var doc = JsonDocument.Parse(pastedTokenJson);
            if (!doc.RootElement.TryGetProperty("payload", out _) ||
                !doc.RootElement.TryGetProperty("signature", out _))
            {
                return ActivationOutcome.Failure(
                    IsArabicError: "الملف الملصق ليس ترخيص DaftarX صالح. يجب أن يحتوي على payload و signature.",
                    EnglishError: "Pasted content is not a valid DaftarX license envelope (missing payload or signature).");
            }
        }
        catch (JsonException ex)
        {
            return ActivationOutcome.Failure(
                IsArabicError: $"خطأ في صيغة JSON: {ex.Message}",
                EnglishError: $"JSON parse error: {ex.Message}");
        }

        return ActivateOnWindows(pastedTokenJson);
    }

    [SupportedOSPlatform("windows")]
    private static ActivationOutcome ActivateOnWindows(string envelopeJson)
    {
        var stateDir = LicenseGate.DefaultStateDirectory();
        Directory.CreateDirectory(stateDir);

        var flow = new ActivationFlow(stateDir);
        // Write the token to the canonical path then re-run the
        // existing first-run activation path. ActivationFlow updates
        // LicenseStatus + the in-memory master key on success.
        var tokenPath = flow.TokenPath;
        try
        {
            File.WriteAllText(tokenPath, envelopeJson, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return ActivationOutcome.Failure(
                IsArabicError: $"فشل حفظ الملف: {ex.Message}",
                EnglishError: $"Could not write token to {tokenPath}: {ex.Message}");
        }

        var ok = flow.TryActivate();
        if (!ok)
        {
            // TryActivate leaves a bad token on disk + returns false.
            // Best-effort cleanup so the bad file doesn't corrupt the
            // next boot.
            try { File.Delete(tokenPath); } catch { /* best-effort */ }
            return ActivationOutcome.Failure(
                IsArabicError: "فشل التحقق من الترخيص. تأكد أن المفتاح صادر لـ HWID جهازك ولم تنته صلاحيته.",
                EnglishError: "License verification failed. Confirm the token is for this machine's HWID and not expired.");
        }

        return ActivationOutcome.Success(
            IsArabicMessage: $"✓ تم التفعيل بنجاح. الخطة: {EditionGate.CurrentEdition().ArabicLabel()}.",
            EnglishMessage: $"✓ Activated. Edition: {EditionGate.CurrentEdition()}.");
    }
}

public sealed record ActivationOutcome(
    bool Ok,
    string IsArabicMessage,
    string EnglishMessage)
{
    public static ActivationOutcome Success(string IsArabicMessage, string EnglishMessage)
        => new(true, IsArabicMessage, EnglishMessage);

    public static ActivationOutcome Failure(string IsArabicError, string EnglishError)
        => new(false, IsArabicError, EnglishError);
}
