using System.Data;
using Microsoft.Data.SqlClient;

namespace CommunityHub.Services;

public interface IDebugDataCleaner
{
    string CurrentTenant { get; }
    Task<List<string>> ListTenantsAsync();
    Task DeleteTenantAsync(string tenant);
    Task DeleteAllAsync();
}

public sealed class LocalDebugDataCleaner(LocalMetrics metrics, LocalGalleryIndex gallery, LocalBlobStore blobs) : IDebugDataCleaner
{
    public string CurrentTenant => metrics.CurrentTenant;

    public async Task<List<string>> ListTenantsAsync() =>
        TenantHelpers.MergeTenants(await metrics.ListTenantsAsync(), await gallery.ListTenantsAsync(), await blobs.ListTenantsAsync());

    public async Task DeleteTenantAsync(string tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant) || !TenantHelpers.IsValidTenant(tenant))
            throw new ArgumentException("Invalid tenant.", nameof(tenant));

        await blobs.DeleteTenantFilesAsync(tenant);
        await gallery.DeleteTenantDataAsync(tenant);
        await metrics.DeleteTenantDataAsync(tenant);
    }

    public async Task DeleteAllAsync()
    {
        await blobs.DeleteAllFilesAsync();
        await gallery.DeleteAllDataAsync();
        await metrics.DeleteAllDataAsync();
    }
}

public sealed class SqlDebugDataCleaner(string connectionString, string currentTenant, AzureBlobStore blobs) : IDebugDataCleaner
{
    public string CurrentTenant => currentTenant;

    public async Task<List<string>> ListTenantsAsync()
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT tenant FROM sessions WITH (NOLOCK)
            UNION
            SELECT tenant FROM counters WITH (NOLOCK)
            UNION
            SELECT tenant FROM tool_counts WITH (NOLOCK)
            UNION
            SELECT tenant FROM gallery WITH (NOLOCK)
            UNION
            SELECT tenant FROM screenshots WITH (NOLOCK)
            ORDER BY tenant
            """, db);

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

        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var tx = db.BeginTransaction();
        try
        {
            foreach (var sql in DeleteStatements.Select(statement => statement.TenantSql))
            {
                using var cmd = new SqlCommand(sql, db, tx);
                cmd.Parameters.Add(new SqlParameter("@tenant", SqlDbType.NVarChar, 31) { Value = tenant });
                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        await blobs.DeleteTenantFilesAsync(tenant);
    }

    public async Task DeleteAllAsync()
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var tx = db.BeginTransaction();
        try
        {
            foreach (var sql in DeleteStatements.Select(statement => statement.AllSql))
            {
                using var cmd = new SqlCommand(sql, db, tx);
                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        await blobs.DeleteAllFilesAsync();
    }

    private static readonly (string TenantSql, string AllSql)[] DeleteStatements =
    [
        ("DELETE FROM sessions WHERE tenant = @tenant", "DELETE FROM sessions"),
        ("DELETE FROM counters WHERE tenant = @tenant", "DELETE FROM counters"),
        ("DELETE FROM tool_counts WHERE tenant = @tenant", "DELETE FROM tool_counts"),
        ("DELETE FROM gallery WHERE tenant = @tenant", "DELETE FROM gallery"),
        ("DELETE FROM screenshots WHERE tenant = @tenant", "DELETE FROM screenshots")
    ];
}
