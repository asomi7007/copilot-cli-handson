namespace CommunityHub.Services;

public class LocalBlobStore : IBlobStore
{
    private readonly string _screenshotsDir;
    private readonly string _galleryDir;

    public string ScreenshotsDir => _screenshotsDir;
    public string GalleryDir => _galleryDir;
    public string CurrentTenant => "local";
    public bool IsLocalServing => true;

    public LocalBlobStore(string dataDir)
    {
        _screenshotsDir = LocalDataDirectories.ScreenshotsPath(dataDir);
        _galleryDir = LocalDataDirectories.GalleryPath(dataDir);
        LocalDataDirectories.EnsureExists(dataDir);
    }

    public async Task<string> PutScreenshotAsync(string name, Stream data, string contentType, string? tenant = null)
    {
        var selectedTenant = TenantOrDefault(tenant);
        var destDir = TenantDirectory(_screenshotsDir, selectedTenant);
        Directory.CreateDirectory(destDir);
        var destPath = Path.Combine(destDir, name);
        using var fs = File.Create(destPath);
        await data.CopyToAsync(fs);
        return TenantUrl("/api/screenshots", selectedTenant, name);
    }

    public async Task<string> PutGalleryHtmlAsync(string name, byte[] data, string? tenant = null)
    {
        var selectedTenant = TenantOrDefault(tenant);
        var destDir = TenantDirectory(_galleryDir, selectedTenant);
        Directory.CreateDirectory(destDir);
        var destPath = Path.Combine(destDir, name);
        await File.WriteAllBytesAsync(destPath, data);
        return TenantUrl("/api/gallery", selectedTenant, name);
    }

    public Task<List<string>> ListScreenshotsAsync(string? tenant = null)
    {
        var selectedTenant = TenantOrDefault(tenant);
        var screenshotsDir = TenantDirectory(_screenshotsDir, selectedTenant);
        if (!Directory.Exists(screenshotsDir))
            return Task.FromResult(new List<string>());

        var entries = Directory.GetFiles(screenshotsDir)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => TenantUrl("/api/screenshots", selectedTenant, n!))
            .Order()
            .ToList();
        return Task.FromResult(entries);
    }

    public Task<List<string>> ListTenantsAsync()
    {
        var tenants = new List<string> { CurrentTenant };
        tenants.AddRange(ListTenantsFromDirectory(_screenshotsDir));
        tenants.AddRange(ListTenantsFromDirectory(_galleryDir));
        return Task.FromResult(tenants.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public string ServeGalleryHtml(string name, string? tenant = null) => TenantUrl("/api/gallery", TenantOrDefault(tenant), name);

    public Task DeleteTenantFilesAsync(string tenant)
    {
        DeleteDirectoryFiles(TenantDirectory(_screenshotsDir, TenantOrDefault(tenant)));
        DeleteDirectoryFiles(TenantDirectory(_galleryDir, TenantOrDefault(tenant)));
        return Task.CompletedTask;
    }

    public Task DeleteAllFilesAsync()
    {
        DeleteDirectoryContents(_screenshotsDir);
        DeleteDirectoryContents(_galleryDir);
        return Task.CompletedTask;
    }

    private string TenantOrDefault(string? tenant)
    {
        if (!TenantHelpers.IsValidTenant(tenant))
            throw new ArgumentException("Invalid tenant", nameof(tenant));
        return string.IsNullOrWhiteSpace(tenant) ? CurrentTenant : tenant;
    }

    private string TenantDirectory(string baseDirectory, string tenant) =>
        string.Equals(tenant, CurrentTenant, StringComparison.OrdinalIgnoreCase)
            ? baseDirectory
            : Path.Combine(baseDirectory, tenant);

    private string TenantUrl(string basePath, string tenant, string name) =>
        string.Equals(tenant, CurrentTenant, StringComparison.OrdinalIgnoreCase)
            ? $"{basePath}/{name}"
            : $"{basePath}/{tenant}/{name}";

    private static IEnumerable<string> ListTenantsFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var tenantDirectory in Directory.EnumerateDirectories(directory))
        {
            var tenant = Path.GetFileName(tenantDirectory);
            if (!string.IsNullOrWhiteSpace(tenant) && TenantHelpers.IsValidTenant(tenant))
                yield return tenant;
        }
    }

    private static void DeleteDirectoryFiles(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(directory))
            File.Delete(file);
    }

    private static void DeleteDirectoryContents(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(directory))
            File.Delete(file);
        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            Directory.Delete(childDirectory, recursive: true);
    }
}
