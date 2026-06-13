using CommunityHub.Services;

namespace CommunityHub.Endpoints;

public static class MetricsEndpoints
{
    public static WebApplication MapMetricsEndpoints(this WebApplication app, string dashboardBaseUrl)
    {
        app.MapGet("/api/activity", async (HttpContext ctx, DashboardOperations dashboard) =>
        {
            var requestedTenant = ctx.Request.Query["tenant"].ToString();
            try
            {
                return Results.Json(await dashboard.GetActivityAsync(requestedTenant));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        app.MapGet("/api/tenants", async (DashboardOperations dashboard) =>
            Results.Json(await dashboard.ListTenantsAsync()));

        app.MapGet("/api/openapi.json", () => Results.Json(OpenApiSpecBuilder.Build(dashboardBaseUrl)));

        return app;
    }
}
