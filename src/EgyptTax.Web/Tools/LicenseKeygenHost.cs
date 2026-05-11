using System.Text.Json;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace EgyptTax.Web.Tools;

/// <summary>
/// Vendor-side CLI: generate a fresh Ed25519 keypair for licensing.
/// Run ONCE per product release. The generated public key gets
/// hardcoded into <c>LicensePublicKey.HexPublicKey</c> before
/// re-publishing the customer build; the private key MUST stay on
/// an offline signing machine.
///
/// Usage:
///   EgyptTax.Web.exe license-keygen [output-path]
/// Default output: vendor-keys.json in the current directory.
/// </summary>
internal static class LicenseKeygenHost
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static bool IsLicenseKeygenInvocation(string[] args)
        => args.Length > 0 && string.Equals(args[0], "license-keygen", StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        var outputPath = args.Length > 1 ? args[1] : "vendor-keys.json";

        var rng = new SecureRandom();
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(rng));
        var pair = generator.GenerateKeyPair();

        var pub = (Ed25519PublicKeyParameters)pair.Public;
        var priv = (Ed25519PrivateKeyParameters)pair.Private;

        var doc = new
        {
            generatedAtUtc = DateTime.UtcNow,
            publicKeyHex = Convert.ToHexString(pub.GetEncoded()),
            privateKeyHex = Convert.ToHexString(priv.GetEncoded()),
            instructions = new[]
            {
                "1. Replace LicensePublicKey.HexPublicKey in source with the publicKeyHex value above.",
                "2. Re-publish the customer build (dotnet publish).",
                "3. KEEP THIS FILE OFFLINE. Move it to a USB key + a sealed envelope.",
                "4. NEVER commit this file. NEVER email it. NEVER store on a network drive.",
                "5. To issue a license: EgyptTax.Web.exe license-issue --keys <this-file> --hwid X --customer Y --expires 2027-12-31",
            },
        };

        var json = JsonSerializer.Serialize(doc, Indented);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Wrote {outputPath}");
        Console.WriteLine($"Public key: {doc.publicKeyHex}");
        Console.WriteLine();
        Console.WriteLine("Update src/EgyptTax.Web/Licensing/LicensePublicKey.cs with the public key above,");
        Console.WriteLine("then re-publish before shipping any customer build.");
        return 0;
    }
}
