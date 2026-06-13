using System.Data;
using Microsoft.Data.SqlClient;

namespace CommunityHub.Services;

public interface IDebugSqlDumper
{
    bool IsAvailable { get; }
    Task<DebugDbInfo?> GetDbInfoAsync();
    Task<List<DebugTableDump>> DumpAsync();
    Task<List<string>> GetMissingTablesAsync();
    Task CreateTablesAsync();
    Task DropTableAsync(string schema, string table);
}

public sealed record DebugTableDump(string Schema, string Name, List<string> Columns, List<List<string>> Rows);

public sealed record DebugDbInfo(string DatabaseName, string Collation)
{
    public bool? IsCaseInsensitive => Collation switch
    {
        _ when Collation.Contains("_CI_", StringComparison.OrdinalIgnoreCase) => true,
        _ when Collation.Contains("_CS_", StringComparison.OrdinalIgnoreCase) => false,
        _ => null
    };
}

public sealed class NoSqlDebugDumper : IDebugSqlDumper
{
    public bool IsAvailable => false;

    public Task<DebugDbInfo?> GetDbInfoAsync() => Task.FromResult<DebugDbInfo?>(null);

    public Task<List<DebugTableDump>> DumpAsync() => Task.FromResult(new List<DebugTableDump>());

    public Task<List<string>> GetMissingTablesAsync() => Task.FromResult(new List<string>());

    public Task CreateTablesAsync() => Task.CompletedTask;

    public Task DropTableAsync(string schema, string table) => Task.CompletedTask;
}

public sealed class SqlDebugDumper(string connectionString) : IDebugSqlDumper
{
    public bool IsAvailable => true;

    public async Task<DebugDbInfo?> GetDbInfoAsync()
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();

        using var cmd = new SqlCommand(
            "SELECT DB_NAME(), CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(128))", db);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var dbName = reader.GetString(0);
            var collation = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
            return new DebugDbInfo(dbName, collation);
        }

        return null;
    }

    public async Task<List<DebugTableDump>> DumpAsync()
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();

        var tables = new List<(string Schema, string Name)>();
        using (var cmd = new SqlCommand("""
            SELECT s.name, t.name
            FROM sys.tables t WITH (NOLOCK)
            JOIN sys.schemas s WITH (NOLOCK) ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name
            """, db))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        var dumps = new List<DebugTableDump>();
        foreach (var table in tables)
        {
            var sql = $"SELECT * FROM {QuoteIdentifier(table.Schema)}.{QuoteIdentifier(table.Name)} WITH (NOLOCK)";
            using var cmd = new SqlCommand(sql, db);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToList();
            var rows = new List<List<string>>();

            while (await reader.ReadAsync())
            {
                var row = new List<string>(reader.FieldCount);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row.Add(await ReadValueAsync(reader, index));
                }
                rows.Add(row);
            }

            dumps.Add(new DebugTableDump(table.Schema, table.Name, columns, rows));
        }

        return dumps;
    }

    public async Task<List<string>> GetMissingTablesAsync()
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = new SqlCommand(
            "SELECT name FROM sys.tables WHERE is_ms_shipped = 0", db);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            existing.Add(reader.GetString(0));

        return MssqlDb.KnownTableNames
            .Where(t => !existing.Contains(t))
            .ToList();
    }

    public Task CreateTablesAsync() => MssqlDb.BootstrapAsync(connectionString);

    public async Task DropTableAsync(string schema, string table)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("Schema and table are required.");

        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();

        using (var exists = new SqlCommand("""
            SELECT COUNT(*)
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @schema AND t.name = @table AND t.is_ms_shipped = 0
            """, db))
        {
            exists.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            exists.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
            var count = Convert.ToInt32(await exists.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            if (count == 0)
                throw new InvalidOperationException("Table does not exist.");
        }

        var sql = $"DROP TABLE {QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";
        using var cmd = new SqlCommand(sql, db);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string value) => "[" + value.Replace("]", "]]") + "]";

    private static async Task<string> ReadValueAsync(SqlDataReader reader, int index)
    {
        if (await reader.IsDBNullAsync(index))
            return "NULL";

        var value = reader.GetValue(index);
        return value switch
        {
            DateTime dateTime => dateTime.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };
    }
}
