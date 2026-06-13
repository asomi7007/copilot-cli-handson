using CommunityHub.Services;

namespace CommunityHub.Endpoints;

public static class GalleryEndpoints
{
    private const int ReadBufferSize = 81920;

    public static WebApplication MapGalleryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/invaders-gallery", async (HttpContext ctx, DashboardOperations dashboard) =>
        {
            if (ctx.Request.ContentLength > DashboardOperations.MaxGalleryBodySize)
                return Results.BadRequest("Failed to read HTML body (max 200KB)");

            var htmlContent = await ReadBodyWithLimitAsync(ctx.Request.Body, DashboardOperations.MaxGalleryBodySize);
            if (htmlContent is null)
                return Results.BadRequest("Failed to read HTML body (max 200KB)");

            try
            {
                return Results.Json(await dashboard.ShareGameAsync(ctx.Request.Query["name"].ToString(), htmlContent, ctx.Request.Query["tenant"].ToString()));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        app.MapGet("/api/invaders-gallery/list", async (HttpContext ctx, DashboardOperations dashboard, int? limit) =>
        {
            var requestedTenant = ctx.Request.Query["tenant"].ToString();
            try
            {
                return Results.Json(await dashboard.ListGamesAsync(limit, requestedTenant));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        return app;
    }

    private static async Task<byte[]?> ReadBodyWithLimitAsync(Stream body, int maxBytes)
    {
        using var memStream = new MemoryStream(capacity: maxBytes);
        var buffer = new byte[ReadBufferSize];

        while (true)
        {
            var remaining = maxBytes - (int)memStream.Length;
            if (remaining <= 0)
            {
                // We have already read the maximum allowed bytes.
                // If there is any additional data, reject the request.
                var extraRead = await body.ReadAsync(buffer.AsMemory(0, 1));
                if (extraRead > 0)
                    return null;
                break;
            }

            var toRead = Math.Min(buffer.Length, remaining);
            var read = await body.ReadAsync(buffer.AsMemory(0, toRead));
            if (read == 0)
                break;

            await memStream.WriteAsync(buffer.AsMemory(0, read));
        }

        return memStream.ToArray();
    }
}
