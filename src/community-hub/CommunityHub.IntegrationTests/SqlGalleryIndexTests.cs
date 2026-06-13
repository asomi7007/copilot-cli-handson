using CommunityHub.Models;
using CommunityHub.Services;

namespace CommunityHub.IntegrationTests;

[Collection("Sql")]
public class SqlGalleryIndexTests(SqlFixture fixture)
{
    // Each test gets a fresh tenant so rows from one test don't affect another.
    private SqlGalleryIndex NewGallery() => new(fixture.ConnectionString, UniqueId());
    private static string UniqueId() => "g" + Guid.NewGuid().ToString("N")[..7];

    [Fact]
    public async Task Count_Empty_ReturnsZero()
    {
        var gallery = NewGallery();
        Assert.Equal(0, await gallery.CountAsync());
    }

    [Fact]
    public async Task List_Empty_ReturnsEmptyList()
    {
        var gallery = NewGallery();
        Assert.Empty(await gallery.ListAsync());
    }

    [Fact]
    public async Task Add_ThenCount_ReturnsCorrectCount()
    {
        var gallery = NewGallery();

        await gallery.AddAsync(new GalleryEntry { Name = "A", Filename = "a.html", Url = "https://example.com/a.html" });
        await gallery.AddAsync(new GalleryEntry { Name = "B", Filename = "b.html", Url = "https://example.com/b.html" });

        Assert.Equal(2, await gallery.CountAsync());
    }

    [Fact]
    public async Task Add_ThenList_ReturnsEntries()
    {
        var gallery = NewGallery();

        var entry1 = new GalleryEntry { Name = "Game 1", Filename = "file1.html", Url = "https://blob.example.com/file1.html" };
        var entry2 = new GalleryEntry { Name = "Game 2", Filename = "file2.html", Url = "https://blob.example.com/file2.html" };

        await gallery.AddAsync(entry1);
        await gallery.AddAsync(entry2);

        var entries = await gallery.ListAsync();
        Assert.Equal(2, entries.Count);
        // Ordered by id DESC (latest first)
        Assert.Equal("Game 2", entries[0].Name);
        Assert.Equal("Game 1", entries[1].Name);
    }

    [Fact]
    public async Task Add_ThenList_PreservesUrl()
    {
        var gallery = NewGallery();
        const string url = "https://blob.example.com/test.html";

        await gallery.AddAsync(new GalleryEntry { Name = "Test", Filename = "test.html", Url = url });

        var entries = await gallery.ListAsync();
        Assert.Single(entries);
        Assert.Equal(url, entries[0].Url);
    }

    [Fact]
    public async Task List_WithLimit_ReturnsLatestEntries()
    {
        var gallery = NewGallery();

        await gallery.AddAsync(new GalleryEntry { Name = "Game 1", Filename = "f1.html", Url = "https://blob/f1.html" });
        await gallery.AddAsync(new GalleryEntry { Name = "Game 2", Filename = "f2.html", Url = "https://blob/f2.html" });
        await gallery.AddAsync(new GalleryEntry { Name = "Game 3", Filename = "f3.html", Url = "https://blob/f3.html" });

        var entries = await gallery.ListAsync(limit: 2);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Game 3", entries[0].Name);
        Assert.Equal("Game 2", entries[1].Name);
    }

    [Fact]
    public async Task List_WithNullLimit_ReturnsAllEntries()
    {
        var gallery = NewGallery();

        for (var i = 1; i <= 5; i++)
            await gallery.AddAsync(new GalleryEntry { Name = $"Game {i}", Filename = $"g{i}.html", Url = $"https://blob/g{i}.html" });

        var entries = await gallery.ListAsync(limit: null);
        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public async Task TenantIsolation_GalleryAreSeparate()
    {
        var galleryA = NewGallery();
        var galleryB = NewGallery();

        await galleryA.AddAsync(new GalleryEntry { Name = "A-Only", Filename = "a.html", Url = "https://blob/a.html" });

        Assert.Equal(1, await galleryA.CountAsync());
        Assert.Equal(0, await galleryB.CountAsync());
        Assert.Empty(await galleryB.ListAsync());
    }

    [Fact]
    public async Task List_TenantLookupIsCaseInsensitive()
    {
        var lowerTenant = UniqueId();
        var upperTenant = lowerTenant.ToUpperInvariant();

        var galleryLower = new SqlGalleryIndex(fixture.ConnectionString, lowerTenant);
        await galleryLower.AddAsync(new GalleryEntry { Name = "CaseTest", Filename = "ct.html", Url = "https://blob/ct.html" });

        // Query using the uppercase variant – SQL Server CI collation should match the rows.
        var galleryUpper = new SqlGalleryIndex(fixture.ConnectionString, upperTenant);
        var entries = await galleryUpper.ListAsync();

        Assert.Single(entries);
        Assert.Equal("CaseTest", entries[0].Name);
    }

    [Fact]
    public async Task Count_TenantLookupIsCaseInsensitive()
    {
        var lowerTenant = UniqueId();
        var upperTenant = lowerTenant.ToUpperInvariant();

        var galleryLower = new SqlGalleryIndex(fixture.ConnectionString, lowerTenant);
        await galleryLower.AddAsync(new GalleryEntry { Name = "Game A", Filename = "a.html", Url = "https://blob/a.html" });
        await galleryLower.AddAsync(new GalleryEntry { Name = "Game B", Filename = "b.html", Url = "https://blob/b.html" });

        var galleryUpper = new SqlGalleryIndex(fixture.ConnectionString, upperTenant);
        Assert.Equal(2, await galleryUpper.CountAsync());
    }

    [Fact]
    public async Task ListTenants_WithCaseMismatchInDb_NoDuplicateCurrentTenant()
    {
        var lowerTenant = UniqueId();
        var upperTenant = lowerTenant.ToUpperInvariant();
        var otherTenant = UniqueId();

        // Write data under the lowercase tenant name.
        var galleryLower = new SqlGalleryIndex(fixture.ConnectionString, lowerTenant);
        await galleryLower.AddAsync(new GalleryEntry { Name = "CaseTest", Filename = "ct.html", Url = "https://blob/ct.html" });

        // Write data under a second distinct tenant to verify DB rows are actually read.
        var galleryOther = new SqlGalleryIndex(fixture.ConnectionString, otherTenant);
        await galleryOther.AddAsync(new GalleryEntry { Name = "OtherTenant", Filename = "ot.html", Url = "https://blob/ot.html" });

        // List tenants from the uppercase perspective.
        var galleryUpper = new SqlGalleryIndex(fixture.ConnectionString, upperTenant);
        var tenants = await galleryUpper.ListTenantsAsync();

        // The logical tenant must appear exactly once (no case-variant duplicate).
        var matchCount = tenants.Count(t => string.Equals(t, lowerTenant, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, matchCount);

        // The current tenant (uppercase) must be first in the list.
        Assert.Equal(upperTenant, tenants[0]);

        // The second distinct tenant must also be present, proving DB rows were actually read.
        Assert.Contains(otherTenant, tenants);
    }
}
