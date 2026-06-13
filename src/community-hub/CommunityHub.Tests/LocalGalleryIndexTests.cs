using CommunityHub.Models;
using CommunityHub.Services;

namespace CommunityHub.Tests;

public class LocalGalleryIndexTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalGalleryIndex _index;

    public LocalGalleryIndexTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "gallerytest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "gallery"));
        _index = new LocalGalleryIndex(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task List_Empty_ReturnsEmptyList()
    {
        var entries = await _index.ListAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Count_Empty_ReturnsZero()
    {
        var count = await _index.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Add_ThenList_ReturnsEntries()
    {
        var entry1 = new GalleryEntry { Name = "Game 1", Filename = "file1.html", Url = "/api/gallery/file1.html" };
        var entry2 = new GalleryEntry { Name = "Game 2", Filename = "file2.html", Url = "/api/gallery/file2.html" };

        await _index.AddAsync(entry1);
        await _index.AddAsync(entry2);

        var entries = await _index.ListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Game 2", entries[0].Name);
        Assert.Equal("Game 1", entries[1].Name);
    }

    [Fact]
    public async Task List_WithLimit_ReturnsLatestEntries()
    {
        await _index.AddAsync(new GalleryEntry { Name = "Game 1", Filename = "file1.html" });
        await _index.AddAsync(new GalleryEntry { Name = "Game 2", Filename = "file2.html" });
        await _index.AddAsync(new GalleryEntry { Name = "Game 3", Filename = "file3.html" });

        var entries = await _index.ListAsync(limit: 2);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Game 3", entries[0].Name);
        Assert.Equal("Game 2", entries[1].Name);
    }

    [Fact]
    public async Task Count_AfterAdds_ReturnsCorrectCount()
    {
        await _index.AddAsync(new GalleryEntry { Name = "A", Filename = "a.html" });
        await _index.AddAsync(new GalleryEntry { Name = "B", Filename = "b.html" });

        var count = await _index.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PersistsToFile()
    {
        var entry = new GalleryEntry { Name = "Game", Filename = "g.html", Url = "/api/gallery/g.html" };
        await _index.AddAsync(entry);

        var indexPath = Path.Combine(_tempDir, "gallery", "index.json");
        Assert.True(File.Exists(indexPath));

        var content = await File.ReadAllTextAsync(indexPath);
        Assert.Contains("Game", content);
    }

    [Fact]
    public async Task WhitespaceIndexFile_LoadsAsEmpty()
    {
        var indexPath = Path.Combine(_tempDir, "gallery", "index.json");
        await File.WriteAllTextAsync(indexPath, "   ");

        var entries = await _index.ListAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var index = new LocalGalleryIndex(_tempDir);
        var ex = Record.Exception(() => index.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Add_WithTenant_StoresAndListsTenantEntriesSeparately()
    {
        await _index.AddAsync(new GalleryEntry { Name = "Local", Filename = "local.html" });
        await _index.AddAsync(new GalleryEntry { Name = "Tenant", Filename = "tenant.html" }, "TenantA");

        var localEntries = await _index.ListAsync();
        var tenantEntries = await _index.ListAsync(tenant: "TenantA");
        var tenants = await _index.ListTenantsAsync();

        Assert.Single(localEntries);
        Assert.Equal("Local", localEntries[0].Name);
        Assert.Single(tenantEntries);
        Assert.Equal("Tenant", tenantEntries[0].Name);
        Assert.Equal(1, await _index.CountAsync());
        Assert.Equal(1, await _index.CountAsync("TenantA"));
        Assert.Equal((1, 2), await _index.CountWithAllTenantsAsync());
        Assert.Equal((1, 2), await _index.CountWithAllTenantsAsync("TenantA"));
        Assert.Contains("TenantA", tenants);
        Assert.True(File.Exists(Path.Combine(_tempDir, "gallery", "TenantA", "index.json")));
    }
}
