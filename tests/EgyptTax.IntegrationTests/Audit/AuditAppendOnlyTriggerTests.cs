using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Audit;

/// <summary>
/// FR-028 defense-in-depth: the SQL trigger on <c>audit.audit_log</c>
/// MUST block any UPDATE or DELETE that originates from a connection
/// declaring <c>Application Name=EgyptTaxApp</c>. Connections with any other
/// Application Name (sa-driven test code, ad-hoc admin sessions) bypass the
/// trigger so audits and forensic work remain possible.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AuditAppendOnlyTriggerTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Update_FromAppNamedConnection_IsBlockedByTrigger()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        await store.AppendAsync(new AuditLogPayload(
            Kind: "Test",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"x":1}"""));

        var appConn = AppNamedConnection(db);
        await using var conn = new SqlConnection(appConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE [audit].[audit_log] SET [payload_json] = '{\"x\":2}' WHERE [index] = 1";

        var act = async () => await cmd.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<SqlException>()
            .Where(ex => ex.Message.Contains("append-only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Delete_FromAppNamedConnection_IsBlockedByTrigger()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        await store.AppendAsync(new AuditLogPayload(
            Kind: "Test",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"x":1}"""));

        var appConn = AppNamedConnection(db);
        await using var conn = new SqlConnection(appConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM [audit].[audit_log] WHERE [index] = 1";

        var act = async () => await cmd.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<SqlException>()
            .Where(ex => ex.Message.Contains("append-only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_FromAdminConnection_IsAllowed()
    {
        // Sanity: tests connect WITHOUT Application Name=EgyptTaxApp; the
        // trigger must NOT fire so that test scaffolding (and forensic
        // tooling) can still mutate the audit table.
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        await store.AppendAsync(new AuditLogPayload(
            Kind: "Test",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"x":1}"""));

        var rows = await db.Database.ExecuteSqlRawAsync(
            "UPDATE [audit].[audit_log] SET [payload_json] = '{{\"x\":2}}' WHERE [index] = 1");

        rows.Should().Be(1);
    }

    private static string AppNamedConnection(AppDbContext db)
    {
        // Plain-string append avoids the SqlConnectionStringBuilder parser,
        // which is brittle against the Testcontainers default password
        // (special characters: parens + bang).
        var baseConn = db.Database.GetConnectionString()!;
        return baseConn.TrimEnd(';') + ";Application Name=EgyptTaxApp;";
    }
}
