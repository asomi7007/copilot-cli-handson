using CommunityHub.Services;

namespace CommunityHub.Tests;

public class GalleryListLimitsTests
{
    [Fact]
    public void Normalize_NullLimit_ReturnsDefault()
    {
        Assert.Equal(GalleryListLimits.DefaultLimit, GalleryListLimits.Normalize(null));
    }

    [Fact]
    public void Normalize_ZeroLimit_ReturnsDefault()
    {
        Assert.Equal(GalleryListLimits.DefaultLimit, GalleryListLimits.Normalize(0));
    }

    [Fact]
    public void Normalize_NegativeLimit_ReturnsDefault()
    {
        Assert.Equal(GalleryListLimits.DefaultLimit, GalleryListLimits.Normalize(-5));
    }

    [Fact]
    public void Normalize_PositiveLimitBelowDefault_ReturnsThatValue()
    {
        Assert.Equal(50, GalleryListLimits.Normalize(50));
    }

    [Fact]
    public void Normalize_LimitEqualsDefault_ReturnsThatValue()
    {
        Assert.Equal(GalleryListLimits.DefaultLimit, GalleryListLimits.Normalize(GalleryListLimits.DefaultLimit));
    }

    [Fact]
    public void Normalize_LimitAboveDefault_ReturnsDefault()
    {
        Assert.Equal(GalleryListLimits.DefaultLimit, GalleryListLimits.Normalize(GalleryListLimits.DefaultLimit + 1));
    }

    [Fact]
    public void Normalize_LargeLimit_CapsAtDefault()
    {
        Assert.Equal(GalleryListLimits.DefaultLimit, GalleryListLimits.Normalize(99999));
    }

    [Fact]
    public void Normalize_LimitOfOne_ReturnsOne()
    {
        Assert.Equal(1, GalleryListLimits.Normalize(1));
    }
}
