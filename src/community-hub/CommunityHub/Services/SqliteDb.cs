using Microsoft.Data.Sqlite;

namespace CommunityHub.Services;

/// <summary>
/// SQLite-backed schema bootstrap and connection factory.
/// Mirrors <see cref="MssqlDb"/> but targets a local SQLite database file so the
/// Community Hub can run on Azure App Service using Blob Storage for files and a
/// SQLite file (on the persistent /home volume) for activity counters and the
/// gallery/screenshot index -- without provisioning Azure SQL.
/// </summary>
public static class SqliteDb
{
    private const string CreateSessionsTable = """
        CREATE TABLE IF NOT EXISTS sessions (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            tenant     TEXT NOT NULL COLLATE NOCASE,
            session_id TEXT NOT NULL,
            user_info  TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
        );
        """;

    private const string CreateSessionsIndexes = """
        CREATE INDEX IF NOT EXISTS idx_sessions_tenant ON sessions (tenant);
        CREATE INDEX IF NOT EXISTS idx_sessions_tenant_session_id ON sessions (tenant, session_id);
        CREATE INDEX IF NOT EXISTS idx_sessions_tenant_user_info ON sessions (tenant, user_info);
        """;

    private const string CreateToolCountsTable = """
        CREATE TABLE IF NOT EXISTS tool_counts (
            tenant    TEXT NOT NULL COLLATE NOCASE,
            tool_name TEXT NOT NULL,
            count     INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (tenant, tool_name)
        );
        """;

    private const string CreateCountersTable = """
        CREATE TABLE IF NOT EXISTS counters (
            tenant  TEXT NOT NULL COLLATE NOCASE,
            name    TEXT NOT NULL,
            value   INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (tenant, name)
        );
        """;

    private const string CreateGalleryTable = """
        CREATE TABLE IF NOT EXISTS gallery (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            tenant      TEXT NOT NULL COLLATE NOCASE,
            name        TEXT NOT NULL,
            filename    TEXT NOT NULL,
            blob_url    TEXT NOT NULL,
            uploaded_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
        );
        """;

    private const string CreateGalleryIndex = """
        CREATE INDEX IF NOT EXISTS idx_gallery_tenant ON gallery (tenant);
        CREATE INDEX IF NOT EXISTS idx_gallery_tenant_uploaded_at ON gallery (tenant, uploaded_at, id);
        """;

    private const string CreateScreenshotsTable = """
        CREATE TABLE IF NOT EXISTS screenshots (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            tenant      TEXT NOT NULL COLLATE NOCASE,
            blob_url    TEXT NOT NULL,
            uploaded_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
        );
        """;

    private const string CreateScreenshotsIndex = """
        CREATE INDEX IF NOT EXISTS idx_screenshots_tenant ON screenshots (tenant);
        """;

    private static readonly string[] SchemaBatches =
    [
        CreateSessionsTable,
        CreateSessionsIndexes,
        CreateToolCountsTable,
        CreateCountersTable,
        CreateGalleryTable,
        CreateGalleryIndex,
        CreateScreenshotsTable,
        CreateScreenshotsIndex,
    ];

    public static IReadOnlyList<string> KnownTableNames { get; } =
    [
        "sessions",
        "tool_counts",
        "counters",
        "gallery",
        "screenshots",
    ];

    public static string BuildConnectionString(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        return builder.ConnectionString;
    }

    public static SqliteConnection CreateConnection(string connectionString)
    {
        return new SqliteConnection(connectionString);
    }

    public static async Task BootstrapAsync(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = CreateConnection(connectionString);
        await conn.OpenAsync();

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync();
        }

        foreach (var batch in SchemaBatches)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = batch.Trim();
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
