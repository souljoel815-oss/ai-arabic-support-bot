using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Persistence;

/// <summary>
/// T027 — apply all EF migrations on a fresh container and assert that the
/// resulting schema contains the audit table, audit_meta checkpoint table,
/// and the append-only trigger from T031.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MigrationSmokeTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Migrate_AppliesCleanly_AndCreatesExpectedObjects()
    {
        await using var db = await _fixture.CreateContextAsync();

        // CreateContextAsync calls MigrateAsync; if it returned, migrations applied.
        var auditTableExists = await ScalarBoolAsync(
            db,
            "SELECT CASE WHEN OBJECT_ID('[audit].[audit_log]', 'U') IS NULL THEN 0 ELSE 1 END");
        var checkpointTableExists = await ScalarBoolAsync(
            db,
            "SELECT CASE WHEN OBJECT_ID('[audit_meta].[checkpoint]', 'U') IS NULL THEN 0 ELSE 1 END");
        var triggerExists = await ScalarBoolAsync(
            db,
            "SELECT CASE WHEN OBJECT_ID('[audit].[trg_audit_log_append_only]', 'TR') IS NULL THEN 0 ELSE 1 END");
        var usersTableExists = await ScalarBoolAsync(
            db,
            "SELECT CASE WHEN OBJECT_ID('[identity].[users]', 'U') IS NULL THEN 0 ELSE 1 END");
        var rolesTableExists = await ScalarBoolAsync(
            db,
            "SELECT CASE WHEN OBJECT_ID('[identity].[roles]', 'U') IS NULL THEN 0 ELSE 1 END");
        var permissionsTableExists = await ScalarBoolAsync(
            db,
            "SELECT CASE WHEN OBJECT_ID('[identity].[permissions]', 'U') IS NULL THEN 0 ELSE 1 END");

        auditTableExists.Should().BeTrue("audit.audit_log table must be created by the Initial migration");
        checkpointTableExists.Should().BeTrue("audit_meta.checkpoint must be created by the AuditCheckpoint migration");
        triggerExists.Should().BeTrue("audit.trg_audit_log_append_only must be created by the AuditAppendOnlyTrigger migration");
        usersTableExists.Should().BeTrue("identity.users must be created by the IdentityCore migration");
        rolesTableExists.Should().BeTrue("identity.roles must be created by the IdentityCore migration");
        permissionsTableExists.Should().BeTrue("identity.permissions must be created by the IdentityCore migration");
    }

    private static async Task<bool> ScalarBoolAsync(DbContext db, string sql)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }
}
