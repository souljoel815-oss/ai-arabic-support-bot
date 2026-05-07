namespace EgyptTax.Application.Identity;

/// <summary>
/// FR-002 / R-08 — password-hashing port. Production wires
/// <c>Argon2idPasswordHasher</c>; tests substitute a fake. Stored hash
/// format is <c>{algo}${params}${salt}${hash}</c> per R-08 so a future
/// hasher can decide on login whether to re-hash with newer parameters.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
