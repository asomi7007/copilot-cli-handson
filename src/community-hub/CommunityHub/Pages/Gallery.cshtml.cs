using CommunityHub.Models;
using CommunityHub.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CommunityHub.Pages;

public class GalleryModel(IGalleryIndex gallery, IBlobStore blobs) : PageModel
{
    public const string CopilotIcon = ActivityModel.CopilotIcon;

    public List<GalleryListItem> Items { get; set; } = [];
    public List<string> TenantOptions { get; set; } = [];
    public string SelectedTenant { get; set; } = "";
    public int? Limit { get; set; }

    public async Task OnGetAsync(int? limit, string? tenant)
    {
        Limit = limit;
        SelectedTenant = TenantHelpers.IsValidTenant(tenant)
            ? TenantHelpers.ResolveTenant(tenant, gallery.CurrentTenant)
            : gallery.CurrentTenant;
        TenantOptions = TenantHelpers.MergeTenants(await gallery.ListTenantsAsync(), [SelectedTenant]);

        var entries = await gallery.ListAsync(GalleryListLimits.Normalize(limit), SelectedTenant);
        Items = entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && !string.IsNullOrEmpty(e.Filename))
            .Select(e => new GalleryListItem
            {
                Name = e.Name,
                Url = string.IsNullOrEmpty(e.Url) ? blobs.ServeGalleryHtml(e.Filename, SelectedTenant) : e.Url
            })
            .ToList();
    }
}
