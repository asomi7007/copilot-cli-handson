using System.Text.RegularExpressions;

namespace CommunityHub.Services;

public static partial class TenantHelpers
{
    public static bool IsValidTenant(string? tenant) =>
        string.IsNullOrWhiteSpace(tenant) || TenantRegex().IsMatch(tenant);

    public static string ResolveTenant(string? requestedTenant, string currentTenant) =>
        string.IsNullOrWhiteSpace(requestedTenant) ? currentTenant : requestedTenant;

    public static List<string> MergeTenants(params IEnumerable<string>[] tenantLists) =>
        tenantLists
            .SelectMany(t => t)
            .Where(t => !string.IsNullOrWhiteSpace(t) && IsValidTenant(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$")]
    private static partial Regex TenantRegex();
}
