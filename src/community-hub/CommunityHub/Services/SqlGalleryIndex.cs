using CommunityHub.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CommunityHub.Services;

public class SqlGalleryIndex(string connectionString, string tenant) : IGalleryIndex
{
    public string CurrentTenant => tenant;

    public async Task AddAsync(GalleryEntry entry, string? requestedTenant = null)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        var selectedTenant = TenantOrDefault(requestedTenant);
        using var cmd = new SqlCommand(
            "INSERT INTO gallery (tenant, name, filename, blob_url) VALUES (@p1, @p2, @p3, @p4)", db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        cmd.Parameters.Add(new SqlParameter("@p2", SqlDbType.NVarChar, 255) { Value = entry.Name });
        cmd.Parameters.Add(new SqlParameter("@p3", SqlDbType.NVarChar, 255) { Value = entry.Filename });
        cmd.Parameters.Add(new SqlParameter("@p4", SqlDbType.NVarChar, -1) { Value = entry.Url ?? "" });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<GalleryEntry>> ListAsync(int? limit = null, string? requestedTenant = null)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        var selectedTenant = TenantOrDefault(requestedTenant);
        var sql = limit is > 0
            ? "SELECT TOP (@p2) name, filename, blob_url FROM gallery WITH (NOLOCK) WHERE tenant = @p1 ORDER BY uploaded_at DESC, id DESC"
            : "SELECT name, filename, blob_url FROM gallery WITH (NOLOCK) WHERE tenant = @p1 ORDER BY uploaded_at DESC, id DESC";
        using var cmd = new SqlCommand(sql, db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        if (limit is > 0)
            cmd.Parameters.Add(new SqlParameter("@p2", SqlDbType.Int) { Value = limit.Value });

        var entries = new List<GalleryEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var entry = new GalleryEntry
            {
                Name = reader.GetString(0),
                Filename = reader.GetString(1)
            };
            var blobUrl = reader.GetString(2);
            if (!string.IsNullOrEmpty(blobUrl))
                entry.Url = blobUrl;
            entries.Add(entry);
        }
        return entries;
    }

    public async Task<int> CountAsync(string? tenant = null)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        var selectedTenant = TenantOrDefault(tenant);
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM gallery WITH (NOLOCK) WHERE tenant = @p1", db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    public async Task<(int Tenant, int AllTenants)> CountWithAllTenantsAsync(string? tenant = null)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        var selectedTenant = TenantOrDefault(tenant);
        using var cmd = new SqlCommand("""
            SELECT
                SUM(CASE WHEN tenant = @p1 THEN 1 ELSE 0 END),
                COUNT(*)
            FROM gallery WITH (NOLOCK)
            """, db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var tenantCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            var allTenantsCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            return (tenantCount, allTenantsCount);
        }
        return (0, 0);
    }

    public async Task<List<string>> ListTenantsAsync()
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var cmd = new SqlCommand("SELECT DISTINCT tenant FROM gallery WITH (NOLOCK) ORDER BY tenant", db);
        var results = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        results.RemoveAll(t => string.Equals(t, CurrentTenant, StringComparison.OrdinalIgnoreCase));
        results.Insert(0, CurrentTenant);
        return results;
    }

    private string TenantOrDefault(string? requestedTenant)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));
        return string.IsNullOrWhiteSpace(requestedTenant) ? CurrentTenant : requestedTenant;
    }
}
