using CommunityHub.Config;
using CommunityHub.Endpoints;
using CommunityHub.Extensions;
using CommunityHub.Services;
using ModelContextProtocol.AspNetCore;

var cfg = AppConfig.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on the configured port
builder.WebHost.UseUrls($"http://0.0.0.0:{cfg.Port}");

builder.Services.AddRazorPages();
builder.AddAppInsights(cfg);
await builder.AddDashboardServicesAsync(cfg);
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapEventEndpoints();
app.MapImageEndpoints();
app.MapLocalFileEndpoints();
app.MapGalleryEndpoints();
app.MapMetricsEndpoints(cfg.DashboardBaseUrl);
app.MapMcp("/mcp");

Console.WriteLine($"Lab 502 Community Hub listening on :{cfg.Port} (mode={cfg.Mode.ToString().ToLower()})");
app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
