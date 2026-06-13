using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.Sqlite;

namespace CommunityHub.Services;

/// <summary>
/// Blob store that uploads screenshots and game HTML to Azure Blob Storage (identical
/// to <see cref="AzureBlobStore"/>) but records the screenshot index in a local SQLite
/// database instead of Azure SQL. Used by the <c>sqlite</c> app mode.
/// </summary>
public class AzureBlobStoreSqlite : IBlobStore
{
    private readonly BlobServiceClient _client;
    private readonly string _tenant;
    private readonly string _publicBase;
    private readonly string _sqliteConnectionString;

    public string CurrentTenant => _tenant;
    public bool IsLocalServing => false;

    public AzureBlobStoreSqlite(string account, string tenant, string publicBase, string sqliteConnectionString)
    {
        _tenant = tenant;
        _publicBase = publicBase;
        _sqliteConnectionString = sqliteConnectionString;
        var serviceUrl = new Uri($"https://{account}.blob.core.windows.net");
        _client = new BlobServiceClient(serviceUrl, new DefaultAzureCredential());
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var db = SqliteDb.CreateConnection(_sqliteConnectionString);
        await db.OpenAsync();
        using (var pragma = db.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync();
        }
        return db;
    }

    public async Task<string> PutScreenshotAsync(string name, Stream data, string contentType, string? tenant = null)
    {
        var container = _client.GetBlobContainerClient("screenshots");
        var selectedTenant = TenantOrDefault(tenant);
        var blobName = $"{selectedTenant.ToLowerInvariant()}/{name}";
        var blobClient = container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = contentType,
            CacheControl = "public, max-age=31536000, immutable"
        };

        await blobClient.UploadAsync(data, new BlobUploadOptions { HttpHeaders = headers });
        var blobUrl = $"{_publicBase}/screenshots/{blobName}";

        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO screenshots (tenant, blob_url) VALUES ($t, $u)";
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        cmd.Parameters.AddWithValue("$u", blobUrl);
        await cmd.ExecuteNonQueryAsync();

        return blobUrl;
    }

    public async Task<string> PutGalleryHtmlAsync(string name, byte[] data, string? tenant = null)
    {
        var container = _client.GetBlobContainerClient("gallery");
        var selectedTenant = TenantOrDefault(tenant);
        var blobName = $"{selectedTenant.ToLowerInvariant()}/{name}";
        var blobClient = container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = "text/html; charset=utf-8",
            CacheControl = "public, max-age=300"
        };

        using var stream = new MemoryStream(data);
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });
        return $"{_publicBase}/gallery/{blobName}";
    }

    public async Task<List<string>> ListScreenshotsAsync(string? tenant = null)
    {
        using var db = await OpenAsync();
        var selectedTenant = TenantOrDefault(tenant);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT blob_url FROM screenshots WHERE tenant = $t ORDER BY uploaded_at DESC, id DESC";
        cmd.Parameters.AddWithValue("$t", selectedTenant);

        var urls = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            urls.Add(reader.GetString(0));
        return urls;
    }

    public async Task<List<string>> ListTenantsAsync()
    {
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT tenant FROM screenshots ORDER BY tenant";
        var results = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        results.RemoveAll(t => string.Equals(t, CurrentTenant, StringComparison.OrdinalIgnoreCase));
        results.Insert(0, CurrentTenant);
        return results;
    }

    public string ServeGalleryHtml(string name, string? tenant = null) =>
        $"{_publicBase}/gallery/{TenantOrDefault(tenant).ToLowerInvariant()}/{name}";

    public async Task DeleteTenantFilesAsync(string tenant)
    {
        await DeleteContainerBlobsAsync("screenshots", tenant);
        await DeleteContainerBlobsAsync("gallery", tenant);
    }

    public async Task DeleteAllFilesAsync()
    {
        await DeleteContainerBlobsAsync("screenshots", null);
        await DeleteContainerBlobsAsync("gallery", null);
    }

    private string TenantOrDefault(string? requestedTenant)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));
        return string.IsNullOrWhiteSpace(requestedTenant) ? CurrentTenant : requestedTenant;
    }

    private async Task DeleteContainerBlobsAsync(string containerName, string? tenant)
    {
        var container = _client.GetBlobContainerClient(containerName);
        var prefix = string.IsNullOrWhiteSpace(tenant) ? null : tenant.ToLowerInvariant() + "/";

        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
            await container.DeleteBlobIfExistsAsync(blob.Name);
    }
}
