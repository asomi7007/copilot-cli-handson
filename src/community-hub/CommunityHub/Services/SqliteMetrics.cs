using CommunityHub.Models;
using Microsoft.Data.Sqlite;

namespace CommunityHub.Services;

/// <summary>
/// SQLite port of <see cref="SqlMetrics"/>. Same IMetricsStore behavior, backed by
/// a local SQLite database file instead of Azure SQL.
/// </summary>
public class SqliteMetrics(string connectionString, string tenant) : IMetricsStore
{
    public string CurrentTenant => tenant;

    private async Task<SqliteConnection> OpenAsync()
    {
        var db = SqliteDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using (var pragma = db.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync();
        }
        return db;
    }

    public async Task OnSessionStartAsync(string sessionId, string userId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions (tenant, session_id, user_info) VALUES ($t, $s, $u)";
        cmd.Parameters.AddWithValue("$t", tenant);
        cmd.Parameters.AddWithValue("$s", sessionId);
        cmd.Parameters.AddWithValue("$u", userId ?? "");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task OnToolUsedAsync(string tool)
    {
        if (string.IsNullOrEmpty(tool))
            return;
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tool_counts (tenant, tool_name, count) VALUES ($t, $n, 1)
            ON CONFLICT(tenant, tool_name) DO UPDATE SET count = count + 1
            """;
        cmd.Parameters.AddWithValue("$t", tenant);
        cmd.Parameters.AddWithValue("$n", tool);
        await cmd.ExecuteNonQueryAsync();
    }

    public Task OnPromptSubmittedAsync() => IncrementCounterAsync("prompt_submissions");
    public Task OnSubagentStopAsync() => IncrementCounterAsync("subagent_stops");
    public Task OnAgentStopAsync() => IncrementCounterAsync("agent_stops");

    private async Task IncrementCounterAsync(string name)
    {
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO counters (tenant, name, value) VALUES ($t, $n, 1)
            ON CONFLICT(tenant, name) DO UPDATE SET value = value + 1
            """;
        cmd.Parameters.AddWithValue("$t", tenant);
        cmd.Parameters.AddWithValue("$n", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<ActivitySnapshot> SnapshotAsync(string? requestedTenant = null)
    {
        var (tenantSnap, _) = await SnapshotWithAllTenantsAsync(requestedTenant);
        return tenantSnap;
    }

    public async Task<ActivitySnapshot> AllTenantsSnapshotAsync()
    {
        var (_, allSnap) = await SnapshotWithAllTenantsAsync();
        return allSnap;
    }

    public async Task<(ActivitySnapshot Tenant, ActivitySnapshot AllTenants)> SnapshotWithAllTenantsAsync(string? requestedTenant = null)
    {
        var selectedTenant = TenantOrDefault(requestedTenant);
        using var db = await OpenAsync();

        var (tSessions, tUsers, aSessions, aUsers) = await GetCombinedSessionCountsAsync(db, selectedTenant);
        var (tCounters, aCounters) = await GetCombinedCountersAsync(db, selectedTenant);
        var (tTools, aTools) = await GetCombinedToolBreakdownAsync(db, selectedTenant);

        var tenantSnap = BuildSnapshot(tSessions, tUsers, tCounters, tTools);
        var allSnap = BuildSnapshot(aSessions, aUsers, aCounters, aTools);
        return (tenantSnap, allSnap);
    }

    private static ActivitySnapshot BuildSnapshot(
        int sessionCount, int userCount,
        List<(string Name, int Value)> counters,
        List<ToolCount> toolBreakdown)
    {
        var snap = new ActivitySnapshot
        {
            SessionCount = sessionCount,
            UserCount = userCount
        };

        foreach (var (name, value) in counters)
        {
            switch (name)
            {
                case "prompt_submissions": snap.PromptSubmissions = value; break;
                case "agent_stops": snap.AgentStops = value; break;
                case "subagent_stops": snap.SubagentStops = value; break;
            }
        }

        snap.ToolBreakdown = toolBreakdown
            .OrderByDescending(tc => tc.Count)
            .ThenBy(tc => tc.Name)
            .ToList();
        snap.ToolCalls = snap.ToolBreakdown.Sum(tc => tc.Count);
        return snap;
    }

    public async Task<List<string>> ListTenantsAsync()
    {
        using var db = await OpenAsync();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT tenant FROM sessions
            UNION SELECT tenant FROM counters
            UNION SELECT tenant FROM tool_counts
            UNION SELECT tenant FROM gallery
            ORDER BY tenant
            """;
        var results = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        results.RemoveAll(t => string.Equals(t, CurrentTenant, StringComparison.OrdinalIgnoreCase));
        results.Insert(0, CurrentTenant);
        return results;
    }

    private static async Task<(int TenantSessions, int TenantUsers, int AllSessions, int AllUsers)> GetCombinedSessionCountsAsync(SqliteConnection db, string selectedTenant)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(DISTINCT session_id) FROM sessions WHERE tenant = $t),
                (SELECT COUNT(DISTINCT user_info) FROM sessions WHERE tenant = $t AND user_info <> ''),
                (SELECT COUNT(*) FROM (SELECT 1 FROM sessions GROUP BY tenant, session_id)),
                (SELECT COUNT(*) FROM (SELECT 1 FROM sessions WHERE user_info <> '' GROUP BY tenant, user_info))
            """;
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (
                reader.IsDBNull(0) ? 0 : (int)reader.GetInt64(0),
                reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1),
                reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2),
                reader.IsDBNull(3) ? 0 : (int)reader.GetInt64(3)
            );
        }
        return (0, 0, 0, 0);
    }

    private static async Task<(List<(string Name, int Value)> Tenant, List<(string Name, int Value)> All)> GetCombinedCountersAsync(SqliteConnection db, string selectedTenant)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT name,
                   SUM(CASE WHEN tenant = $t THEN value ELSE 0 END),
                   SUM(value)
            FROM counters
            GROUP BY name
            """;
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        var tenantCounters = new List<(string, int)>();
        var allCounters = new List<(string, int)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            tenantCounters.Add((name, reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1)));
            allCounters.Add((name, reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2)));
        }
        return (tenantCounters, allCounters);
    }

    private static async Task<(List<ToolCount> Tenant, List<ToolCount> All)> GetCombinedToolBreakdownAsync(SqliteConnection db, string selectedTenant)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT tool_name,
                   SUM(CASE WHEN tenant = $t THEN count ELSE 0 END),
                   SUM(count)
            FROM tool_counts
            GROUP BY tool_name
            """;
        cmd.Parameters.AddWithValue("$t", selectedTenant);
        var tenantTools = new List<ToolCount>();
        var allTools = new List<ToolCount>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var toolName = reader.GetString(0);
            var tenantCount = reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1);
            var allCount = reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2);
            if (tenantCount > 0)
                tenantTools.Add(new ToolCount { Name = toolName, Count = tenantCount });
            allTools.Add(new ToolCount { Name = toolName, Count = allCount });
        }
        return (tenantTools, allTools);
    }

    private string TenantOrDefault(string? requestedTenant) =>
        string.IsNullOrWhiteSpace(requestedTenant) ? CurrentTenant : requestedTenant;
}
