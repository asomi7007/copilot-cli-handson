using System.Text.RegularExpressions;

namespace CommunityHub.Config;

public enum AppMode
{
    Local,
    Cloud,
    Sqlite
}

public partial class AppConfig
{
    public AppMode Mode { get; set; }
    public string Tenant { get; set; } = "";
    public string Port { get; set; } = "1345";

    // Local mode
    public string LocalDataDir { get; set; } = ".";

    // Sqlite mode -- path to the SQLite database file (place on a persistent volume,
    // e.g. /home/data/communityhub.db on Azure App Service).
    public string SqliteDbPath { get; set; } = "communityhub.db";

    // Cloud mode — Azure SQL
    public string SqlServer { get; set; } = "";
    public string SqlDatabase { get; set; } = "dashboard";
    public string SqlConnectionString { get; set; } = "";

    // Cloud mode — Azure Blob Storage
    public string AzureStorageAccount { get; set; } = "";
    public string AzureBlobPublicBase { get; set; } = "";

    // Cloud mode — observability
    public string AppInsightsConnStr { get; set; } = "";

    // Community Hub URL
    public string DashboardBaseUrl { get; set; } = "";

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$")]
    private static partial Regex TenantRegex();

    public static AppConfig Load()
    {
        var modeStr = EnvOr("APP_MODE", "local");
        if (modeStr != "local" && modeStr != "cloud" && modeStr != "sqlite")
            throw new InvalidOperationException($"APP_MODE must be 'local', 'cloud', or 'sqlite', got '{modeStr}'");

        var mode = modeStr switch
        {
            "cloud" => AppMode.Cloud,
            "sqlite" => AppMode.Sqlite,
            _ => AppMode.Local
        };
        var port = EnvOr("PORT", "1345");

        var config = new AppConfig
        {
            Mode = mode,
            Port = port
        };

        // Community Hub base URL
        config.DashboardBaseUrl = Environment.GetEnvironmentVariable("LAB502_DASHBOARD_URL") ?? "";
        if (string.IsNullOrEmpty(config.DashboardBaseUrl))
            config.DashboardBaseUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "";
        if (string.IsNullOrEmpty(config.DashboardBaseUrl))
            config.DashboardBaseUrl = $"http://localhost:{port}";

        if (mode == AppMode.Local)
        {
            config.LocalDataDir = EnvOr("LOCAL_DATA_DIR", ".");
            var t = Environment.GetEnvironmentVariable("APP_TENANT");
            if (!string.IsNullOrEmpty(t))
                Console.WriteLine($"Warning: APP_TENANT=\"{t}\" is ignored in local mode");
            return config;
        }

        if (mode == AppMode.Sqlite)
        {
            // Sqlite mode reuses the Azure Blob settings from cloud mode but stores
            // activity counters and the gallery/screenshot index in a SQLite file.
            config.SqliteDbPath = EnvOr("SQLITE_DB_PATH", "communityhub.db");

            config.Tenant = Environment.GetEnvironmentVariable("APP_TENANT") ?? "";
            if (!TenantRegex().IsMatch(config.Tenant))
                throw new InvalidOperationException($"APP_TENANT must match ^[a-zA-Z0-9][a-zA-Z0-9-]{{0,30}}$, got '{config.Tenant}'");

            var missingSqlite = new List<string>();
            config.AzureStorageAccount = RequireEnv("AZURE_STORAGE_ACCOUNT", missingSqlite);
            config.AzureBlobPublicBase = RequireEnv("AZURE_BLOB_PUBLIC_BASE", missingSqlite);
            config.AppInsightsConnStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") ?? "";
            if (missingSqlite.Count > 0)
                throw new InvalidOperationException($"Required env vars missing in sqlite mode: {string.Join(", ", missingSqlite)}");

            return config;
        }

        // Cloud mode
        config.Tenant = Environment.GetEnvironmentVariable("APP_TENANT") ?? "";
        if (!TenantRegex().IsMatch(config.Tenant))
            throw new InvalidOperationException($"APP_TENANT must match ^[a-zA-Z0-9][a-zA-Z0-9-]{{0,30}}$, got '{config.Tenant}'");

        var missing = new List<string>();
        config.SqlConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING") ?? "";
        if (string.IsNullOrEmpty(config.SqlConnectionString))
        {
            config.SqlServer = RequireEnv("SQL_SERVER", missing);
            config.SqlDatabase = EnvOr("SQL_DATABASE", "dashboard");
        }
        config.AzureStorageAccount = RequireEnv("AZURE_STORAGE_ACCOUNT", missing);
        config.AzureBlobPublicBase = RequireEnv("AZURE_BLOB_PUBLIC_BASE", missing);
        config.AppInsightsConnStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") ?? "";

        if (missing.Count > 0)
            throw new InvalidOperationException($"Required env vars missing in cloud mode: {string.Join(", ", missing)}");

        return config;
    }

    private static string EnvOr(string key, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    private static string RequireEnv(string key, List<string> missing)
    {
        var v = Environment.GetEnvironmentVariable(key) ?? "";
        if (string.IsNullOrEmpty(v))
            missing.Add(key);
        return v;
    }
}
