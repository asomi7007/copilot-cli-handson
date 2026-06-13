using CommunityHub.Helpers;

namespace CommunityHub.Tests;

public class FileHelpersTests
{
    [Fact]
    public void IsSafeServedFilename_EmptyString_ReturnsFalse()
    {
        Assert.False(FileHelpers.IsSafeServedFilename("", ""));
    }

    [Fact]
    public void IsSafeServedFilename_EmptyFilename_ReturnsFalse()
    {
        // Empty string is rejected even when no extension is required
        Assert.False(FileHelpers.IsSafeServedFilename("", ".html"));
    }

    [Fact]
    public void IsSafeServedFilename_WithForwardSlash_ReturnsFalse()
    {
        Assert.False(FileHelpers.IsSafeServedFilename("a/b.png", ""));
    }

    [Fact]
    public void IsSafeServedFilename_WithBackslash_ReturnsFalse()
    {
        Assert.False(FileHelpers.IsSafeServedFilename("a\\b.png", ""));
    }

    [Fact]
    public void IsSafeServedFilename_WithDoubleDot_ReturnsFalse()
    {
        Assert.False(FileHelpers.IsSafeServedFilename("..secret.html", ".html"));
    }

    [Fact]
    public void IsSafeServedFilename_WrongExtension_ReturnsFalse()
    {
        Assert.False(FileHelpers.IsSafeServedFilename("game.txt", ".html"));
    }

    [Fact]
    public void IsSafeServedFilename_CorrectExtension_ReturnsTrue()
    {
        Assert.True(FileHelpers.IsSafeServedFilename("game.html", ".html"));
    }

    [Fact]
    public void IsSafeServedFilename_NoRequiredExtension_ReturnsTrue()
    {
        Assert.True(FileHelpers.IsSafeServedFilename("photo.png", ""));
    }

    [Fact]
    public void IsSafeServedFilename_NoRequiredExtension_NoExt_ReturnsTrue()
    {
        Assert.True(FileHelpers.IsSafeServedFilename("plainname", ""));
    }

    [Fact]
    public void IsSafeServedFilename_ExtensionCaseInsensitive_ReturnsTrue()
    {
        Assert.True(FileHelpers.IsSafeServedFilename("game.HTML", ".html"));
    }

    [Theory]
    [InlineData("My Game", "My Game")]
    [InlineData("  SpacePadded  ", "SpacePadded")]
    [InlineData("Game-Name_v1.0", "Game-Name_v1.0")]
    [InlineData("Game'; DROP TABLE gallery;--", "Game DROP TABLE gallery--")]
    [InlineData("<script>alert(1)</script>", "scriptalert1script")]
    [InlineData("../etc/passwd", "..etcpasswd")]
    [InlineData("  ", "")]
    [InlineData("", "")]
    public void SanitizeName_RemovesDisallowedCharacters(string input, string expected)
    {
        Assert.Equal(expected, FileHelpers.SanitizeName(input));
    }

    [Fact]
    public void GenerateRandomName_ReturnsTwoWordName()
    {
        var name = FileHelpers.GenerateRandomName();
        Assert.False(string.IsNullOrWhiteSpace(name));
        var parts = name.Split(' ');
        Assert.Equal(2, parts.Length);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p)));
    }

    [Fact]
    public void GenerateRandomName_UsesWordsFromPredefinedLists()
    {
        for (var i = 0; i < 50; i++)
        {
            var name = FileHelpers.GenerateRandomName();
            var parts = name.Split(' ');
            Assert.Equal(2, parts.Length);
            Assert.Contains(parts[0], FileHelpers.NameAdjectives);
            Assert.Contains(parts[1], FileHelpers.NameNouns);
        }
    }

    [Fact]
    public void GenerateRandomName_ContainsOnlyAllowedCharacters()
    {
        for (var i = 0; i < 50; i++)
        {
            var name = FileHelpers.GenerateRandomName();
            Assert.Equal(name, FileHelpers.SanitizeName(name));
        }
    }

    [Fact]
    public void GenerateGuid_ReturnsNonEmptyString()
    {
        var guid = FileHelpers.GenerateGuid();
        Assert.False(string.IsNullOrEmpty(guid));
        // D format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        Assert.Equal(36, guid.Length);
    }
}
