using System.Text.Json;
using CommunityHub.Models;

namespace CommunityHub.Services;

public class LocalGalleryIndex : IGalleryIndex, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly string _galleryDir;
    public string CurrentTenant => "local";

    public LocalGalleryIndex(string dataDir)
    {
        _galleryDir = Path.Combine(dataDir, "gallery");
    }

    public Task AddAsync(GalleryEntry entry, string? tenant = null)
    {
        _lock.EnterWriteLock();
        try
        {
            var indexPath = IndexPathForTenant(TenantOrDefault(tenant));
            var entries = LoadUnsafe(indexPath);
            entries.Add(entry);
            SaveUnsafe(indexPath, entries);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public Task<List<GalleryEntry>> ListAsync(int? limit = null, string? tenant = null)
    {
        _lock.EnterReadLock();
        try
        {
            var entries = LoadUnsafe(IndexPathForTenant(TenantOrDefault(tenant)));
            entries.Reverse();
            if (limit is > 0 && entries.Count > limit.Value)
                entries = entries.Take(limit.Value).ToList();
            return Task.FromResult(entries);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<int> CountAsync(string? tenant = null)
    {
        _lock.EnterReadLock();
        try
        {
            return Task.FromResult(LoadUnsafe(IndexPathForTenant(TenantOrDefault(tenant))).Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<(int Tenant, int AllTenants)> CountWithAllTenantsAsync(string? tenant = null)
    {
        _lock.EnterReadLock();
        try
        {
            var tenantCount = LoadUnsafe(IndexPathForTenant(TenantOrDefault(tenant))).Count;
            var allTenantsCount = ListTenantIndexPathsUnsafe()
                .Sum(indexPath => LoadUnsafe(indexPath).Count);
            return Task.FromResult((tenantCount, allTenantsCount));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<List<string>> ListTenantsAsync()
    {
        var tenants = new List<string> { CurrentTenant };
        if (Directory.Exists(_galleryDir))
        {
            tenants.AddRange(Directory.EnumerateDirectories(_galleryDir)
                .Select(Path.GetFileName)
                .Where(t => !string.IsNullOrWhiteSpace(t) && TenantHelpers.IsValidTenant(t))
                .Cast<string>());
        }
        return Task.FromResult(tenants.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public Task DeleteTenantDataAsync(string tenant)
    {
        _lock.EnterWriteLock();
        try
        {
            SaveUnsafe(IndexPathForTenant(TenantOrDefault(tenant)), []);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllDataAsync() => DeleteTenantDataAsync(CurrentTenant);

    private string TenantOrDefault(string? tenant)
    {
        if (!TenantHelpers.IsValidTenant(tenant))
            throw new ArgumentException("Invalid tenant", nameof(tenant));
        return string.IsNullOrWhiteSpace(tenant) ? CurrentTenant : tenant;
    }

    private string IndexPathForTenant(string tenant) =>
        string.Equals(tenant, CurrentTenant, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(_galleryDir, "index.json")
            : Path.Combine(_galleryDir, tenant, "index.json");

    private IEnumerable<string> ListTenantIndexPathsUnsafe()
    {
        yield return IndexPathForTenant(CurrentTenant);
        if (!Directory.Exists(_galleryDir))
            yield break;

        foreach (var tenantDirectory in Directory.EnumerateDirectories(_galleryDir))
        {
            var tenant = Path.GetFileName(tenantDirectory);
            if (!string.IsNullOrWhiteSpace(tenant) && TenantHelpers.IsValidTenant(tenant))
                yield return IndexPathForTenant(tenant);
        }
    }

    private List<GalleryEntry> LoadUnsafe(string indexPath)
    {
        if (!File.Exists(indexPath))
            return [];

        var data = File.ReadAllText(indexPath);
        if (string.IsNullOrWhiteSpace(data))
            return [];

        return JsonSerializer.Deserialize<List<GalleryEntry>>(data) ?? [];
    }

    private void SaveUnsafe(string indexPath, List<GalleryEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        var data = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(indexPath, data);
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
