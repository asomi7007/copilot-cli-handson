using System.Text.RegularExpressions;

namespace CommunityHub.Helpers;

internal static partial class FileHelpers
{
    [GeneratedRegex(@"[^a-zA-Z0-9 _\-.]")]
    private static partial Regex DisallowedNameChars();

    public static string SanitizeName(string? name) =>
        name is null ? "" : DisallowedNameChars().Replace(name, "").Trim();

    internal static readonly string[] NameAdjectives =
    [
        "Cosmic", "Stellar", "Brave", "Swift", "Nimble", "Laser", "Turbo", "Pixel",
        "Neon", "Galactic", "Atomic", "Shadow", "Hyper", "Fuzzy", "Lucky", "Warp",
        "Solar", "Binary", "Speedy", "Glowing"
    ];

    internal static readonly string[] NameNouns =
    [
        "Invader", "Blaster", "Raider", "Falcon", "Comet", "Ranger", "Drifter", "Rocket",
        "Probe", "Striker", "Wanderer", "Pilot", "Cruiser", "Phoenix", "Pulsar", "Voyager",
        "Nebula", "Sparrow", "Cannon", "Aurora"
    ];

    public static string GenerateRandomName()
    {
        var adj = NameAdjectives[Random.Shared.Next(NameAdjectives.Length)];
        var noun = NameNouns[Random.Shared.Next(NameNouns.Length)];
        return $"{adj} {noun}";
    }

    public static string GenerateGuid() => Guid.NewGuid().ToString("D");

    public static bool IsSafeServedFilename(string name, string requiredExt)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Contains('/') || name.Contains('\\')) return false;
        if (Path.GetFileName(name) != name) return false;
        if (!string.IsNullOrEmpty(requiredExt) &&
            !name.EndsWith(requiredExt, StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Contains("..")) return false;
        return true;
    }
}
