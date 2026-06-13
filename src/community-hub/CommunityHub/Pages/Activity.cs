using CommunityHub.Models;
using CommunityHub.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CommunityHub.Pages;

public class ActivityModel(IMetricsStore metrics, IBlobStore blobs, IGalleryIndex gallery) : PageModel
{
    public const int ActivityColumnCount = 3;
    public const string CopilotIcon = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M23.922 16.992c-.861 1.495-5.859 5.023-11.922 5.023-6.063 0-11.061-3.528-11.922-5.023A.641.641 0 0 1 0 16.736v-2.869a.841.841 0 0 1 .053-.22c.372-.935 1.347-2.292 2.605-2.656.167-.429.414-1.055.644-1.517a10.198 10.198 0 0 1-.052-1.086c0-1.331.282-2.499 1.132-3.368.397-.406.89-.717 1.474-.952C5.357 1.952 7.35.995 10.102.995c2.756 0 4.748.957 6.148 2.093.584.235 1.077.546 1.474.952.85.869 1.132 2.037 1.132 3.368 0 .368-.014.733-.052 1.086.23.462.477 1.088.644 1.517 1.258.364 2.233 1.721 2.605 2.656a.832.832 0 0 1 .053.22v2.869a.641.641 0 0 1-.078.305ZM12.172 11h-.344a4.323 4.323 0 0 1-.355.508C10.703 12.455 9.555 13 7.965 13c-1.725 0-2.989-.359-3.782-1.259a2.005 2.005 0 0 1-.085-.104L4 11.741v6.585c1.435.779 4.514 2.179 8 2.179 3.486 0 6.565-1.4 8-2.179v-6.585l-.098-.104s-.033.045-.085.104c-.793.9-2.057 1.259-3.782 1.259-1.59 0-2.738-.545-3.508-1.492a4.323 4.323 0 0 1-.355-.508h-.016Zm.641-2.935c.136 1.057.403 1.913.878 2.497.442.544 1.134.938 2.344.938 1.573 0 2.292-.337 2.657-.751.384-.435.558-1.15.558-2.361 0-1.14-.243-1.847-.59-2.291-.348-.444-.881-.681-1.617-.681-1.281 0-2.014.487-2.485 1.123-.475.642-.764 1.539-.964 2.629ZM5.5 5.416c-.736 0-1.269.237-1.617.681-.347.444-.59 1.151-.59 2.291 0 1.211.174 1.926.558 2.361.365.414 1.084.751 2.657.751 1.21 0 1.902-.394 2.344-.938.475-.584.742-1.44.878-2.497-.2-1.09-.489-1.987-.964-2.629-.471-.636-1.204-1.123-2.485-1.123Z"/><path d="M14.5 14.25a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1Zm-5 0a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1Z"/></svg>""";

    public ActivitySnapshot Snapshot { get; set; } = new();
    public ActivitySnapshot AllTenantsSnapshot { get; set; } = new();
    public List<string> Screenshots { get; set; } = [];
    public List<string> TenantOptions { get; set; } = [];
    public string SelectedTenant { get; set; } = "";

    public async Task OnGetAsync(string? tenant)
    {
        SelectedTenant = TenantHelpers.IsValidTenant(tenant)
            ? TenantHelpers.ResolveTenant(tenant, metrics.CurrentTenant)
            : metrics.CurrentTenant;
        TenantOptions = TenantHelpers.MergeTenants(await metrics.ListTenantsAsync(), await gallery.ListTenantsAsync(), await blobs.ListTenantsAsync(), [SelectedTenant]);

        Snapshot = await metrics.SnapshotAsync(SelectedTenant);
        AllTenantsSnapshot = await metrics.AllTenantsSnapshotAsync();
        var galleryCounts = await gallery.CountWithAllTenantsAsync(SelectedTenant);
        Snapshot.UploadedGames = galleryCounts.Tenant;
        AllTenantsSnapshot.UploadedGames = galleryCounts.AllTenants;
        Snapshot.DistinctToolsCalled = Snapshot.ToolBreakdown.Count;
        AllTenantsSnapshot.DistinctToolsCalled = AllTenantsSnapshot.ToolBreakdown.Count;

        var names = await blobs.ListScreenshotsAsync(SelectedTenant);
        Screenshots = names;
    }
}
