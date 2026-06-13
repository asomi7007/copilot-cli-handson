using CommunityHub.Config;
using CommunityHub.Helpers;
using CommunityHub.Models;

namespace CommunityHub.Services;

public sealed class DashboardOperations(IBlobStore blobs, IGalleryIndex gallery, IMetricsStore metrics, AppConfig config)
{
    public const int MaxGalleryBodySize = 200 * 1024;
    public const long MaxImageBodySize = 10 * 1024 * 1024;

    public async Task<GalleryListItem> ShareGameAsync(string? name, byte[] htmlContent, string? requestedTenant = null)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));

        var sanitizedName = FileHelpers.SanitizeName(name);
        if (string.IsNullOrEmpty(sanitizedName))
            sanitizedName = FileHelpers.GenerateRandomName();

        if (htmlContent.Length == 0)
            throw new ArgumentException("HTML body is empty", nameof(htmlContent));
        if (htmlContent.Length > MaxGalleryBodySize)
            throw new ArgumentException("Failed to read HTML body (max 200KB)", nameof(htmlContent));

        var filename = FileHelpers.GenerateGuid() + ".html";
        var tenant = TenantHelpers.ResolveTenant(requestedTenant, gallery.CurrentTenant);
        var url = await blobs.PutGalleryHtmlAsync(filename, htmlContent, tenant);

        await gallery.AddAsync(new GalleryEntry { Name = sanitizedName, Filename = filename, Url = url }, tenant);

        return new GalleryListItem { Name = sanitizedName, Url = url };
    }

    public async Task<List<GalleryListItem>> ListGamesAsync(int? limit = null, string? requestedTenant = null)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));

        var tenant = TenantHelpers.ResolveTenant(requestedTenant, gallery.CurrentTenant);
        var entries = await gallery.ListAsync(GalleryListLimits.Normalize(limit), tenant);
        return entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && !string.IsNullOrEmpty(e.Filename))
            .Select(e => new GalleryListItem
            {
                Name = e.Name,
                Url = string.IsNullOrEmpty(e.Url) ? blobs.ServeGalleryHtml(e.Filename, tenant) : e.Url
            })
            .ToList();
    }

    public async Task<string> UploadImageAsync(string? filename, string? contentType, Stream data, long? contentLength = null, string? requestedTenant = null)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));
        if (contentLength > MaxImageBodySize)
            throw new ArgumentException("Image upload is too large", nameof(contentLength));

        var extension = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(extension))
            extension = ".png";

        var storedFilename = FileHelpers.GenerateGuid() + extension;
        var tenant = TenantHelpers.ResolveTenant(requestedTenant, blobs.CurrentTenant);
        await blobs.PutScreenshotAsync(storedFilename, data, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, tenant);

        return storedFilename;
    }

    public async Task<string> UploadImageAsync(string? filename, string? contentType, byte[] data, string? requestedTenant = null)
    {
        if (data.Length == 0)
            throw new ArgumentException("Image body is empty", nameof(data));
        if (data.Length > MaxImageBodySize)
            throw new ArgumentException("Image upload is too large", nameof(data));

        await using var stream = new MemoryStream(data);
        return await UploadImageAsync(filename, contentType, stream, data.Length, requestedTenant);
    }

    public async Task<List<string>> ListScreenshotsAsync(string? requestedTenant = null)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));

        var tenant = TenantHelpers.ResolveTenant(requestedTenant, blobs.CurrentTenant);
        return await blobs.ListScreenshotsAsync(tenant);
    }

    public async Task<ActivityApiView> GetActivityAsync(string? requestedTenant = null)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));

        var (snap, allTenants) = await metrics.SnapshotWithAllTenantsAsync(requestedTenant);
        var selectedTenant = TenantHelpers.ResolveTenant(requestedTenant, metrics.CurrentTenant);
        var galleryCounts = await gallery.CountWithAllTenantsAsync(selectedTenant);
        snap.UploadedGames = galleryCounts.Tenant;
        allTenants.UploadedGames = galleryCounts.AllTenants;

        return new ActivityApiView
        {
            Tenant = selectedTenant,
            Activity = new ActivityScopesView
            {
                CurrentTenant = ToCountsView(snap),
                AllTenants = ToCountsView(allTenants)
            },
            Tools = new ToolBreakdownScopesView
            {
                CurrentTenant = snap.ToolBreakdown,
                AllTenants = allTenants.ToolBreakdown
            }
        };
    }

    public async Task<List<ToolCount>> ListToolUsageAsync(string? requestedTenant = null)
    {
        if (!TenantHelpers.IsValidTenant(requestedTenant))
            throw new ArgumentException("Invalid tenant", nameof(requestedTenant));

        var (snap, _) = await metrics.SnapshotWithAllTenantsAsync(requestedTenant);
        return snap.ToolBreakdown;
    }

    public async Task<TenantsApiView> ListTenantsAsync()
    {
        var tenants = TenantHelpers.MergeTenants(await metrics.ListTenantsAsync(), await gallery.ListTenantsAsync(), await blobs.ListTenantsAsync());
        return new TenantsApiView { CurrentTenant = metrics.CurrentTenant, Tenants = tenants };
    }

    public ScreenshotUploadInstructions GetScreenshotUploadInstructions(string? imagePath = null)
    {
        var uploadUrl = CombineUrl(config.DashboardBaseUrl, "/api/image");
        var pathPlaceholder = string.IsNullOrWhiteSpace(imagePath) ? "<ABSOLUTE_IMAGE_PATH>" : imagePath;
        var curlPath = ShellSingleQuote(pathPlaceholder);
        var curlUrl = ShellSingleQuote(uploadUrl);

        return new ScreenshotUploadInstructions
        {
            UploadUrl = uploadUrl,
            ExampleCommand = $"curl -f -sS -X POST -F image=@{curlPath} {curlUrl}",
            Instructions = "Upload the screenshot outside the MCP tool context by sending a multipart/form-data POST directly to upload_url with the local screenshot file in the 'image' field. Do not read, open, base64-encode, summarize, or pass the image bytes to any MCP tool; upload the file without opening it. The example_command is only a suggestion; any equivalent direct HTTP upload method is fine."
        };
    }

    public ShareGameInstructions GetShareGameInstructions(string? htmlPath = null, string? name = null)
    {
        var uploadUrl = CombineUrl(config.DashboardBaseUrl, "/api/invaders-gallery");
        if (!string.IsNullOrWhiteSpace(name))
            uploadUrl = $"{uploadUrl}?name={Uri.EscapeDataString(name)}";

        var pathPlaceholder = string.IsNullOrWhiteSpace(htmlPath) ? "<ABSOLUTE_HTML_PATH>" : htmlPath;
        var curlPath = ShellSingleQuote(pathPlaceholder);
        var curlUrl = ShellSingleQuote(uploadUrl);

        return new ShareGameInstructions
        {
            UploadUrl = uploadUrl,
            ExampleCommand = $"curl -f -sS -X POST -H 'Content-Type: text/html; charset=utf-8' --data-binary @{curlPath} {curlUrl}",
            Instructions = "Share the game outside the MCP tool context by sending a direct HTTP POST to upload_url with the local HTML file as the request body and content_type as the Content-Type header. If the user provided a file path, pass that path to this tool and run the example_command or equivalent direct upload. Do not read, open, inspect, encode, summarize, or pass the HTML file contents through MCP tool arguments. The example_command is only a suggestion; any equivalent direct HTTP upload method is fine."
        };
    }

    private static ActivityCountsView ToCountsView(ActivitySnapshot snap) => new()
    {
        SessionCount = snap.SessionCount,
        UserCount = snap.UserCount,
        PromptSubmissions = snap.PromptSubmissions,
        ToolCalls = snap.ToolCalls,
        DistinctToolsCalled = snap.ToolBreakdown.Count,
        AgentStops = snap.AgentStops,
        SubagentStops = snap.SubagentStops,
        UploadedGamesCount = snap.UploadedGames
    };

    private static string CombineUrl(string baseUrl, string path) =>
        string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string ShellSingleQuote(string value) => $"'{value.Replace("'", "'\\''")}'";
}
