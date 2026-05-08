using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Identity;

/// <summary>
/// T051 — Administrator login + MFA enrollment + force-change-password flow
/// per FR-001 / FR-002 / FR-038. Exercises the pieces a UI orchestrator
/// would invoke (password hasher, TOTP, MFA-secret protector, session
/// service) end-to-end against a fresh Testcontainer database, so
/// configuration drift (EF mappings, value-converter wiring, role-
/// membership loading) surfaces here too.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MfaFlowTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Administrator_LoginFlow_EnrollsMfaAndChangesPassword()
    {
        await using var db = await _fixture.CreateContextAsync();

        var hasher = new Argon2idPasswordHasher();
        var totp = new TotpService();
        var protector = new DataProtectionMfaSecretProtector(new EphemeralDataProtectionProvider());
        var clock = new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
        var sessions = new SessionService(db, clock, NoOpAuditLogStore.Instance);

        var adminRole = new Role(
            code: "ADMIN",
            name: new ArabicEnglishText("مسؤول النظام", "Administrator"),
            requiresMfa: true
        );
        var initialPassword = "Init#Pass1234";
        var user = new User(
            email: "admin@firm.eg",
            displayName: new ArabicEnglishText("مسؤول", "Administrator"),
            passwordHash: hasher.Hash(initialPassword),
            preferredLanguage: Language.Ar,
            passwordMustChange: true
        );
        user.Roles.Add(adminRole);
        db.Add(adminRole);
        db.Add(user);
        await db.SaveChangesAsync();

        // Step 1 — initial login attempt with the bootstrap password verifies,
        // but FR-038 force-change must fire before MFA enrollment is allowed.
        hasher.Verify(initialPassword, user.PasswordHash).Should().BeTrue();
        user.PasswordMustChange.Should()
            .BeTrue("FR-038 — admin-issued password must be changed on first login");
        user.RequiresMfa().Should().BeTrue("Administrator role flips RequiresMfa per FR-002");
        user.MfaSecretEncrypted.Should().BeNull("user has not enrolled MFA yet");

        // Step 2 — force-change the password.
        var newPassword = "S3lf-Chosen!Pass77";
        user.SetPassword(hasher.Hash(newPassword), mustChange: false);
        await db.SaveChangesAsync();

        user.PasswordMustChange.Should().BeFalse();
        hasher.Verify(newPassword, user.PasswordHash).Should().BeTrue();
        hasher.Verify(initialPassword, user.PasswordHash).Should().BeFalse();

        // Step 3 — TOTP enrollment. Generate a secret, encrypt, store.
        var plaintextSecret = totp.GenerateSecret();
        var encryptedBytes = protector.Protect(plaintextSecret);
        user.EnrollMfa(encryptedBytes);
        await db.SaveChangesAsync();

        // Step 4 — second login: verify password + TOTP code derived from
        // the decrypted secret. Reload from DB to mimic the request boundary.
        db.ChangeTracker.Clear();
        var reloaded = await db.Set<User>().Include(u => u.Roles).FirstAsync(u => u.Id == user.Id);

        reloaded.MfaSecretEncrypted.Should().NotBeNull();
        var decryptedSecret = protector.Unprotect(reloaded.MfaSecretEncrypted!);
        decryptedSecret.Should().Be(plaintextSecret);
        var code = totp.GenerateCode(decryptedSecret);
        hasher.Verify(newPassword, reloaded.PasswordHash).Should().BeTrue();
        totp.VerifyCode(decryptedSecret, code).Should().BeTrue();

        // Step 5 — open the application session.
        var session = await sessions.BeginAsync(
            reloaded.Id,
            "127.0.0.1",
            "xunit-agent",
            CancellationToken.None
        );
        session.Should().NotBeNull();

        var validation = await sessions.ValidateAsync(session.Id, CancellationToken.None);
        validation.Status.Should().Be(SessionValidationStatus.Valid);
        validation.UserId.Should().Be(reloaded.Id);
    }

    /// <summary>
    /// In-memory test clock so the session service's TTL behaviour is
    /// deterministic without sleeping the test thread.
    /// </summary>
    private sealed class TestClock(DateTime initial) : EgyptTax.SharedKernel.Time.IClock
    {
        public DateTime UtcNow { get; private set; } = initial;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    /// <summary>
    /// Drop-in audit-log store for tests that don't care about chain
    /// recording. The behaviour of the chain itself is covered by the
    /// audit suite; here we want to assert session-service contracts.
    /// </summary>
    internal sealed class NoOpAuditLogStore : EgyptTax.Application.Audit.IAuditLogStore
    {
        public static readonly NoOpAuditLogStore Instance = new();

        public Task<EgyptTax.Domain.Audit.AuditLogEntry> AppendAsync(
            EgyptTax.Domain.Audit.AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            // Synthesise a stub entry; tests exercising the chain itself
            // wire SqlAuditLogStore directly.
            var entry = new EgyptTax.Domain.Audit.AuditLogEntry(
                index: 0,
                tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId,
                actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId,
                kind: payload.Kind,
                payloadJson: payload.PayloadJson,
                prevHash: new byte[32],
                thisHash: new byte[32]
            );
            return Task.FromResult(entry);
        }
    }
}
