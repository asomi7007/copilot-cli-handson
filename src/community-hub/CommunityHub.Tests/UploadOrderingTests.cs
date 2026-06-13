using System.Net;
using System.Net.Http.Headers;
using CommunityHub.Models;
using CommunityHub.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityHub.Tests;

public class UploadOrderingTests : IDisposable
{
    private readonly TestApp _factory;
    private readonly HttpClient _client;

    public UploadOrderingTests()
    {
        _factory = new TestApp();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ImageUpload_WritesBlobWithoutMetricsCounter()
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "ordered.png");

        var resp = await _client.PostAsync("/api/image", content);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(["blob:screenshot"], _factory.Recorder.Calls);
    }

    [Fact]
    public async Task ImageUpload_DoesNotWriteDatabaseWhenBlobUploadFails()
    {
        _factory.Recorder.FailBlobUploads = true;
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "failed.png");

        var resp = await _client.PostAsync("/api/image", content);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        Assert.DoesNotContain(_factory.Recorder.Calls, c => c.StartsWith("db:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GalleryUpload_WritesDatabaseOnlyAfterBlobUploadSucceeds()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=OrderedGame", htmlContent);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(["blob:gallery", "db:gallery"], _factory.Recorder.Calls);
    }

    [Fact]
    public async Task GalleryUpload_DoesNotWriteDatabaseWhenBlobUploadFails()
    {
        _factory.Recorder.FailBlobUploads = true;
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=FailedGame", htmlContent);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        Assert.DoesNotContain(_factory.Recorder.Calls, c => c.StartsWith("db:", StringComparison.Ordinal));
    }

    public class TestApp : WebApplicationFactory<Program>
    {
        public UploadRecorder Recorder { get; } = new();
        private readonly string _tempDir;

        public TestApp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "upload_ordering_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            Environment.SetEnvironmentVariable("APP_MODE", "local");
            Environment.SetEnvironmentVariable("LOCAL_DATA_DIR", _tempDir);
            Environment.SetEnvironmentVariable("PORT", "0");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseUrls("http://127.0.0.1:0");
            builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CommunityHub"));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IBlobStore>(new RecordingBlobStore(Recorder));
                services.AddSingleton<IGalleryIndex>(new RecordingGalleryIndex(Recorder));
                services.AddSingleton<IMetricsStore>(new RecordingMetricsStore());
            });
        }
    }

    public class UploadRecorder
    {
        public List<string> Calls { get; } = [];
        public bool FailBlobUploads { get; set; }
    }

    private sealed class RecordingBlobStore(UploadRecorder recorder) : IBlobStore
    {
        public string CurrentTenant => "local";
        public bool IsLocalServing => false;

        public Task<string> PutScreenshotAsync(string name, Stream data, string contentType, string? tenant = null)
        {
            if (recorder.FailBlobUploads)
                throw new InvalidOperationException("blob unavailable");
            recorder.Calls.Add("blob:screenshot");
            return Task.FromResult($"/api/screenshots/{name}");
        }

        public Task<string> PutGalleryHtmlAsync(string name, byte[] data, string? tenant = null)
        {
            if (recorder.FailBlobUploads)
                throw new InvalidOperationException("blob unavailable");
            recorder.Calls.Add("blob:gallery");
            return Task.FromResult($"/api/gallery/{name}");
        }

        public Task<List<string>> ListScreenshotsAsync(string? tenant = null) => Task.FromResult<List<string>>([]);
        public Task<List<string>> ListTenantsAsync() => Task.FromResult<List<string>>(["local"]);
        public string ServeGalleryHtml(string name, string? tenant = null) => $"/api/gallery/{name}";
    }

    private sealed class RecordingGalleryIndex(UploadRecorder recorder) : IGalleryIndex
    {
        public string CurrentTenant => "local";

        public Task AddAsync(GalleryEntry entry, string? tenant = null)
        {
            recorder.Calls.Add("db:gallery");
            return Task.CompletedTask;
        }

        public Task<List<GalleryEntry>> ListAsync(int? limit = null, string? tenant = null) => Task.FromResult<List<GalleryEntry>>([]);
        public Task<int> CountAsync(string? tenant = null) => Task.FromResult(0);
        public Task<(int Tenant, int AllTenants)> CountWithAllTenantsAsync(string? tenant = null) => Task.FromResult((0, 0));
        public Task<List<string>> ListTenantsAsync() => Task.FromResult<List<string>>(["local"]);
    }

    private sealed class RecordingMetricsStore : IMetricsStore
    {
        public string CurrentTenant => "local";

        public Task OnSessionStartAsync(string sessionId, string userId) => Task.CompletedTask;
        public Task OnPromptSubmittedAsync() => Task.CompletedTask;
        public Task OnToolUsedAsync(string tool) => Task.CompletedTask;
        public Task OnAgentStopAsync() => Task.CompletedTask;
        public Task OnSubagentStopAsync() => Task.CompletedTask;

        public Task<ActivitySnapshot> SnapshotAsync(string? tenant = null) => Task.FromResult(new ActivitySnapshot());
        public Task<ActivitySnapshot> AllTenantsSnapshotAsync() => Task.FromResult(new ActivitySnapshot());
        public Task<(ActivitySnapshot Tenant, ActivitySnapshot AllTenants)> SnapshotWithAllTenantsAsync(string? tenant = null) => Task.FromResult((new ActivitySnapshot(), new ActivitySnapshot()));
        public Task<List<string>> ListTenantsAsync() => Task.FromResult<List<string>>(["local"]);
    }
}
