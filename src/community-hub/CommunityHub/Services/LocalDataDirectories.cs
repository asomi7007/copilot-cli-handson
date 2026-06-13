namespace CommunityHub.Services;

public static class LocalDataDirectories
{
    public static string ScreenshotsPath(string dataDir) => Path.Combine(dataDir, "screenshots");

    public static string GalleryPath(string dataDir) => Path.Combine(dataDir, "gallery");

    public static void EnsureExists(string dataDir)
    {
        Directory.CreateDirectory(ScreenshotsPath(dataDir));
        Directory.CreateDirectory(GalleryPath(dataDir));
    }
}