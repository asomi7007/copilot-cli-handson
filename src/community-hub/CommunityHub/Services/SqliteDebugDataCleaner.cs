using Microsoft.Data.Sqlite;

namespace CommunityHub.Services;

/// <summary>
/// SQLite port of <see cref="SqlDebugDataCleaner"/>. Clears tenant data from the
/// SQLite tables and deletes the corresponding blobs.
/// </summary>
public sealed class SqliteDebugDataCleaner(string connectionString, string currentTenant, AzureBlobStoreSqlite blobs) : IDebugDataCleaner
{
    public string CurrentTenant => currentTenant;

    private async Task<SqliteConnection> OpenAsync()
    {
        var db = SqliteDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using (var pragma = db.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync();
        }
        return db;
    }

    public async Task<List<string>> ListTenantsAsync()
    {
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT tenant FROM sessions
            UNION SELECT tenant FROM counters
            UNION SELECT tenant FROM tool_counts
            UNION SELECT tenant FROM gallery
            UNION SELECT tenant FROM screenshots
            ORDER BY tenant
            """;
        var tenants = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tenants.Add(reader.GetString(0));
        tenants.RemoveAll(t => string.Equals(t, CurrentTenant, StringComparison.OrdinalIgnoreCase));
        tenants.Insert(0, CurrentTenant);
        return tenants;
    }

    public async Task DeleteTenantAsync(string tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant) || !TenantHelpers.IsValidTenant(tenant))
            throw new ArgumentException("Invalid tenant.", nameof(tenant));

        using (var db = await OpenAsync())
        using (var tx = (SqliteTransaction)await db.BeginTransactionAsync())
        {
            foreach (var (tenantSql, _) in DeleteStatements)
            {
                using var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = tenantSql;
                cmd.Parameters.AddWithValue("$tenant", tenant);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }

        await blobs.DeleteTenantFilesAsync(tenant);
    }

    public async Task DeleteAllAsync()
    {
        using (var db = await OpenAsync())
        using (var tx = (SqliteTransaction)await db.BeginTransactionAsync())
        {
            foreach (var (_, allSql) in DeleteStatements)
            {
                using var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = allSql;
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }

        await blobs.DeleteAllFilesAsync();
    }

    private static readonly (string TenantSql, string AllSql)[] DeleteStatements =
    [
        ("DELETE FROM sessions WHERE tenant = $tenant", "DELETE FROM sessions"),
        ("DELETE FROM counters WHERE tenant = $tenant", "DELETE FROM counters"),
        ("DELETE FROM tool_counts WHERE tenant = $tenant", "DELETE FROM tool_counts"),
        ("DELETE FROM gallery WHERE tenant = $tenant", "DELETE FROM gallery"),
        ("DELETE FROM screenshots WHERE tenant = $tenant", "DELETE FROM screenshots")
    ];
}
