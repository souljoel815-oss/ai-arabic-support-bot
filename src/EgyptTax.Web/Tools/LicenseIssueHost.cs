using System.Text.Json;
using EgyptTax.Web.Licensing;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace EgyptTax.Web.Tools;

/// <summary>
/// Vendor-side CLI: sign a license envelope for a specific customer
/// HWID. Output is the license.token file the customer drops at
/// <c>%PROGRAMDATA%\DaftarX\license\</c> on first run.
///
/// Usage:
///   EgyptTax.Web.exe license-issue --keys vendor-keys.json
///                                  --hwid ABCD-1234-EF56-7890
///                                  --customer "شركة الأمل"
///                                  --expires 2027-12-31
///                                 [--edition SMB]              -- Solo / SMB / Enterprise / Firm
///                                 [--max-users 5]              -- override edition default
///                                 [--max-companies 2]          -- override edition default
///                                 [--features "ai_assistant,cloud_backup"]
///                                                              -- comma-separated extras on top of edition defaults
///                                 [--phone "+20 100 123 4567"]
///                                 [--email "sales@daftarx.local"]
///                                 [--out license.token]
/// </summary>
internal static class LicenseIssueHost
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static bool IsLicenseIssueInvocation(string[] args)
        => args.Length > 0 && string.Equals(args[0], "license-issue", StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        var keysPath     = ExtractFlag(args, "--keys") ?? "vendor-keys.json";
        var hwid         = ExtractFlag(args, "--hwid");
        var customer     = ExtractFlag(args, "--customer");
        var expires      = ExtractFlag(args, "--expires");
        var edition      = ExtractFlag(args, "--edition") ?? "SMB";
        var phone        = ExtractFlag(args, "--phone")   ?? "+20 100 000 0000";
        var email        = ExtractFlag(args, "--email")   ?? "sales@daftarx.local";
        var outPath      = ExtractFlag(args, "--out")     ?? "license.token";
        var maxUsersStr      = ExtractFlag(args, "--max-users");
        var maxCompaniesStr  = ExtractFlag(args, "--max-companies");
        var extraFeatures    = ExtractFlag(args, "--features");

        if (string.IsNullOrWhiteSpace(hwid) || string.IsNullOrWhiteSpace(customer) || string.IsNullOrWhiteSpace(expires))
        {
            Console.Error.WriteLine("Required: --hwid, --customer, --expires (yyyy-MM-dd)");
            return 2;
        }
        if (!DateTime.TryParse(expires, out var expiresDate))
        {
            Console.Error.WriteLine("--expires must be a valid date in yyyy-MM-dd form.");
            return 2;
        }
        if (!File.Exists(keysPath))
        {
            Console.Error.WriteLine($"Keys file not found: {keysPath}");
            return 3;
        }

        // Load private key.
        var keysJson = File.ReadAllText(keysPath);
        using var doc = JsonDocument.Parse(keysJson);
        if (!doc.RootElement.TryGetProperty("privateKeyHex", out var privEl))
        {
            Console.Error.WriteLine("Keys file missing privateKeyHex.");
            return 3;
        }
        var privBytes = Convert.FromHexString(privEl.GetString()!);
        var privKey = new Ed25519PrivateKeyParameters(privBytes, 0);

        // Resolve the v2 fields. Parse the edition first; defaults
        // for MaxUsers/MaxCompanies/Features come from
        // LicenseEdition extensions when the caller didn't override.
        var parsedEdition = LicenseEditionExtensions.ParseOrSolo(edition);
        int? maxUsers = null;
        int? maxCompanies = null;
        if (!string.IsNullOrWhiteSpace(maxUsersStr) &&
            int.TryParse(maxUsersStr, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var muInt) && muInt > 0)
        {
            maxUsers = muInt;
        }
        if (!string.IsNullOrWhiteSpace(maxCompaniesStr) &&
            int.TryParse(maxCompaniesStr, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var mcInt) && mcInt > 0)
        {
            maxCompanies = mcInt;
        }

        // Features = edition defaults + any extras the operator
        // passed via --features. Deduplicated.
        var features = Feature.DefaultsFor(parsedEdition).ToList();
        if (!string.IsNullOrWhiteSpace(extraFeatures))
        {
            foreach (var extra in extraFeatures.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = extra.Trim();
                if (trimmed.Length > 0 && !features.Contains(trimmed, StringComparer.Ordinal))
                {
                    features.Add(trimmed);
                }
            }
        }

        // Build payload + canonical bytes + sign. Bumping Version
        // to 2 so verifiers can tell new tokens from pre-Gux.13 ones.
        var payload = new LicensePayload(
            Version: 2,
            Hwid: hwid.Trim(),
            Customer: customer,
            Edition: parsedEdition.ToWireString(),
            IssuedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: DateTime.SpecifyKind(expiresDate, DateTimeKind.Utc),
            SalesPhone: phone,
            SalesEmail: email,
            MaxUsers: maxUsers,
            MaxCompanies: maxCompanies,
            Features: features.Count > 0 ? features.ToArray() : null);

        var canonical = LicensePayloadSerializer.CanonicalBytes(payload);
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privKey);
        signer.BlockUpdate(canonical, 0, canonical.Length);
        var sig = signer.GenerateSignature();

        var envelope = new LicenseEnvelope(payload, Convert.ToBase64String(sig));
        var json = JsonSerializer.Serialize(envelope, Indented);
        File.WriteAllText(outPath, json);

        Console.WriteLine($"Wrote {outPath}");
        Console.WriteLine($"  Customer:     {customer}");
        Console.WriteLine($"  HWID:         {hwid}");
        Console.WriteLine($"  Edition:      {parsedEdition.ToWireString()}");
        Console.WriteLine($"  Expires:      {expiresDate:yyyy-MM-dd}");
        Console.WriteLine($"  Features:     {features.Count} flag(s)");
        if (maxUsers is not null)     Console.WriteLine($"  MaxUsers:     {maxUsers}");
        if (maxCompanies is not null) Console.WriteLine($"  MaxCompanies: {maxCompanies}");
        Console.WriteLine();
        Console.WriteLine($"Send {outPath} to the customer. They place it at:");
        Console.WriteLine(@"  %PROGRAMDATA%\DaftarX\license\license.token");
        Console.WriteLine($"and restart the EgyptTax service.");
        return 0;
    }

    private static string? ExtractFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
