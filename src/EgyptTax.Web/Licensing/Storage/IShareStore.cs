namespace EgyptTax.Web.Licensing.Storage;

/// <summary>
/// Read/write a single Shamir share. Three implementations live
/// alongside this interface — DPAPI file, registry, HWID-derived —
/// each storing one of the three 2-of-3 shares.
/// </summary>
public interface IShareStore
{
    string Name { get; }
    bool TryRead(out byte[] share);
    void Write(byte[] share);
    void Erase();
}
