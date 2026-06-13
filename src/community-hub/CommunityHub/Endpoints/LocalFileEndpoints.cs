using CommunityHub.Helpers;
using CommunityHub.Services;

namespace CommunityHub.Endpoints;

public static class LocalFileEndpoints
{
    public static WebApplication MapLocalFileEndpoints(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IBlobStore>().IsLocalServing)
            return app;

        var localBlobStore = (LocalBlobStore)app.Services.GetRequiredService<IBlobStore>();

        app.MapGet("/api/screenshots/{name}", (string name) => ServeLocalFile(localBlobStore.ScreenshotsDir, name));
        app.MapGet("/api/screenshots/{tenant}/{name}", (string tenant, string name) => ServeLocalFile(localBlobStore.ScreenshotsDir, name, tenant: tenant));

        app.MapGet("/api/gallery/{name}", (string name) => ServeLocalFile(localBlobStore.GalleryDir, name, ".html", "text/html"));
        app.MapGet("/api/gallery/{tenant}/{name}", (string tenant, string name) => ServeLocalFile(localBlobStore.GalleryDir, name, ".html", "text/html", tenant));

        return app;
    }

    private static IResult ServeLocalFile(string directory, string name, string requiredExt = "", string? contentType = null, string? tenant = null)
    {
        if (!TenantHelpers.IsValidTenant(tenant))
            return Results.NotFound();
        if (!FileHelpers.IsSafeServedFilename(name, requiredExt))
            return Results.NotFound();

        var fullPath = string.IsNullOrWhiteSpace(tenant) || string.Equals(tenant, "local", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(directory, name)
            : Path.Combine(directory, tenant, name);
        if (!File.Exists(fullPath))
            return Results.NotFound();

        try
        {
            return Results.File(File.OpenRead(fullPath), contentType);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
