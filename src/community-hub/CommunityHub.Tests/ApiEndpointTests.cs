using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityHub.Models;
using CommunityHub.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityHub.Tests;

public class ApiEndpointTests : IClassFixture<ApiEndpointTests.TestApp>, IDisposable
{
    private readonly HttpClient _client;
    private readonly TestApp _factory;

    public class TestApp : WebApplicationFactory<Program>
    {
        public string TempDir { get; }

        public TestApp()
        {
            TempDir = Path.Combine(Path.GetTempPath(), "apitest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDir);
            // Set env vars before building host
            Environment.SetEnvironmentVariable("APP_MODE", "local");
            Environment.SetEnvironmentVariable("LOCAL_DATA_DIR", TempDir);
            Environment.SetEnvironmentVariable("PORT", "0"); // let system pick
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseUrls("http://127.0.0.1:0");
            builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CommunityHub"));
        }
    }

    public ApiEndpointTests(TestApp factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task RobotsTxt_DisallowsAllAgents()
    {
        var resp = await _client.GetAsync("/robots.txt");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var text = await resp.Content.ReadAsStringAsync();
        Assert.Contains("User-agent: *", text);
        Assert.Contains("Disallow: /", text);
    }

    [Fact]
    public async Task SessionStart_ReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/session_start?session_id=s1&user_info=u1", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task UserPromptSubmitted_ReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/user_prompt_submitted", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task UserPromptSubmitted_IncrementsActivityCounter()
    {
        await _client.PostAsync("/api/event/user_prompt_submitted?session_id=s1", null);
        await _client.PostAsync("/api/event/user_prompt_submitted?session_id=s1", null);

        var resp = await _client.GetAsync("/api/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<ActivityApiView>();
        Assert.NotNull(json);
        Assert.True(json!.Activity.CurrentTenant.PromptSubmissions >= 2);
        Assert.True(json.Activity.AllTenants.PromptSubmissions >= json.Activity.CurrentTenant.PromptSubmissions);
    }

    [Fact]
    public async Task ToolUsed_ReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/tool_used?tool_name=my-tool", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Theory]
    [InlineData("/api/event/tool_used")]
    [InlineData("/api/event/tool_used?tool=wrong-param")]
    [InlineData("/api/event/tool_used?tool_name=")]
    public async Task ToolUsed_WithoutToolName_ReturnsBadRequest(string url)
    {
        var resp = await _client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SubagentStop_ReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/subagent_stop", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AgentStop_ReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/agent_stop", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AgentStop_IncrementsActivityCounter()
    {
        await _client.PostAsync("/api/event/agent_stop?session_id=s1", null);
        await _client.PostAsync("/api/event/agent_stop?session_id=s1", null);

        var resp = await _client.GetAsync("/api/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<ActivityApiView>();
        Assert.NotNull(json);
        Assert.True(json!.Activity.CurrentTenant.AgentStops >= 2);
        Assert.True(json.Activity.AllTenants.AgentStops >= json.Activity.CurrentTenant.AgentStops);
    }

    [Fact]
    public async Task Activity_ReturnsJson()
    {
        // Record some counters first
        await _client.PostAsync("/api/event/session_start?session_id=s1&user_info=u1", null);
        await _client.PostAsync("/api/event/user_prompt_submitted?session_id=s1", null);
        await _client.PostAsync("/api/event/tool_used?tool_name=test-tool", null);
        await _client.PostAsync("/api/event/agent_stop?session_id=s1", null);

        var resp = await _client.GetAsync("/api/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"activity\"", body);
        Assert.DoesNotContain("\"metrics\"", body);

        var json = JsonSerializer.Deserialize<ActivityApiView>(body);
        Assert.NotNull(json);
        Assert.Equal("local", json!.Tenant);
        Assert.True(json.Activity.CurrentTenant.SessionCount >= 1);
        Assert.True(json.Activity.CurrentTenant.PromptSubmissions >= 1);
        Assert.True(json.Activity.CurrentTenant.ToolCalls >= 1);
        Assert.True(json.Activity.CurrentTenant.DistinctToolsCalled >= 1);
        Assert.True(json.Activity.CurrentTenant.AgentStops >= 1);
        Assert.Contains(json.Tools.CurrentTenant, tool => tool.Name == "test-tool" && tool.Count >= 1);
    }

    [Fact]
    public async Task Activity_UploadedGamesCount_ComesFromGalleryRows()
    {
        var firstHtml = new StringContent("<html><body>game one</body></html>");
        firstHtml.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        var secondHtml = new StringContent("<html><body>game two</body></html>");
        secondHtml.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/api/invaders-gallery?name=GameOne", firstHtml)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/api/invaders-gallery?name=GameTwo", secondHtml)).StatusCode);

        var resp = await _client.GetAsync("/api/activity?tenant=local");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<ActivityApiView>();
        Assert.NotNull(json);
        Assert.True(json!.Activity.CurrentTenant.UploadedGamesCount >= 2);
        Assert.True(json.Activity.AllTenants.UploadedGamesCount >= json.Activity.CurrentTenant.UploadedGamesCount);
    }

    [Fact]
    public async Task Activity_UploadedGamesCount_IsScopedToRequestedTenant()
    {
        var tenant = "ApiGameTenant" + Guid.NewGuid().ToString("N")[..8];
        var beforeLocalResp = await _client.GetAsync("/api/activity?tenant=local");
        Assert.Equal(HttpStatusCode.OK, beforeLocalResp.StatusCode);
        var beforeLocalJson = await beforeLocalResp.Content.ReadFromJsonAsync<ActivityApiView>();
        Assert.NotNull(beforeLocalJson);

        var htmlContent = new StringContent("<html><body>tenant game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        await _client.PostAsync($"/api/invaders-gallery?name=TenantGame&tenant={tenant}", htmlContent);

        var localResp = await _client.GetAsync("/api/activity?tenant=local");
        var tenantResp = await _client.GetAsync($"/api/activity?tenant={tenant}");
        Assert.Equal(HttpStatusCode.OK, localResp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tenantResp.StatusCode);

        var localJson = await localResp.Content.ReadFromJsonAsync<ActivityApiView>();
        var tenantJson = await tenantResp.Content.ReadFromJsonAsync<ActivityApiView>();

        Assert.NotNull(localJson);
        Assert.NotNull(tenantJson);
        Assert.Equal(beforeLocalJson!.Activity.CurrentTenant.UploadedGamesCount, localJson!.Activity.CurrentTenant.UploadedGamesCount);
        Assert.Equal(1, tenantJson!.Activity.CurrentTenant.UploadedGamesCount);
        Assert.True(tenantJson.Activity.AllTenants.UploadedGamesCount >= 1);
    }

    [Fact]
    public async Task ListToolUsage_ReturnsBreakdown()
    {
        var toolA = "tool-a-" + Guid.NewGuid().ToString("N");
        var toolB = "tool-b-" + Guid.NewGuid().ToString("N");
        await _client.PostAsync($"/api/event/tool_used?tool_name={toolA}", null);
        await _client.PostAsync($"/api/event/tool_used?tool_name={toolA}", null);
        await _client.PostAsync($"/api/event/tool_used?tool_name={toolB}", null);

        var dashboard = _factory.Services.GetRequiredService<DashboardOperations>();
        var usage = await dashboard.ListToolUsageAsync();
        var testUsage = usage.Where(tool => tool.Name == toolA || tool.Name == toolB).ToList();

        Assert.Equal(2, testUsage.Count);
        Assert.Equal(toolA, testUsage[0].Name);
        Assert.Equal(2, testUsage[0].Count);
        Assert.Equal(toolB, testUsage[1].Name);
        Assert.Equal(1, testUsage[1].Count);
    }

    [Fact]
    public async Task Activity_WithTenantQuery_ReturnsAllTenantsColumnData()
    {
        await _client.PostAsync("/api/event/session_start?session_id=tenant-query-session&user_info=tenant-query-user", null);

        var resp = await _client.GetAsync("/api/activity?tenant=local");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<ActivityApiView>();
        Assert.NotNull(json);
        Assert.Equal("local", json!.Tenant);
        Assert.True(json.Activity.AllTenants.SessionCount >= json.Activity.CurrentTenant.SessionCount);
    }

    [Fact]
    public async Task Tenants_ReturnsCurrentTenant()
    {
        var resp = await _client.GetAsync("/api/tenants");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("local", json?["current_tenant"]?.GetValue<string>());
        Assert.Contains("local", json?["tenants"]?.AsArray().Select(n => n?.GetValue<string>()) ?? []);
    }

    [Theory]
    [InlineData("/api/activity?tenant=../bad")]
    [InlineData("/api/screenshots?tenant=../bad")]
    [InlineData("/api/invaders-gallery/list?tenant=../bad")]
    public async Task ReadEndpoints_WithInvalidTenant_Return400(string url)
    {
        var resp = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData("/api/image?tenant=../bad")]
    [InlineData("/api/invaders-gallery?name=BadTenant&tenant=../bad")]
    public async Task UploadEndpoints_WithInvalidTenant_Return400(string url)
    {
        HttpResponseMessage resp;
        if (url.StartsWith("/api/image", StringComparison.Ordinal))
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent([0x89, 0x50]);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", "photo.png");
            resp = await _client.PostAsync(url, content);
        }
        else
        {
            var htmlContent = new StringContent("<html><body>game</body></html>");
            htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            resp = await _client.PostAsync(url, htmlContent);
        }

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Screenshots_EmptyList()
    {
        var resp = await _client.GetAsync("/api/screenshots");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var urls = await resp.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(urls);
    }

    [Fact]
    public async Task ImageUpload_ReturnsOk()
    {
        using var content = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "test.png");

        var resp = await _client.PostAsync("/api/image", content);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Saved as", body);
    }

    [Fact]
    public void LocalModeStartup_CreatesLocalDataDirectories()
    {
        Assert.True(Directory.Exists(Path.Combine(_factory.TempDir, "screenshots")));
        Assert.True(Directory.Exists(Path.Combine(_factory.TempDir, "gallery")));
    }

    [Fact]
    public async Task ImageUpload_ThenListScreenshots()
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "photo.png");

        await _client.PostAsync("/api/image", content);

        var resp = await _client.GetAsync("/api/screenshots");
        var urls = await resp.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(urls);
        Assert.True(urls!.Count >= 1);
        Assert.All(urls, u => Assert.StartsWith("/api/screenshots/", u));
    }

    [Fact]
    public async Task ImageUpload_WithTenant_ThenListTenantScreenshots()
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "photo.png");

        await _client.PostAsync("/api/image?tenant=SeedTenant", content);

        var resp = await _client.GetAsync("/api/screenshots?tenant=SeedTenant");
        var urls = await resp.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(urls);
        Assert.Single(urls!);
        Assert.StartsWith("/api/screenshots/SeedTenant/", urls![0]);
    }

    [Fact]
    public void ScreenshotUploadInstructions_ReturnsDirectUploadDetails()
    {
        var dashboard = _factory.Services.GetRequiredService<DashboardOperations>();
        var instructions = dashboard.GetScreenshotUploadInstructions("/tmp/player's game.png");

        Assert.EndsWith("/api/image", instructions.UploadUrl);
        Assert.Equal("POST", instructions.Method);
        Assert.Equal("image", instructions.FormField);
        Assert.Contains("image=@", instructions.ExampleCommand);
        Assert.Contains("/tmp/player", instructions.ExampleCommand);
        Assert.Contains("s game.png", instructions.ExampleCommand);
        Assert.Contains($"'{instructions.UploadUrl}'", instructions.ExampleCommand);
        Assert.Contains("outside the MCP tool context", instructions.Instructions);
        Assert.Contains("Do not read", instructions.Instructions);
        Assert.Contains("without opening", instructions.Instructions);
        Assert.Contains("only a suggestion", instructions.Instructions);
    }

    [Fact]
    public void ShareGameInstructions_ReturnsDirectUploadDetails()
    {
        var dashboard = _factory.Services.GetRequiredService<DashboardOperations>();
        var instructions = dashboard.GetShareGameInstructions("/tmp/player's game.html", "Player Game");

        Assert.Contains("/api/invaders-gallery?name=Player%20Game", instructions.UploadUrl);
        Assert.Equal("POST", instructions.Method);
        Assert.Equal("text/html; charset=utf-8", instructions.ContentType);
        Assert.Contains("--data-binary @", instructions.ExampleCommand);
        Assert.Contains("/tmp/player", instructions.ExampleCommand);
        Assert.Contains("s game.html", instructions.ExampleCommand);
        Assert.Contains("Content-Type: text/html; charset=utf-8", instructions.ExampleCommand);
        Assert.Contains($"'{instructions.UploadUrl}'", instructions.ExampleCommand);
        Assert.Contains("outside the MCP tool context", instructions.Instructions);
        Assert.Contains("Do not read", instructions.Instructions);
        Assert.Contains("only a suggestion", instructions.Instructions);
    }

    [Fact]
    public async Task InvadersGallery_NameWithDisallowedChars_SanitizesAndSucceeds()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=My%20Game%27%3B--", htmlContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var item = await resp.Content.ReadFromJsonAsync<GalleryListItem>();
        Assert.NotNull(item);
        Assert.Equal("My Game--", item!.Name);
    }

    [Fact]
    public async Task InvadersGallery_NameOnlyDisallowedChars_GeneratesRandomName()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=%27%3B%3C%3E", htmlContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var item = await resp.Content.ReadFromJsonAsync<GalleryListItem>();
        Assert.NotNull(item);
        Assert.False(string.IsNullOrWhiteSpace(item!.Name));
    }

    [Fact]
    public async Task InvadersGallery_Upload_ReturnsItem()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=TestGame", htmlContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var item = await resp.Content.ReadFromJsonAsync<GalleryListItem>();
        Assert.NotNull(item);
        Assert.Equal("TestGame", item!.Name);
        Assert.StartsWith("/api/gallery/", item.Url);
    }

    [Fact]
    public async Task InvadersGallery_MissingName_GeneratesRandomName()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery", htmlContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var item = await resp.Content.ReadFromJsonAsync<GalleryListItem>();
        Assert.NotNull(item);
        Assert.False(string.IsNullOrWhiteSpace(item!.Name));
    }

    [Fact]
    public async Task InvadersGallery_EmptyBody_Returns400()
    {
        var resp = await _client.PostAsync("/api/invaders-gallery?name=Test", new StringContent(""));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task InvadersGalleryList_ReturnsEntries()
    {
        var htmlContent = new StringContent("<html>g</html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        await _client.PostAsync("/api/invaders-gallery?name=ListTest", htmlContent);

        var resp = await _client.GetAsync("/api/invaders-gallery/list");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var items = await resp.Content.ReadFromJsonAsync<List<GalleryListItem>>();
        Assert.NotNull(items);
        Assert.Contains(items!, i => i.Name == "ListTest");
    }

    [Fact]
    public async Task InvadersGalleryUpload_WithTenant_ThenListTenantEntries()
    {
        var htmlContent = new StringContent("<html>tenant game</html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        await _client.PostAsync("/api/invaders-gallery?name=TenantGame&tenant=SeedTenant", htmlContent);

        var defaultResp = await _client.GetAsync("/api/invaders-gallery/list");
        var defaultItems = await defaultResp.Content.ReadFromJsonAsync<List<GalleryListItem>>();
        var tenantResp = await _client.GetAsync("/api/invaders-gallery/list?tenant=SeedTenant");
        var tenantItems = await tenantResp.Content.ReadFromJsonAsync<List<GalleryListItem>>();

        Assert.DoesNotContain(defaultItems!, i => i.Name == "TenantGame");
        var tenantItem = Assert.Single(tenantItems!, i => i.Name == "TenantGame");
        Assert.StartsWith("/api/gallery/SeedTenant/", tenantItem.Url);
    }

    [Fact]
    public async Task OpenApiJson_ReturnsSpec()
    {
        var resp = await _client.GetAsync("/api/openapi.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("openapi", body);
        Assert.Contains("3.1.0", body);
        Assert.Contains("Lab 502 Community Hub API", body);
    }

    [Fact]
    public async Task MetricsPage_ReturnsHtml()
    {
        var resp = await _client.GetAsync("/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Live Activity Board", content);
        Assert.Contains("Microsoft Build", content);
    }

    [Fact]
    public async Task GalleryPage_ReturnsHtml()
    {
        var resp = await _client.GetAsync("/gallery");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invaders Gallery", content);
    }

    [Fact]
    public async Task ScreenshotFile_NotFound_Returns404()
    {
        var resp = await _client.GetAsync("/api/screenshots/nonexistent.png");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GalleryFile_NotFound_Returns404()
    {
        var resp = await _client.GetAsync("/api/gallery/nonexistent.html");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ScreenshotFile_PathTraversal_Returns404()
    {
        var resp = await _client.GetAsync("/api/screenshots/../../../etc/passwd");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GalleryFile_NonHtml_Returns404()
    {
        var resp = await _client.GetAsync("/api/gallery/test.txt");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task EventEndpoints_GetMethod_Returns405()
    {
        var resp = await _client.GetAsync("/api/event/session_start");
        // Minimal APIs return 405 for wrong methods
        Assert.True(resp.StatusCode == HttpStatusCode.MethodNotAllowed ||
                    resp.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImageUpload_NotFormContent_Returns400()
    {
        var content = new StringContent("not a multipart body");
        var resp = await _client.PostAsync("/api/image", content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ImageUpload_MissingImageField_Returns400()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("some data"), "other_field", "other.png");
        var resp = await _client.PostAsync("/api/image", content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ImageUpload_TooLarge_Returns400()
    {
        // Send a multipart request with a fake Content-Length > 10 MB
        const long fakeSize = 11L * 1024 * 1024;
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "big.png");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/image")
        {
            Content = new FakeContentLengthContent(content, fakeSize)
        };

        var resp = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ScreenshotFile_Exists_ReturnsFileContent()
    {
        // Upload a screenshot
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "serve_test.png");

        var uploadResp = await _client.PostAsync("/api/image", content);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);

        var body = await uploadResp.Content.ReadAsStringAsync();
        var filename = body.Replace("Saved as ", "").Trim();

        // Retrieve the file
        var resp = await _client.GetAsync($"/api/screenshots/{filename}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fileBytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(imageBytes, fileBytes);
    }

    [Fact]
    public async Task GalleryFile_Exists_ReturnsHtmlContent()
    {
        const string html = "<html><body>serve-gallery-test</body></html>";
        var htmlContent = new StringContent(html);
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var uploadResp = await _client.PostAsync("/api/invaders-gallery?name=ServeGalleryTest", htmlContent);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);

        var item = await uploadResp.Content.ReadFromJsonAsync<GalleryListItem>();
        Assert.NotNull(item);

        // item.Url is e.g. "/api/gallery/xxxxxxxx.html"
        var resp = await _client.GetAsync(item!.Url);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content2 = await resp.Content.ReadAsStringAsync();
        Assert.Equal(html, content2);
    }

    [Fact]
    public async Task GalleryUpload_TooLarge_Returns400()
    {
        // Content > 200 KB triggers the ContentLength check
        var largeBody = new byte[201 * 1024];
        var content = new ByteArrayContent(largeBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=Oversized", content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GalleryList_WithLimit_ReturnsAtMostLimitItems()
    {
        // Upload 3 items
        for (var i = 0; i < 3; i++)
        {
            var html = new StringContent($"<html>{i}</html>");
            html.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            await _client.PostAsync($"/api/invaders-gallery?name=LimitGame{i}", html);
        }

        var resp = await _client.GetAsync("/api/invaders-gallery/list?limit=2");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var items = await resp.Content.ReadFromJsonAsync<List<GalleryListItem>>();
        Assert.NotNull(items);
        Assert.True(items!.Count <= 2);
    }

    [Fact]
    public async Task GalleryList_WithLargeLimit_CapsAtDefault()
    {
        var resp = await _client.GetAsync("/api/invaders-gallery/list?limit=99999");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var items = await resp.Content.ReadFromJsonAsync<List<GalleryListItem>>();
        Assert.NotNull(items);
        Assert.True(items!.Count <= GalleryListLimits.DefaultLimit);
    }

    [Fact]
    public async Task MetricsPage_WithToolData_ContainsToolName()
    {
        await _client.PostAsync("/api/event/tool_used?tool_name=coverage-tool-xyz", null);

        var resp = await _client.GetAsync("/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("coverage-tool-xyz", html);
    }

    [Fact]
    public async Task MetricsPage_ShowsTenantSelectorAndAllTenantsColumn()
    {
        var resp = await _client.GetAsync("/activity?tenant=local");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("id=\"tenant-select\"", html);
        Assert.Contains("All Labs", html);
    }

    [Fact]
    public async Task MetricsPage_TenantSelectorIncludesScreenshotOnlyTenants()
    {
        var tenant = "ShotTenant" + Guid.NewGuid().ToString("N")[..8];
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "tenant-shot.png");
        await _client.PostAsync($"/api/image?tenant={tenant}", content);

        var resp = await _client.GetAsync("/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{tenant}\"", html);
    }

    [Fact]
    public async Task MetricsPage_TenantUploadedGamesCountUsesSelectedTenant()
    {
        var tenant = "GameTenant" + Guid.NewGuid().ToString("N")[..8];
        var htmlContent = new StringContent("<html><body>tenant game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        await _client.PostAsync($"/api/invaders-gallery?name=TenantGame&tenant={tenant}", htmlContent);

        var resp = await _client.GetAsync($"/activity?tenant={tenant}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{tenant}\" selected", html);
        Assert.Contains($"><a class=\"metric-link\" href=\"/gallery?tenant={tenant}\">1</a></td>", html);
    }

    [Fact]
    public async Task MetricsPage_UploadedGamesValueLinksToTenantGallery()
    {
        var tenant = "LinkTenant" + Guid.NewGuid().ToString("N")[..8];
        var resp = await _client.GetAsync($"/activity?tenant={tenant}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains($"<td id=\"uploaded-games-count\"><a class=\"metric-link\" href=\"/gallery?tenant={tenant}\">", html);
        Assert.DoesNotContain($"<a class=\"metric-link\" href=\"/gallery?tenant={tenant}\"><strong>Uploaded Games</strong></a>", html);
    }

    [Fact]
    public async Task MetricsPage_WithScreenshot_ContainsImageSrc()
    {
        // Upload a screenshot
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "metrics_page_test.png");
        var uploadResp = await _client.PostAsync("/api/image", content);
        var body = await uploadResp.Content.ReadAsStringAsync();
        var filename = body.Replace("Saved as ", "").Trim();

        var resp = await _client.GetAsync("/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains(filename, html);
    }

    [Fact]
    public async Task ScreenshotsPage_TenantSelectorIncludesScreenshotTenants()
    {
        var tenant = "ScreenTenant" + Guid.NewGuid().ToString("N")[..8];
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "screen-tenant.png");
        await _client.PostAsync($"/api/image?tenant={tenant}", content);

        var resp = await _client.GetAsync("/screenshots");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{tenant}\"", html);
    }

    [Fact]
    public async Task GalleryPage_WithItems_ContainsItemName()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        await _client.PostAsync("/api/invaders-gallery?name=UniqueCoverageGame7391", htmlContent);

        var resp = await _client.GetAsync("/gallery");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("UniqueCoverageGame7391", html);
    }

    [Fact]
    public async Task GalleryPage_ShowsTenantSelector()
    {
        var resp = await _client.GetAsync("/gallery?tenant=local");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("id=\"tenant-select\"", html);
    }

    [Fact]
    public async Task DebugPage_WithoutDbgEnv_Returns404()
    {
        var previous = Environment.GetEnvironmentVariable("DBG");
        try
        {
            Environment.SetEnvironmentVariable("DBG", null);

            var resp = await _client.GetAsync("/dbg");

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBG", previous);
        }
    }

    [Fact]
    public async Task DebugPage_WithDbgEnv_ReturnsVersionAndLocalSqlMessage()
    {
        var previous = Environment.GetEnvironmentVariable("DBG");
        try
        {
            Environment.SetEnvironmentVariable("DBG", "1");

            var resp = await _client.GetAsync("/dbg");

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var html = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Version", html);
            Assert.Contains("id=\"tenant-select\"", html);
            Assert.Contains("Delete tenant data", html);
            Assert.Contains("Delete all tenant data", html);
            Assert.Contains("SQL table dump is not available in local mode.", html);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBG", previous);
        }
    }

    [Fact]
    public async Task ScreenshotFile_WithDotDotName_Returns404()
    {
        // A filename with ".." embedded should be rejected as unsafe
        var resp = await _client.GetAsync("/api/screenshots/test..png");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GalleryList_EntryWithoutUrl_UsesServeGalleryHtml()
    {
        // Add an entry without URL through the app's actual gallery service to
        // exercise the "missing URL → use ServeGalleryHtml" branch in the list handler.
        var gallery = _factory.Services.GetRequiredService<IGalleryIndex>();
        await gallery.AddAsync(new GalleryEntry
        {
            Name = "NoUrlGalleryItem",
            Filename = "no-url-test.html"
        });

        var resp = await _client.GetAsync("/api/invaders-gallery/list");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var items = await resp.Content.ReadFromJsonAsync<List<GalleryListItem>>();
        Assert.NotNull(items);
        var item = items!.FirstOrDefault(i => i.Name == "NoUrlGalleryItem");
        Assert.NotNull(item);
        Assert.Contains("no-url-test.html", item!.Url);
    }

    [Fact]
    public async Task GalleryUpload_BodyExceedsLimitWithNoContentLength_Returns400()
    {
        // Send a body > 200 KB without a Content-Length header so the ContentLength
        // check is skipped and ReadBodyWithLimitAsync's internal guard triggers.
        var largeBody = new byte[203 * 1024];
        var content = new NoContentLengthContent(largeBody, "text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=StreamBig", content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DebugPage_PostDeleteTenant_WithValidTenant_ShowsSuccessMessage()
    {
        var previous = Environment.GetEnvironmentVariable("DBG");
        try
        {
            Environment.SetEnvironmentVariable("DBG", "1");

            // Use a cookie-aware client to support anti-forgery validation
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

            // GET the page first to obtain the anti-forgery token
            var getResp = await client.GetAsync("/dbg");
            Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
            var html = await getResp.Content.ReadAsStringAsync();

            var tokenMatch = System.Text.RegularExpressions.Regex.Match(
                html, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            Assert.True(tokenMatch.Success, "Anti-forgery token not found in page HTML");
            var token = tokenMatch.Groups[1].Value;

            // POST to the DeleteTenant handler with a valid tenant
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["tenant"] = "local"
            });
            var postResp = await client.PostAsync("/dbg?handler=DeleteTenant", content);

            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            var resultHtml = await postResp.Content.ReadAsStringAsync();
            Assert.Contains("Deleted data for tenant", resultHtml);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBG", previous);
        }
    }

    [Fact]
    public async Task DebugPage_PostDeleteTenant_WithBlankTenant_ShowsErrorMessage()
    {
        var previous = Environment.GetEnvironmentVariable("DBG");
        try
        {
            Environment.SetEnvironmentVariable("DBG", "1");

            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

            var getResp = await client.GetAsync("/dbg");
            var html = await getResp.Content.ReadAsStringAsync();

            var tokenMatch = System.Text.RegularExpressions.Regex.Match(
                html, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            Assert.True(tokenMatch.Success, "Anti-forgery token not found in page HTML");
            var token = tokenMatch.Groups[1].Value;

            // POST with a blank tenant — should be rejected with an error message
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["tenant"] = ""
            });
            var postResp = await client.PostAsync("/dbg?handler=DeleteTenant", content);

            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            var resultHtml = await postResp.Content.ReadAsStringAsync();
            Assert.Contains("Invalid tenant", resultHtml);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBG", previous);
        }
    }

    /// <summary>
    /// Custom HttpContent that wraps another content but reports a specific Content-Length.
    /// Used to test server-side size checks without actually sending large payloads.
    /// </summary>
    private sealed class FakeContentLengthContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly long _fakeLength;

        public FakeContentLengthContent(HttpContent inner, long fakeLength)
        {
            _inner = inner;
            _fakeLength = fakeLength;
            // Copy content-type and other content headers
            foreach (var header in inner.Headers)
                Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => _inner.CopyToAsync(stream);

        protected override bool TryComputeLength(out long length)
        {
            length = _fakeLength;
            return true;
        }
    }

    /// <summary>
    /// Custom HttpContent that sends bytes without setting a Content-Length header
    /// (TryComputeLength returns false), causing chunked transfer encoding.
    /// </summary>
    private sealed class NoContentLengthContent : HttpContent
    {
        private readonly byte[] _data;

        public NoContentLengthContent(byte[] data, string mediaType)
        {
            _data = data;
            Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => stream.WriteAsync(_data).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false; // omit Content-Length → server sees null ContentLength
        }
    }
}
