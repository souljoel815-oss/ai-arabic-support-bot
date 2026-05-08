using EgyptTax.Domain.Identity;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Identity;

/// <summary>
/// T052 — FR-002 invariant: MFA cannot be disabled while a user holds
/// the Administrator role (or any role with <c>RequiresMfa = true</c>).
/// The invariant is enforced defensively in
/// <see cref="User.DisableMfa"/>; this test exercises the full
/// EF round-trip path so configuration drift (e.g. role-membership
/// not loading) surfaces here too.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MfaInvariantTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task DisableMfa_ThrowsInvariantViolation_WhenUserHoldsAdministratorRole()
    {
        await using var db = await _fixture.CreateContextAsync();

        var adminRole = new Role(
            code: "ADMIN",
            name: new ArabicEnglishText("مسؤول النظام", "Administrator"),
            requiresMfa: true
        );

        var user = new User(
            email: "admin@firm.eg",
            displayName: new ArabicEnglishText("مسؤول النظام", "System Administrator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );

        user.Roles.Add(adminRole);
        user.EnrollMfa(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        db.Add(adminRole);
        db.Add(user);
        await db.SaveChangesAsync();

        // Reload to verify the invariant survives the EF round-trip.
        db.ChangeTracker.Clear();
        var reloaded = await db.Set<User>().Include(u => u.Roles).FirstAsync(u => u.Id == user.Id);

        var act = () => reloaded.DisableMfa();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*role that requires it*",
                because: "FR-002 forbids disabling MFA on Administrator-style roles"
            );
    }

    [Fact]
    public async Task DisableMfa_Succeeds_AfterRevokingTheMfaRequiringRole()
    {
        await using var db = await _fixture.CreateContextAsync();

        var adminRole = new Role(
            code: "ADMIN",
            name: new ArabicEnglishText("مسؤول النظام", "Administrator"),
            requiresMfa: true
        );

        var user = new User(
            email: "demoted@firm.eg",
            displayName: new ArabicEnglishText("مستخدم", "User"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.En,
            passwordMustChange: false
        );

        user.Roles.Add(adminRole);
        user.EnrollMfa(new byte[] { 0x09, 0x08, 0x07 });

        db.Add(adminRole);
        db.Add(user);
        await db.SaveChangesAsync();

        // Demote — drop the MFA-requiring role first, then DisableMfa is allowed.
        user.Roles.Remove(adminRole);
        user.DisableMfa();
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reloaded = await db.Set<User>().Include(u => u.Roles).FirstAsync(u => u.Id == user.Id);
        reloaded.MfaSecretEncrypted.Should().BeNull();
        reloaded.MfaEnrolledAtUtc.Should().BeNull();
    }
}
