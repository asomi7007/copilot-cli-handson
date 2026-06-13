using CommunityHub.Models;
using Microsoft.Data.Sqlite;

namespace CommunityHub.Services;

/// <summary>
/// SQLite port of <see cref="SqlGalleryIndex"/>. Stores the gallery index rows in a
/// local SQLite file; the actual game HTML still lives in Azure Blob Storage.
/// </summary>
public class SqliteGalleryIndex(string connectionString, string tenant) : IGalleryIndex
{
    public string CurrentTenant => tenant;

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

    public async Task AddAsync(GalleryEntry entry, string? requestedTenant = null)
    {
        using var db = await OpenAsync();
        var selectedTenant = TenantOrDefault(requestedTenant);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO gallery (tenant, name, filename, blob_url) VALUES ($t, $n, $f, $u)";
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        cmd.Parameters.AddWithValue("$n", entry.Name);
        cmd.Parameters.AddWithValue("$f", entry.Filename);
        cmd.Parameters.AddWithValue("$u", entry.Url ?? "");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<GalleryEntry>> ListAsync(int? limit = null, string? requestedTenant = null)
    {
        using var db = await OpenAsync();
        var selectedTenant = TenantOrDefault(requestedTenant);
        using var cmd = db.CreateCommand();
        cmd.CommandText = limit is > 0
            ? "SELECT name, filename, blob_url FROM gallery WHERE tenant = $t ORDER BY uploaded_at DESC, id DESC LIMIT $lim"
            : "SELECT name, filename, blob_url FROM gallery WHERE tenant = $t ORDER BY uploaded_at DESC, id DESC";
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        if (limit is > 0)
            cmd.Parameters.AddWithValue("$lim", limit.Value);

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

    public async Task<int> CountAsync(string? requestedTenant = null)
    {
        using var db = await OpenAsync();
        var selectedTenant = TenantOrDefault(requestedTenant);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM gallery WHERE tenant = $t";
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? 0 : (int)(long)result;
    }

    public async Task<(int Tenant, int AllTenants)> CountWithAllTenantsAsync(string? requestedTenant = null)
    {
        using var db = await OpenAsync();
        var selectedTenant = TenantOrDefault(requestedTenant);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT
                SUM(CASE WHEN tenant = $t THEN 1 ELSE 0 END),
                COUNT(*)
            FROM gallery
            """;
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var tenantCount = reader.IsDBNull(0) ? 0 : (int)reader.GetInt64(0);
            var allTenantsCount = reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1);
            return (tenantCount, allTenantsCount);
        }
        return (0, 0);
    }

    public async Task<List<string>> ListTenantsAsync()
    {
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT tenant FROM gallery ORDER BY tenant";
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
