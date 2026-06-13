using CommunityHub.Models;
using CommunityHub.Services;

namespace CommunityHub.Tests;

public class LocalBlobStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalBlobStore _store;

    public LocalBlobStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "blobtest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new LocalBlobStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Constructor_CreatesDirectories()
    {
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "screenshots")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "gallery")));
    }

    [Fact]
    public void IsLocalServing_ReturnsTrue()
    {
        Assert.True(_store.IsLocalServing);
    }

    [Fact]
    public async Task PutScreenshot_WritesFileAndReturnsUrl()
    {
        var data = new MemoryStream("hello"u8.ToArray());
        var url = await _store.PutScreenshotAsync("test.png", data, "image/png");

        Assert.Equal("/api/screenshots/test.png", url);
        Assert.True(File.Exists(Path.Combine(_store.ScreenshotsDir, "test.png")));
    }

    [Fact]
    public async Task PutGalleryHtml_WritesFileAndReturnsUrl()
    {
        var html = "<html>test</html>"u8.ToArray();
        var url = await _store.PutGalleryHtmlAsync("game.html", html);

        Assert.Equal("/api/gallery/game.html", url);
        var content = await File.ReadAllTextAsync(Path.Combine(_store.GalleryDir, "game.html"));
        Assert.Equal("<html>test</html>", content);
    }

    [Fact]
    public async Task ListScreenshots_ReturnsUrls()
    {
        await _store.PutScreenshotAsync("b.png", new MemoryStream("1"u8.ToArray()), "image/png");
        await _store.PutScreenshotAsync("a.png", new MemoryStream("2"u8.ToArray()), "image/png");

        var urls = await _store.ListScreenshotsAsync();
        Assert.Equal(2, urls.Count);
        Assert.Equal("/api/screenshots/a.png", urls[0]); // sorted
        Assert.Equal("/api/screenshots/b.png", urls[1]);
    }

    [Fact]
    public async Task ListScreenshots_EmptyDir_ReturnsEmpty()
    {
        var names = await _store.ListScreenshotsAsync();
        Assert.Empty(names);
    }

    [Fact]
    public void ServeGalleryHtml_ReturnsLocalPath()
    {
        Assert.Equal("/api/gallery/game.html", _store.ServeGalleryHtml("game.html"));
    }

    [Fact]
    public async Task PutFiles_WithTenant_WritesTenantFilesAndReturnsTenantUrls()
    {
        var screenshotUrl = await _store.PutScreenshotAsync("test.png", new MemoryStream("hello"u8.ToArray()), "image/png", "TenantA");
        var galleryUrl = await _store.PutGalleryHtmlAsync("game.html", "<html>test</html>"u8.ToArray(), "TenantA");

        Assert.Equal("/api/screenshots/TenantA/test.png", screenshotUrl);
        Assert.Equal("/api/gallery/TenantA/game.html", galleryUrl);
        Assert.True(File.Exists(Path.Combine(_store.ScreenshotsDir, "TenantA", "test.png")));
        Assert.True(File.Exists(Path.Combine(_store.GalleryDir, "TenantA", "game.html")));
        Assert.Equal(["/api/screenshots/TenantA/test.png"], await _store.ListScreenshotsAsync("TenantA"));
    }

    [Fact]
    public void ServeGalleryHtml_WithTenant_ReturnsTenantPath()
    {
        Assert.Equal("/api/gallery/TenantA/game.html", _store.ServeGalleryHtml("game.html", "TenantA"));
    }

    [Fact]
    public async Task ListTenants_IncludesTenantsWithFiles()
    {
        await _store.PutScreenshotAsync("test.png", new MemoryStream("hello"u8.ToArray()), "image/png", "ScreenshotTenant");
        await _store.PutGalleryHtmlAsync("game.html", "<html>test</html>"u8.ToArray(), "GalleryTenant");

        var tenants = await _store.ListTenantsAsync();

        Assert.Contains("local", tenants);
        Assert.Contains("ScreenshotTenant", tenants);
        Assert.Contains("GalleryTenant", tenants);
    }
}
