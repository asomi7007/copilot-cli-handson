using CommunityHub.Config;

namespace CommunityHub.Tests;

public class AppConfigTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnv = [];

    private void SetEnv(string key, string? value)
    {
        if (!_originalEnv.ContainsKey(key))
            _originalEnv[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private void ClearAllAppEnv()
    {
        var keys = new[]
        {
            "APP_MODE", "PORT", "LOCAL_DATA_DIR", "APP_TENANT",
            "SQL_SERVER", "SQL_DATABASE", "AZURE_STORAGE_ACCOUNT",
            "AZURE_BLOB_PUBLIC_BASE", "APPLICATIONINSIGHTS_CONNECTION_STRING",
            "LAB502_DASHBOARD_URL", "DASHBOARD_URL"
        };
        foreach (var key in keys) SetEnv(key, null);
    }

    public void Dispose()
    {
        foreach (var (key, value) in _originalEnv)
            Environment.SetEnvironmentVariable(key, value);
    }

    [Fact]
    public void Load_DefaultsToLocalMode()
    {
        ClearAllAppEnv();
        var config = AppConfig.Load();
        Assert.Equal(AppMode.Local, config.Mode);
        Assert.Equal("1345", config.Port);
        Assert.Equal(".", config.LocalDataDir);
        Assert.Equal("http://localhost:1345", config.DashboardBaseUrl);
    }

    [Fact]
    public void Load_LocalMode_CustomPort()
    {
        ClearAllAppEnv();
        SetEnv("PORT", "8080");
        var config = AppConfig.Load();
        Assert.Equal("8080", config.Port);
        Assert.Equal("http://localhost:8080", config.DashboardBaseUrl);
    }

    [Fact]
    public void Load_LocalMode_CustomDataDir()
    {
        ClearAllAppEnv();
        SetEnv("LOCAL_DATA_DIR", "/tmp/test");
        var config = AppConfig.Load();
        Assert.Equal("/tmp/test", config.LocalDataDir);
    }

    [Fact]
    public void Load_LocalMode_DashboardUrlFromEnv()
    {
        ClearAllAppEnv();
        SetEnv("LAB502_DASHBOARD_URL", "https://example.com");
        var config = AppConfig.Load();
        Assert.Equal("https://example.com", config.DashboardBaseUrl);
    }

    [Fact]
    public void Load_LocalMode_DashboardUrlFallback()
    {
        ClearAllAppEnv();
        SetEnv("DASHBOARD_URL", "https://fallback.com");
        var config = AppConfig.Load();
        Assert.Equal("https://fallback.com", config.DashboardBaseUrl);
    }

    [Fact]
    public void Load_InvalidMode_Throws()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "invalid");
        Assert.Throws<InvalidOperationException>(() => AppConfig.Load());
    }

    [Fact]
    public void Load_CloudMode_MissingTenant_Throws()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "cloud");
        Assert.Throws<InvalidOperationException>(() => AppConfig.Load());
    }

    [Fact]
    public void Load_CloudMode_InvalidTenant_Throws()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "cloud");
        SetEnv("APP_TENANT", "invalid_tenant");
        Assert.Throws<InvalidOperationException>(() => AppConfig.Load());
    }

    [Fact]
    public void Load_CloudMode_UppercaseTenant_IsValid()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "cloud");
        SetEnv("APP_TENANT", "Lab-502");
        SetEnv("SQL_SERVER", "server.database.windows.net");
        SetEnv("AZURE_STORAGE_ACCOUNT", "mystorageacct");
        SetEnv("AZURE_BLOB_PUBLIC_BASE", "https://mystorageacct.blob.core.windows.net");

        var config = AppConfig.Load();

        Assert.Equal("Lab-502", config.Tenant);
    }

    [Fact]
    public void Load_CloudMode_MissingRequiredVars_Throws()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "cloud");
        SetEnv("APP_TENANT", "test-tenant");
        var ex = Assert.Throws<InvalidOperationException>(() => AppConfig.Load());
        Assert.Contains("SQL_SERVER", ex.Message);
        Assert.Contains("AZURE_STORAGE_ACCOUNT", ex.Message);
        Assert.Contains("AZURE_BLOB_PUBLIC_BASE", ex.Message);
    }

    [Fact]
    public void Load_CloudMode_AllVarsSet()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "cloud");
        SetEnv("APP_TENANT", "test-tenant");
        SetEnv("SQL_SERVER", "server.database.windows.net");
        SetEnv("AZURE_STORAGE_ACCOUNT", "mystorageacct");
        SetEnv("AZURE_BLOB_PUBLIC_BASE", "https://mystorageacct.blob.core.windows.net");

        var config = AppConfig.Load();
        Assert.Equal(AppMode.Cloud, config.Mode);
        Assert.Equal("test-tenant", config.Tenant);
        Assert.Equal("server.database.windows.net", config.SqlServer);
        Assert.Equal("dashboard", config.SqlDatabase);
        Assert.Equal("mystorageacct", config.AzureStorageAccount);
    }

    [Fact]
    public void Load_LocalMode_WithTenantSet_DoesNotThrow()
    {
        ClearAllAppEnv();
        SetEnv("APP_MODE", "local");
        SetEnv("APP_TENANT", "some-tenant");

        // APP_TENANT is ignored (with a warning) in local mode — must not throw
        var config = AppConfig.Load();
        Assert.Equal(AppMode.Local, config.Mode);
    }
}
