using Microsoft.Data.SqlClient;
using CommunityHub.Services;

namespace CommunityHub.IntegrationTests;

[Collection("Sql")]
public class MssqlDbTests(SqlFixture fixture)
{
    [Fact]
    public async Task Bootstrap_CreatesAllExpectedTables()
    {
        var expectedTables = new[] { "sessions", "tool_counts", "counters", "gallery", "screenshots" };
        using var conn = new SqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        foreach (var table in expectedTables)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", table);
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            Assert.True(count == 1, $"Expected table '{table}' to exist after bootstrap");
        }
    }

    [Fact]
    public async Task Bootstrap_IsIdempotent()
    {
        // BootstrapAsync was already called by the fixture; running it again must not throw
        var ex = await Record.ExceptionAsync(() => MssqlDb.BootstrapAsync(fixture.ConnectionString));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Bootstrap_CreatesExpectedIndexes()
    {
        var expectedIndexes = new[]
        {
            "idx_sessions_tenant",
            "idx_sessions_tenant_session_id",
            "idx_sessions_tenant_user_info",
            "idx_gallery_tenant",
            "idx_gallery_tenant_uploaded_at",
            "idx_screenshots_tenant",
        };
        using var conn = new SqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        foreach (var index in expectedIndexes)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", index);
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            Assert.True(count == 1, $"Expected index '{index}' to exist after bootstrap");
        }
    }
}
