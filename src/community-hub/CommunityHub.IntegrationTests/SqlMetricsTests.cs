using CommunityHub.Services;

namespace CommunityHub.IntegrationTests;

[Collection("Sql")]
public class SqlMetricsTests(SqlFixture fixture)
{
    // Each test gets a fresh tenant so rows from one test don't affect another.
    private SqlMetrics NewMetrics() => new(fixture.ConnectionString, UniqueId());
    private static string UniqueId() => "t" + Guid.NewGuid().ToString("N")[..7];

    [Fact]
    public async Task Snapshot_InitialState_AllZeros()
    {
        var metrics = NewMetrics();
        var snap = await metrics.SnapshotAsync();

        Assert.Equal(0, snap.SessionCount);
        Assert.Equal(0, snap.UserCount);
        Assert.Equal(0, snap.PromptSubmissions);
        Assert.Equal(0, snap.ToolCalls);
        Assert.Equal(0, snap.AgentStops);
        Assert.Equal(0, snap.SubagentStops);
        Assert.Empty(snap.ToolBreakdown);
    }

    [Fact]
    public async Task OnSessionStart_TracksSessions()
    {
        var metrics = NewMetrics();

        await metrics.OnSessionStartAsync("s1", "u1");
        await metrics.OnSessionStartAsync("s2", "u1");
        await metrics.OnSessionStartAsync("s1", "u2"); // same session_id: counted once

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.SessionCount); // DISTINCT session_id
        Assert.Equal(2, snap.UserCount);    // DISTINCT user_info
    }

    [Fact]
    public async Task OnSessionStart_DuplicateSessionAndUser_CountedOnce()
    {
        var metrics = NewMetrics();

        await metrics.OnSessionStartAsync("s1", "u1");
        await metrics.OnSessionStartAsync("s1", "u1"); // exact duplicate

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(1, snap.SessionCount);
        Assert.Equal(1, snap.UserCount);
    }

    [Fact]
    public async Task OnSessionStart_EmptySessionId_NotTracked()
    {
        var metrics = NewMetrics();

        await metrics.OnSessionStartAsync("", "u1");
        var snap = await metrics.SnapshotAsync();
        Assert.Equal(0, snap.SessionCount);
    }

    [Fact]
    public async Task OnPromptSubmitted_IncrementsCounter()
    {
        var metrics = NewMetrics();

        await metrics.OnPromptSubmittedAsync();
        await metrics.OnPromptSubmittedAsync();

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.PromptSubmissions);
    }

    [Fact]
    public async Task OnToolUsed_TracksToolCalls()
    {
        var metrics = NewMetrics();

        await metrics.OnToolUsedAsync("tool-a");
        await metrics.OnToolUsedAsync("tool-a");
        await metrics.OnToolUsedAsync("tool-b");

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(3, snap.ToolCalls);
        Assert.Equal(2, snap.ToolBreakdown.Count);
        Assert.Equal("tool-a", snap.ToolBreakdown[0].Name);
        Assert.Equal(2, snap.ToolBreakdown[0].Count);
        Assert.Equal("tool-b", snap.ToolBreakdown[1].Name);
        Assert.Equal(1, snap.ToolBreakdown[1].Count);
    }

    [Fact]
    public async Task OnToolUsed_EmptyTool_IsIgnored()
    {
        var metrics = NewMetrics();

        await metrics.OnToolUsedAsync("");
        var snap = await metrics.SnapshotAsync();
        Assert.Equal(0, snap.ToolCalls);
        Assert.Empty(snap.ToolBreakdown);
    }

    [Fact]
    public async Task ToolBreakdown_SortedByCountDescThenName()
    {
        var metrics = NewMetrics();

        await metrics.OnToolUsedAsync("zulu");
        await metrics.OnToolUsedAsync("alpha");
        await metrics.OnToolUsedAsync("alpha");
        await metrics.OnToolUsedAsync("bravo");

        var snap = await metrics.SnapshotAsync();
        Assert.Equal("alpha", snap.ToolBreakdown[0].Name);
        Assert.Equal(2, snap.ToolBreakdown[0].Count);
        // bravo and zulu both have count 1, sorted by name ascending
        Assert.Equal("bravo", snap.ToolBreakdown[1].Name);
        Assert.Equal("zulu", snap.ToolBreakdown[2].Name);
    }

    [Fact]
    public async Task OnSubagentStop_IncrementsCounter()
    {
        var metrics = NewMetrics();

        await metrics.OnSubagentStopAsync();
        await metrics.OnSubagentStopAsync();

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.SubagentStops);
    }

    [Fact]
    public async Task OnAgentStop_IncrementsCounter()
    {
        var metrics = NewMetrics();

        await metrics.OnAgentStopAsync();
        await metrics.OnAgentStopAsync();

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.AgentStops);
    }

    [Fact]
    public async Task TenantIsolation_MetricsAreSeparate()
    {
        var tenantA = new SqlMetrics(fixture.ConnectionString, UniqueId());
        var tenantB = new SqlMetrics(fixture.ConnectionString, UniqueId());

        await tenantA.OnSessionStartAsync("s1", "u1");
        await tenantA.OnToolUsedAsync("tool-a");
        await tenantA.OnSubagentStopAsync();

        var snapA = await tenantA.SnapshotAsync();
        var snapB = await tenantB.SnapshotAsync();

        Assert.Equal(1, snapA.SessionCount);
        Assert.Equal(1, snapA.ToolCalls);
        Assert.Equal(1, snapA.SubagentStops);

        Assert.Equal(0, snapB.SessionCount);
        Assert.Equal(0, snapB.ToolCalls);
        Assert.Equal(0, snapB.SubagentStops);
    }

    [Fact]
    public async Task Snapshot_TenantLookupIsCaseInsensitive()
    {
        var lowerTenant = UniqueId();                       // e.g. "t3a7b2c"
        var upperTenant = lowerTenant.ToUpperInvariant();   // e.g. "T3A7B2C"

        var metricsLower = new SqlMetrics(fixture.ConnectionString, lowerTenant);
        await metricsLower.OnSessionStartAsync("s1", "u1");
        await metricsLower.OnToolUsedAsync("tool-a");
        await metricsLower.OnSubagentStopAsync();

        // Query using the uppercase variant – SQL Server CI collation should match the rows.
        var metricsUpper = new SqlMetrics(fixture.ConnectionString, upperTenant);
        var snap = await metricsUpper.SnapshotAsync();

        Assert.Equal(1, snap.SessionCount);
        Assert.Equal(1, snap.ToolCalls);
        Assert.Equal(1, snap.SubagentStops);
    }

    [Fact]
    public async Task ListTenants_WithCaseMismatchInDb_NoDuplicateCurrentTenant()
    {
        var lowerTenant = UniqueId();
        var upperTenant = lowerTenant.ToUpperInvariant();
        var otherTenant = UniqueId();

        // Write data under the lowercase tenant name.
        var metricsLower = new SqlMetrics(fixture.ConnectionString, lowerTenant);
        await metricsLower.OnSessionStartAsync("s1", "u1");

        // Write data under a second distinct tenant to verify DB rows are actually read.
        var metricsOther = new SqlMetrics(fixture.ConnectionString, otherTenant);
        await metricsOther.OnSessionStartAsync("s2", "u2");

        // List tenants from the uppercase perspective.
        var metricsUpper = new SqlMetrics(fixture.ConnectionString, upperTenant);
        var tenants = await metricsUpper.ListTenantsAsync();

        // The logical tenant must appear exactly once (no case-variant duplicate).
        var matchCount = tenants.Count(t => string.Equals(t, lowerTenant, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, matchCount);

        // The current tenant (uppercase) must be first in the list.
        Assert.Equal(upperTenant, tenants[0]);

        // The second distinct tenant must also be present, proving DB rows were actually read.
        Assert.Contains(otherTenant, tenants);
    }
}
