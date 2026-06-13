using CommunityHub.Models;

namespace CommunityHub.Services;

public interface IMetricsStore
{
    string CurrentTenant { get; }
    Task OnSessionStartAsync(string sessionId, string userId);
    Task OnPromptSubmittedAsync();
    Task OnToolUsedAsync(string tool);
    Task OnAgentStopAsync();
    Task OnSubagentStopAsync();
    Task<ActivitySnapshot> SnapshotAsync(string? tenant = null);
    Task<ActivitySnapshot> AllTenantsSnapshotAsync();
    Task<(ActivitySnapshot Tenant, ActivitySnapshot AllTenants)> SnapshotWithAllTenantsAsync(string? tenant = null);
    Task<List<string>> ListTenantsAsync();
}

public interface IGalleryIndex
{
    string CurrentTenant { get; }
    Task AddAsync(GalleryEntry entry, string? tenant = null);
    Task<List<GalleryEntry>> ListAsync(int? limit = null, string? tenant = null);
    Task<int> CountAsync(string? tenant = null);
    Task<(int Tenant, int AllTenants)> CountWithAllTenantsAsync(string? tenant = null);
    Task<List<string>> ListTenantsAsync();
}

public interface IBlobStore
{
    string CurrentTenant { get; }
    Task<string> PutScreenshotAsync(string name, Stream data, string contentType, string? tenant = null);
    Task<string> PutGalleryHtmlAsync(string name, byte[] data, string? tenant = null);
    Task<List<string>> ListScreenshotsAsync(string? tenant = null);
    Task<List<string>> ListTenantsAsync();
    string ServeGalleryHtml(string name, string? tenant = null);
    bool IsLocalServing { get; }
}
