using System.Net;
using System.Net.Http.Headers;
using CommunityHub.Models;
using CommunityHub.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CommunityHub.Tests;

/// <summary>
/// Tests that endpoints handle exceptions from IMetricsStore gracefully,
/// returning success responses despite the metrics store throwing.
/// </summary>
public class ExceptionHandlingTests : IClassFixture<ExceptionHandlingTests.TestApp>, IDisposable
{
    private readonly HttpClient _client;

    public class TestApp : WebApplicationFactory<Program>
    {
        public string TempDir { get; }

        public TestApp()
        {
            TempDir = Path.Combine(Path.GetTempPath(), "exctest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDir);
            Environment.SetEnvironmentVariable("APP_MODE", "local");
            Environment.SetEnvironmentVariable("LOCAL_DATA_DIR", TempDir);
            Environment.SetEnvironmentVariable("PORT", "0");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseUrls("http://127.0.0.1:0");
            builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CommunityHub"));
            builder.ConfigureTestServices(services =>
            {
                var mock = new Mock<IMetricsStore>();
                mock.Setup(m => m.OnSessionStartAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new InvalidOperationException("metrics unavailable"));
                mock.Setup(m => m.OnToolUsedAsync(It.IsAny<string>()))
                    .ThrowsAsync(new InvalidOperationException("metrics unavailable"));
                mock.Setup(m => m.OnAgentStopAsync())
                    .ThrowsAsync(new InvalidOperationException("metrics unavailable"));
                mock.Setup(m => m.OnSubagentStopAsync())
                    .ThrowsAsync(new InvalidOperationException("metrics unavailable"));
                mock.Setup(m => m.SnapshotAsync())
                    .ReturnsAsync(new ActivitySnapshot());

                services.AddSingleton(mock.Object);
            });
        }
    }

    public ExceptionHandlingTests(TestApp factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task SessionStart_MetricsThrows_StillReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/session_start?session_id=s1&user_info=u1", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ToolUsed_MetricsThrows_StillReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/tool_used?tool_name=my-tool", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SubagentStop_MetricsThrows_StillReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/subagent_stop", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AgentStop_MetricsThrows_StillReturnsOk()
    {
        var resp = await _client.PostAsync("/api/event/agent_stop", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GalleryUpload_MetricsThrows_StillReturnsItem()
    {
        var htmlContent = new StringContent("<html><body>game</body></html>");
        htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var resp = await _client.PostAsync("/api/invaders-gallery?name=ExcTest", htmlContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ImageUpload_MetricsThrows_StillReturnsOk()
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(PngHeader);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "exc_test.png");

        var resp = await _client.PostAsync("/api/image", content);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
