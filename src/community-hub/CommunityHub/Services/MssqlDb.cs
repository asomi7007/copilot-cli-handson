using Microsoft.Data.SqlClient;

namespace CommunityHub.Services;

public static class MssqlDb
{
    private const string CreateSessionsTable = """
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sessions')
        CREATE TABLE sessions (
            id         BIGINT IDENTITY(1,1) PRIMARY KEY,
            tenant     NVARCHAR(31) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
            session_id NVARCHAR(255) NOT NULL,
            user_info  NVARCHAR(255) NOT NULL DEFAULT '',
            created_at DATETIME2 DEFAULT GETUTCDATE(),
            INDEX idx_sessions_tenant (tenant)
        );
        """;

    private const string CreateSessionsTenantSessionIdIndex = """
        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_sessions_tenant_session_id' AND object_id = OBJECT_ID('sessions'))
        CREATE INDEX idx_sessions_tenant_session_id ON sessions (tenant, session_id);
        """;

    private const string CreateSessionsTenantUserInfoIndex = """
        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_sessions_tenant_user_info' AND object_id = OBJECT_ID('sessions'))
        CREATE INDEX idx_sessions_tenant_user_info ON sessions (tenant, user_info) WHERE user_info <> '';
        """;

    private const string CreateToolCountsTable = """
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tool_counts')
        CREATE TABLE tool_counts (
            tenant    NVARCHAR(31) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
            tool_name NVARCHAR(255) NOT NULL,
            count     BIGINT NOT NULL DEFAULT 0,
            PRIMARY KEY (tenant, tool_name)
        );
        """;

    private const string CreateCountersTable = """
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'counters')
        CREATE TABLE counters (
            tenant  NVARCHAR(31) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
            name    NVARCHAR(63) NOT NULL,
            value   BIGINT NOT NULL DEFAULT 0,
            PRIMARY KEY (tenant, name)
        );
        """;

    private const string CreateGalleryTable = """
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'gallery')
        CREATE TABLE gallery (
            id          BIGINT IDENTITY(1,1) PRIMARY KEY,
            tenant      NVARCHAR(31) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
            name        NVARCHAR(255) NOT NULL,
            filename    NVARCHAR(255) NOT NULL,
            blob_url    NVARCHAR(MAX) NOT NULL,
            uploaded_at DATETIME2 DEFAULT GETUTCDATE(),
            INDEX idx_gallery_tenant (tenant)
        );
        """;

    private const string CreateGalleryTenantUploadedAtIndex = """
        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_gallery_tenant_uploaded_at' AND object_id = OBJECT_ID('gallery'))
        CREATE INDEX idx_gallery_tenant_uploaded_at ON gallery (tenant, uploaded_at, id);
        """;

    private const string CreateScreenshotsTable = """
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'screenshots')
        CREATE TABLE screenshots (
            id          BIGINT IDENTITY(1,1) PRIMARY KEY,
            tenant      NVARCHAR(31) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
            blob_url    NVARCHAR(MAX) NOT NULL,
            uploaded_at DATETIME2 DEFAULT GETUTCDATE(),
            INDEX idx_screenshots_tenant (tenant)
        );
        """;

    public static IReadOnlyList<string> KnownTableNames { get; } =
    [
        "sessions",
        "tool_counts",
        "counters",
        "gallery",
        "screenshots",
    ];

    private static readonly string[] SchemaBatches =
    [
        CreateSessionsTable,
        CreateSessionsTenantSessionIdIndex,
        CreateSessionsTenantUserInfoIndex,
        CreateToolCountsTable,
        CreateCountersTable,
        CreateGalleryTable,
        CreateGalleryTenantUploadedAtIndex,
        CreateScreenshotsTable,
    ];

    public static string BuildConnectionString(string server, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 60,
            Pooling = true,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
        };

        return builder.ConnectionString;
    }

    public static string NormalizeConnectionStringForPooling(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            Pooling = true
        };

        if (string.IsNullOrEmpty(builder.UserID) && string.IsNullOrEmpty(builder.Password)
            && builder.Authentication == SqlAuthenticationMethod.NotSpecified)
        {
            builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Creates a SqlConnection from a normalized, pooling-enabled connection string.
    /// </summary>
    public static SqlConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(NormalizeConnectionStringForPooling(connectionString));
    }

    /// <summary>
    /// Creates or verifies the schema. Each <c>CREATE TABLE</c> statement is guarded by
    /// <c>IF NOT EXISTS</c>, so it only runs when the table is absent.
    /// <para>
    /// The explicit <c>COLLATE SQL_Latin1_General_CP1_CI_AS</c> on every <c>tenant</c> column
    /// applies only to <b>freshly bootstrapped databases</b>.  Existing installations where the
    /// tables were already created without that collation will retain their original column
    /// collation; a schema migration (ALTER COLUMN + index rebuild) would be required to enforce
    /// case-insensitive tenant lookups in those environments.
    /// </para>
    /// </summary>
    public static async Task BootstrapAsync(string connectionString)
    {
        using var conn = CreateConnection(connectionString);
        await conn.OpenAsync();

        foreach (var batch in SchemaBatches)
        {
            using var cmd = new SqlCommand(batch.Trim(), conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
