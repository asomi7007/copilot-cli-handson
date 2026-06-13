using CommunityHub.Services;

namespace CommunityHub.Tests;

public class LocalDataDirectoriesTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "localdatatest_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void EnsureExists_CreatesLocalDataDirectories()
    {
        LocalDataDirectories.EnsureExists(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "screenshots")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "gallery")));
    }
}