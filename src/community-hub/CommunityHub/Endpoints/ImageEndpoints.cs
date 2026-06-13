using CommunityHub.Services;

namespace CommunityHub.Endpoints;

public static class ImageEndpoints
{
    public static WebApplication MapImageEndpoints(this WebApplication app)
    {
        app.MapPost("/api/image", async (HttpContext ctx, DashboardOperations dashboard) =>
        {
            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");
            if (ctx.Request.ContentLength > DashboardOperations.MaxImageBodySize)
                return Results.BadRequest("Image upload is too large");

            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files["image"];
            if (file is null)
                return Results.BadRequest("Failed to read image: missing 'image' field");

            using var stream = file.OpenReadStream();
            try
            {
                var filename = await dashboard.UploadImageAsync(file.FileName, file.ContentType, stream, ctx.Request.ContentLength, ctx.Request.Query["tenant"].ToString());
                return Results.Text($"Saved as {filename}\n");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        app.MapGet("/api/screenshots", async (HttpContext ctx, DashboardOperations dashboard) =>
        {
            var requestedTenant = ctx.Request.Query["tenant"].ToString();
            try
            {
                return Results.Json(await dashboard.ListScreenshotsAsync(requestedTenant));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        return app;
    }
}
