namespace EgyptTax.Portal.Application.Licences;

/// <summary>
/// T028. Signs a paid licence token. Reads the Ed25519 keypair from
/// <c>vendor-keys.json</c> (path from
/// <c>LicenceSigning:VendorKeysPath</c> config). Production lives behind
/// a secret-store mount on the App Service. Implementation in
/// <c>Infrastructure/Licences/Ed25519LicenceSigningService.cs</c>.
/// </summary>
public interface ILicenceSigningService
{
    /// <summary>
    /// Sign the given payload and return the wire-format envelope as a
    /// base64-encoded JSON byte stream — the customer downloads this
    /// blob as <c>license.token</c> and drops it at
    /// <c>%PROGRAMDATA%\DaftarX\license\</c> on their machine.
    /// </summary>
    Task<LicenceEnvelopeDto> SignAsync(LicencePayloadDto payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Public-key-only verifier used by the portal's own tests + diagnostics
/// (the real verifier is in the on-prem product's
/// <c>EgyptTax.Web.Licensing.LicenseVerifier</c>; the portal mirrors the
/// minimal verify path so the contract tests can assert end-to-end byte
/// equivalence without depending on the on-prem product).
/// </summary>
public interface ILicenceSignatureVerifier
{
    bool Verify(LicenceEnvelopeDto envelope);
}
