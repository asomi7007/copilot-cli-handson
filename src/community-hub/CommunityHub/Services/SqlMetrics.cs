using CommunityHub.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CommunityHub.Services;

public class SqlMetrics(string connectionString, string tenant) : IMetricsStore
{
    public string CurrentTenant => tenant;

    private async Task ExecuteInTransactionAsync(Func<SqlConnection, SqlTransaction, Task> action)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var tx = db.BeginTransaction();
        try
        {
            await action(db, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task OnSessionStartAsync(string sessionId, string userId)
    {
        await ExecuteInTransactionAsync(async (db, tx) =>
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                using var cmd = new SqlCommand(
                    "INSERT INTO sessions (tenant, session_id, user_info) VALUES (@p1, @p2, @p3)", db, tx);
                cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = tenant });
                cmd.Parameters.Add(new SqlParameter("@p2", SqlDbType.NVarChar, 255) { Value = sessionId });
                cmd.Parameters.Add(new SqlParameter("@p3", SqlDbType.NVarChar, 255) { Value = userId ?? "" });
                await cmd.ExecuteNonQueryAsync();
            }
        });
    }

    public async Task OnToolUsedAsync(string tool)
    {
        await ExecuteInTransactionAsync(async (db, tx) =>
        {
            if (!string.IsNullOrEmpty(tool))
            {
                using var cmd = new SqlCommand("""
                    MERGE tool_counts WITH (HOLDLOCK) AS target
                    USING (SELECT @p1 AS tenant, @p2 AS tool_name) AS source
                    ON target.tenant = source.tenant AND target.tool_name = source.tool_name
                    WHEN MATCHED THEN UPDATE SET count = target.count + 1
                    WHEN NOT MATCHED THEN INSERT (tenant, tool_name, count) VALUES (source.tenant, source.tool_name, 1);
                    """, db, tx);
                cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = tenant });
                cmd.Parameters.Add(new SqlParameter("@p2", SqlDbType.NVarChar, 255) { Value = tool });
                await cmd.ExecuteNonQueryAsync();
            }
        });
    }

    public async Task OnPromptSubmittedAsync()
    {
        await IncrementCounterAsync("prompt_submissions");
    }

    public async Task OnSubagentStopAsync()
    {
        await IncrementCounterAsync("subagent_stops");
    }

    public async Task OnAgentStopAsync()
    {
        await IncrementCounterAsync("agent_stops");
    }

    private async Task IncrementCounterAsync(string name)
    {
        await ExecuteInTransactionAsync(async (db, tx) =>
        {
            using var cmd = new SqlCommand("""
                MERGE counters WITH (HOLDLOCK) AS target
                USING (SELECT @p1 AS tenant, @p2 AS name) AS source
                ON target.tenant = source.tenant AND target.name = source.name
                WHEN MATCHED THEN UPDATE SET value = target.value + 1
                WHEN NOT MATCHED THEN INSERT (tenant, name, value) VALUES (source.tenant, source.name, 1);
                """, db, tx);
            cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = tenant });
            cmd.Parameters.Add(new SqlParameter("@p2", SqlDbType.NVarChar, 63) { Value = name });
            await cmd.ExecuteNonQueryAsync();
        });
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

        // Run all three combined queries concurrently (3 round-trips instead of 8)
        var sessionsTask = GetCombinedSessionCountsAsync(selectedTenant);
        var countersTask = GetCombinedCountersAsync(selectedTenant);
        var toolsTask = GetCombinedToolBreakdownAsync(selectedTenant);

        await Task.WhenAll(sessionsTask, countersTask, toolsTask);

        var (tenantSessions, tenantUsers, allSessions, allUsers) = await sessionsTask;
        var (tenantCounters, allCounters) = await countersTask;
        var (tenantTools, allTools) = await toolsTask;

        var tenantSnap = BuildSnapshot(tenantSessions, tenantUsers, tenantCounters, tenantTools);
        var allSnap = BuildSnapshot(allSessions, allUsers, allCounters, allTools);

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
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT tenant FROM sessions WITH (NOLOCK)
            UNION
            SELECT tenant FROM counters WITH (NOLOCK)
            UNION
            SELECT tenant FROM tool_counts WITH (NOLOCK)
            UNION
            SELECT tenant FROM gallery WITH (NOLOCK)
            ORDER BY tenant
            """, db);
        var results = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        results.RemoveAll(t => string.Equals(t, CurrentTenant, StringComparison.OrdinalIgnoreCase));
        results.Insert(0, CurrentTenant);
        return results;
    }

    private async Task<(int TenantSessions, int TenantUsers, int AllSessions, int AllUsers)> GetCombinedSessionCountsAsync(string selectedTenant)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT
                COUNT(DISTINCT CASE WHEN tenant = @p1 THEN session_id END),
                COUNT(DISTINCT CASE WHEN tenant = @p1 AND user_info != '' THEN user_info END),
                (SELECT COUNT(*) FROM (SELECT tenant, session_id FROM sessions WITH (NOLOCK) GROUP BY tenant, session_id) AS s),
                (SELECT COUNT(*) FROM (SELECT tenant, user_info FROM sessions WITH (NOLOCK) WHERE user_info != '' GROUP BY tenant, user_info) AS u)
            FROM sessions WITH (NOLOCK)
            """, db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (
                reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            );
        }
        return (0, 0, 0, 0);
    }

    private async Task<(List<(string Name, int Value)> Tenant, List<(string Name, int Value)> All)> GetCombinedCountersAsync(string selectedTenant)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT name,
                   SUM(CASE WHEN tenant = @p1 THEN value ELSE 0 END),
                   SUM(value)
            FROM counters WITH (NOLOCK)
            GROUP BY name
            """, db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        var tenantCounters = new List<(string, int)>();
        var allCounters = new List<(string, int)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            tenantCounters.Add((name, ToInt32Checked(reader.GetInt64(1), "counters.tenant_sum")));
            allCounters.Add((name, ToInt32Checked(reader.GetInt64(2), "counters.total_sum")));
        }
        return (tenantCounters, allCounters);
    }

    private async Task<(List<ToolCount> Tenant, List<ToolCount> All)> GetCombinedToolBreakdownAsync(string selectedTenant)
    {
        using var db = MssqlDb.CreateConnection(connectionString);
        await db.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT tool_name,
                   SUM(CASE WHEN tenant = @p1 THEN count ELSE 0 END),
                   SUM(count)
            FROM tool_counts WITH (NOLOCK)
            GROUP BY tool_name
            """, db);
        cmd.Parameters.Add(new SqlParameter("@p1", SqlDbType.NVarChar, 31) { Value = selectedTenant });
        var tenantTools = new List<ToolCount>();
        var allTools = new List<ToolCount>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var toolName = reader.GetString(0);
            var tenantCount = ToInt32Checked(reader.GetInt64(1), "tool_counts.tenant_sum");
            var allCount = ToInt32Checked(reader.GetInt64(2), "tool_counts.total_sum");
            if (tenantCount > 0)
                tenantTools.Add(new ToolCount { Name = toolName, Count = tenantCount });
            allTools.Add(new ToolCount { Name = toolName, Count = allCount });
        }
        return (tenantTools, allTools);
    }

    private static int ToInt32Checked(long value, string fieldName)
    {
        if (value < int.MinValue || value > int.MaxValue)
            throw new OverflowException($"Value '{value}' for '{fieldName}' exceeds Int32 range.");
        return (int)value;
    }

    private string TenantOrDefault(string? requestedTenant) =>
        string.IsNullOrWhiteSpace(requestedTenant) ? CurrentTenant : requestedTenant;
}
