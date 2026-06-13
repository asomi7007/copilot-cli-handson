using System.ComponentModel;
using CommunityHub.Models;
using ModelContextProtocol.Server;

namespace CommunityHub.Services;

[McpServerToolType]
public static class DashboardMcpTools
{
    [McpServerTool, Description("Return the list of uploaded Space Invaders games with their names and URLs.")]
    public static Task<List<GalleryListItem>> ListGames(
        DashboardOperations dashboard,
        [Description("Optional maximum number of games to return.")] int? limit = null,
        [Description("Optional tenant name. Omit for the current tenant.")] string? tenant = null) =>
        dashboard.ListGamesAsync(limit, tenant);

    [McpServerTool, Description("Return the list of uploaded screenshot URLs.")]
    public static Task<List<string>> ListScreenshots(
        DashboardOperations dashboard,
        [Description("Optional tenant name. Omit for the current tenant.")] string? tenant = null) =>
        dashboard.ListScreenshotsAsync(tenant);

    [McpServerTool, Description("Return the Lab 502 Community Hub activity for the requested tenant and all tenants.")]
    public static Task<ActivityApiView> GetCommunityActivity(
        DashboardOperations dashboard,
        [Description("Optional tenant name. Omit for the current tenant.")] string? tenant = null) =>
        dashboard.GetActivityAsync(tenant);

    [McpServerTool, Description("Return the per-tool usage counts for the current or requested tenant.")]
    public static Task<List<ToolCount>> ListToolUsage(
        DashboardOperations dashboard,
        [Description("Optional tenant name. Omit for the current tenant.")] string? tenant = null) =>
        dashboard.ListToolUsageAsync(tenant);

    [McpServerTool, Description("Return the current tenant and the list of known Lab 502 Community Hub tenants.")]
    public static Task<TenantsApiView> ListTenants(DashboardOperations dashboard) =>
        dashboard.ListTenantsAsync();

    [McpServerTool, Description("Return direct share-game upload details for a local HTML file path. Pass the file path as htmlPath without reading the file; this tool does not accept HTML content.")]
    public static ShareGameInstructions GetShareGameInstructions(
        DashboardOperations dashboard,
        [Description("Optional absolute local path to the HTML game file. Pass the path exactly as provided by the user; do not read, inspect, encode, or summarize the file.")] string? htmlPath = null,
        [Description("Optional display name for the shared game. When provided, the upload URL includes it as the name query parameter.")] string? name = null) =>
        dashboard.GetShareGameInstructions(htmlPath, name);

    [McpServerTool, Description("Return direct screenshot upload details. Use this for screenshots; do not read, encode, or pass image bytes through MCP.")]
    public static ScreenshotUploadInstructions GetScreenshotUploadInstructions(
        DashboardOperations dashboard,
        [Description("Optional absolute local path to the screenshot file. When provided, the example command includes it.")] string? imagePath = null) =>
        dashboard.GetScreenshotUploadInstructions(imagePath);
}
