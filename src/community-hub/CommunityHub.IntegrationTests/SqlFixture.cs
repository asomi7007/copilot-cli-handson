using Microsoft.Data.SqlClient;
using CommunityHub.Services;

namespace CommunityHub.IntegrationTests;

/// <summary>
/// Creates a dedicated test database before all SQL integration tests run
/// and drops it afterwards. Each test should use a unique tenant string to
/// avoid cross-test data interference.
/// </summary>
public class SqlFixture : IAsyncLifetime
{
    private readonly string _masterCs;
    private readonly string _dbName;

    /// <summary>Connection string pointing at the per-run test database.</summary>
    public string ConnectionString { get; private set; } = "";

    public SqlFixture()
    {
        var raw = Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "MSSQL_CONNECTION_STRING environment variable is not set. " +
                "Provide a connection string to an SA-level account, e.g.: " +
                "Server=localhost,1433;Database=master;User Id=sa;Password=...;TrustServerCertificate=True;");

        // Normalise to master so we can CREATE DATABASE
        var builder = new SqlConnectionStringBuilder(raw) { InitialCatalog = "master" };
        _masterCs = builder.ConnectionString;
        _dbName = "inttest_" + Guid.NewGuid().ToString("N")[..12];
    }

    public async Task InitializeAsync()
    {
        using (var conn = new SqlConnection(_masterCs))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_dbName}] COLLATE SQL_Latin1_General_CP1_CI_AS";
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(_masterCs) { InitialCatalog = _dbName };
        ConnectionString = builder.ConnectionString;

        await MssqlDb.BootstrapAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        // Clear the connection pool so SQL Server can set the database to single-user
        SqlConnection.ClearAllPools();

        using var conn = new SqlConnection(_masterCs);
        await conn.OpenAsync();

        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
        await cmd1.ExecuteNonQueryAsync();

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = $"DROP DATABASE [{_dbName}]";
        await cmd2.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("Sql")]
public class SqlCollection : ICollectionFixture<SqlFixture> { }
