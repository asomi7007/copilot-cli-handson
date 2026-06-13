using System.Reflection;
using CommunityHub.Config;
using CommunityHub.Services;

namespace CommunityHub.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly string AppVersion =
        Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";

    public static void AddAppInsights(this WebApplicationBuilder builder, AppConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.AppInsightsConnStr))
        {
            builder.Services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = cfg.AppInsightsConnStr;
            });
        }
    }

    public static async Task AddDashboardServicesAsync(this WebApplicationBuilder builder, AppConfig cfg)
    {
        builder.Services.AddSingleton(cfg);

        switch (cfg.Mode)
        {
            case AppMode.Local:
                Console.WriteLine($"Starting in LOCAL mode (data dir: {cfg.LocalDataDir})");
                LocalDataDirectories.EnsureExists(cfg.LocalDataDir);

                var localBlobs = new LocalBlobStore(cfg.LocalDataDir);
                var localGallery = new LocalGalleryIndex(cfg.LocalDataDir);
                var localMetrics = new LocalMetrics();

                builder.Services.AddSingleton<IBlobStore>(localBlobs);
                builder.Services.AddSingleton<IGalleryIndex>(localGallery);
                builder.Services.AddSingleton<IMetricsStore>(localMetrics);
                builder.Services.AddSingleton<IDebugSqlDumper, NoSqlDebugDumper>();
                builder.Services.AddSingleton<IDebugDataCleaner>(new LocalDebugDataCleaner(localMetrics, localGallery, localBlobs));
                break;

            case AppMode.Sqlite:
                Console.WriteLine($"Starting in SQLITE mode (tenant: {cfg.Tenant}, commit: {AppVersion})");
                Console.WriteLine($"  SQLite DB:        {cfg.SqliteDbPath}");
                Console.WriteLine($"  Storage Account:  {cfg.AzureStorageAccount}");

                var sqliteConnStr = SqliteDb.BuildConnectionString(cfg.SqliteDbPath);
                await SqliteDb.BootstrapAsync(sqliteConnStr);

                var sqliteBlobs = new AzureBlobStoreSqlite(cfg.AzureStorageAccount, cfg.Tenant, cfg.AzureBlobPublicBase, sqliteConnStr);
                var sqliteMetrics = new SqliteMetrics(sqliteConnStr, cfg.Tenant);
                var sqliteGallery = new SqliteGalleryIndex(sqliteConnStr, cfg.Tenant);

                builder.Services.AddSingleton<IBlobStore>(sqliteBlobs);
                builder.Services.AddSingleton<IGalleryIndex>(sqliteGallery);
                builder.Services.AddSingleton<IMetricsStore>(sqliteMetrics);
                builder.Services.AddSingleton<IDebugSqlDumper, NoSqlDebugDumper>();
                builder.Services.AddSingleton<IDebugDataCleaner>(new SqliteDebugDataCleaner(sqliteConnStr, cfg.Tenant, sqliteBlobs));
                break;

            case AppMode.Cloud:
                Console.WriteLine($"Starting in CLOUD mode (tenant: {cfg.Tenant}, commit: {AppVersion})");
                Console.WriteLine($"  SQL Server:       {cfg.SqlServer}");
                Console.WriteLine($"  Storage Account:  {cfg.AzureStorageAccount}");

                var sqlConnStr = MssqlDb.NormalizeConnectionStringForPooling(!string.IsNullOrEmpty(cfg.SqlConnectionString)
                    ? cfg.SqlConnectionString
                    : MssqlDb.BuildConnectionString(cfg.SqlServer, cfg.SqlDatabase));
                await MssqlDb.BootstrapAsync(sqlConnStr);

                var azureBlobs = new AzureBlobStore(cfg.AzureStorageAccount, cfg.Tenant, cfg.AzureBlobPublicBase, sqlConnStr);
                var sqlMetrics = new SqlMetrics(sqlConnStr, cfg.Tenant);
                var sqlGallery = new SqlGalleryIndex(sqlConnStr, cfg.Tenant);

                builder.Services.AddSingleton<IBlobStore>(azureBlobs);
                builder.Services.AddSingleton<IGalleryIndex>(sqlGallery);
                builder.Services.AddSingleton<IMetricsStore>(sqlMetrics);
                builder.Services.AddSingleton<IDebugSqlDumper>(new SqlDebugDumper(sqlConnStr));
                builder.Services.AddSingleton<IDebugDataCleaner>(new SqlDebugDataCleaner(sqlConnStr, cfg.Tenant, azureBlobs));
                break;
        }

        builder.Services.AddSingleton<DashboardOperations>();
    }
}
