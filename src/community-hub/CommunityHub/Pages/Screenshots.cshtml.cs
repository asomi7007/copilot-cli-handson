using CommunityHub.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CommunityHub.Pages;

public class ScreenshotsModel(IMetricsStore metrics, IBlobStore blobs) : PageModel
{
    public const string CopilotIcon = ActivityModel.CopilotIcon;

    public List<string> Items { get; set; } = [];
    public List<string> TenantOptions { get; set; } = [];
    public string SelectedTenant { get; set; } = "";

    public async Task OnGetAsync(string? tenant)
    {
        SelectedTenant = TenantHelpers.IsValidTenant(tenant)
            ? TenantHelpers.ResolveTenant(tenant, metrics.CurrentTenant)
            : metrics.CurrentTenant;
        TenantOptions = TenantHelpers.MergeTenants(await metrics.ListTenantsAsync(), await blobs.ListTenantsAsync(), [SelectedTenant]);
        Items = await blobs.ListScreenshotsAsync(SelectedTenant);
    }
}
