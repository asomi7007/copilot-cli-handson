namespace CommunityHub.Services;

public static class GalleryListLimits
{
    public const int DefaultLimit = 100;

    public static int Normalize(int? limit) =>
        limit is > 0 ? Math.Min(limit.Value, DefaultLimit) : DefaultLimit;
}
