using CommunityHub.Models;
using CommunityHub.Services;

namespace CommunityHub.Tests;

public class DebugDataCleanerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalBlobStore _blobs;
    private readonly LocalGalleryIndex _gallery;
    private readonly LocalMetrics _metrics;
    private readonly LocalDebugDataCleaner _cleaner;

    public DebugDataCleanerTests()
    {
        _tempDir = Path.GetTempPath() + "debugcleaner_" + Guid.NewGuid().ToString("N");
        LocalDataDirectories.EnsureExists(_tempDir);
        _blobs = new LocalBlobStore(_tempDir);
        _gallery = new LocalGalleryIndex(_tempDir);
        _metrics = new LocalMetrics();
        _cleaner = new LocalDebugDataCleaner(_metrics, _gallery, _blobs);
    }

    public void Dispose()
    {
        _gallery.Dispose();
        _metrics.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task DeleteTenantAsync_ClearsLocalMetricsGalleryAndFiles()
    {
        await _metrics.OnSessionStartAsync("s1", "u1");
        await _metrics.OnToolUsedAsync("tool-a");
        await _gallery.AddAsync(new GalleryEntry { Name = "Game", Filename = "game.html", Url = "/api/gallery/game.html" });
        using var screenshotStream = new MemoryStream("image"u8.ToArray());
        await _blobs.PutScreenshotAsync("shot.png", screenshotStream, "image/png");
        await _blobs.PutGalleryHtmlAsync("game.html", "html"u8.ToArray());

        await _cleaner.DeleteTenantAsync("local");

        var snapshot = await _metrics.SnapshotAsync();
        Assert.Equal(0, snapshot.SessionCount);
        Assert.Equal(0, snapshot.ToolCalls);
        Assert.Empty(await _gallery.ListAsync());
        Assert.Empty(await _blobs.ListScreenshotsAsync());
        Assert.False(File.Exists(Path.Combine(_blobs.GalleryDir, Path.GetFileName("game.html"))));
    }

    [Fact]
    public async Task DeleteTenantAsync_IgnoresOtherTenantInLocalMode()
    {
        await _metrics.OnSessionStartAsync("s1", "u1");
        await _gallery.AddAsync(new GalleryEntry { Name = "Game", Filename = "game.html" });

        await _cleaner.DeleteTenantAsync("other");

        Assert.Equal(1, (await _metrics.SnapshotAsync()).SessionCount);
        Assert.Single(await _gallery.ListAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_ReturnsLocalTenant()
    {
        var tenants = await _cleaner.ListTenantsAsync();
        Assert.Equal(["local"], tenants);
    }
}
