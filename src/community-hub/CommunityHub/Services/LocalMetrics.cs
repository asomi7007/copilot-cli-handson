using CommunityHub.Models;

namespace CommunityHub.Services;

public class LocalMetrics : IMetricsStore, IDisposable
{
    public string CurrentTenant => "local";

    private readonly ReaderWriterLockSlim _lock = new();
    private readonly HashSet<string> _sessions = [];
    private readonly HashSet<string> _users = [];
    private readonly Dictionary<string, int> _toolCounts = [];
    private int _promptSubmissions;
    private int _agentStops;
    private int _subagentStops;

    public Task OnSessionStartAsync(string sessionId, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!string.IsNullOrEmpty(sessionId))
                _sessions.Add(sessionId);
            if (!string.IsNullOrEmpty(userId))
                _users.Add(userId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public Task OnToolUsedAsync(string tool)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!string.IsNullOrEmpty(tool))
            {
                _toolCounts.TryGetValue(tool, out var count);
                _toolCounts[tool] = count + 1;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public Task OnPromptSubmittedAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            _promptSubmissions++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public Task OnSubagentStopAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            _subagentStops++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public Task OnAgentStopAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            _agentStops++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public Task<ActivitySnapshot> SnapshotAsync(string? tenant = null)
    {
        if (!IsCurrentTenant(tenant))
            return Task.FromResult(new ActivitySnapshot());

        _lock.EnterReadLock();
        try
        {
            var breakdown = _toolCounts
                .Select(kv => new ToolCount { Name = kv.Key, Count = kv.Value })
                .OrderByDescending(tc => tc.Count)
                .ThenBy(tc => tc.Name)
                .ToList();

            return Task.FromResult(new ActivitySnapshot
            {
                SessionCount = _sessions.Count,
                UserCount = _users.Count,
                PromptSubmissions = _promptSubmissions,
                ToolCalls = breakdown.Sum(tool => tool.Count),
                AgentStops = _agentStops,
                SubagentStops = _subagentStops,
                ToolBreakdown = breakdown
            });
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<ActivitySnapshot> AllTenantsSnapshotAsync() => SnapshotAsync();

    public async Task<(ActivitySnapshot Tenant, ActivitySnapshot AllTenants)> SnapshotWithAllTenantsAsync(string? tenant = null)
    {
        var snap = await SnapshotAsync(tenant);
        return (snap, CloneSnapshot(snap));
    }

    public Task<List<string>> ListTenantsAsync() => Task.FromResult(new List<string> { CurrentTenant });

    public Task DeleteTenantDataAsync(string tenant)
    {
        if (!IsCurrentTenant(tenant))
            return Task.CompletedTask;

        _lock.EnterWriteLock();
        try
        {
            _sessions.Clear();
            _users.Clear();
            _toolCounts.Clear();
            _promptSubmissions = 0;
            _agentStops = 0;
            _subagentStops = 0;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllDataAsync() => DeleteTenantDataAsync(CurrentTenant);

    private bool IsCurrentTenant(string? tenant) =>
        string.IsNullOrWhiteSpace(tenant) || string.Equals(tenant, CurrentTenant, StringComparison.OrdinalIgnoreCase);

    private static ActivitySnapshot CloneSnapshot(ActivitySnapshot snapshot) => new()
    {
        SessionCount = snapshot.SessionCount,
        UserCount = snapshot.UserCount,
        PromptSubmissions = snapshot.PromptSubmissions,
        ToolCalls = snapshot.ToolCalls,
        DistinctToolsCalled = snapshot.DistinctToolsCalled,
        AgentStops = snapshot.AgentStops,
        SubagentStops = snapshot.SubagentStops,
        UploadedGames = snapshot.UploadedGames,
        ToolBreakdown = snapshot.ToolBreakdown
            .Select(tool => new ToolCount { Name = tool.Name, Count = tool.Count })
            .ToList()
    };

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
