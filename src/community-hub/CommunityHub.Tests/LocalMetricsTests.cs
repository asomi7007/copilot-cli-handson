using CommunityHub.Services;

namespace CommunityHub.Tests;

public class LocalMetricsTests
{
    [Fact]
    public async Task Snapshot_InitialState_AllZeros()
    {
        var metrics = new LocalMetrics();
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
        var metrics = new LocalMetrics();

        await metrics.OnSessionStartAsync("s1", "u1");
        await metrics.OnSessionStartAsync("s2", "u1");
        await metrics.OnSessionStartAsync("s1", "u2"); // duplicate session

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.SessionCount);
        Assert.Equal(2, snap.UserCount);
    }

    [Fact]
    public async Task OnSessionStart_EmptySessionAndUser_NotTracked()
    {
        var metrics = new LocalMetrics();

        await metrics.OnSessionStartAsync("", "");
        var snap = await metrics.SnapshotAsync();
        Assert.Equal(0, snap.SessionCount);
        Assert.Equal(0, snap.UserCount);
    }

    [Fact]
    public async Task OnPromptSubmitted_IncrementsCounter()
    {
        var metrics = new LocalMetrics();

        await metrics.OnPromptSubmittedAsync();
        await metrics.OnPromptSubmittedAsync();

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.PromptSubmissions);
    }

    [Fact]
    public async Task OnToolUsed_TracksToolCalls()
    {
        var metrics = new LocalMetrics();

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
        var metrics = new LocalMetrics();

        await metrics.OnToolUsedAsync("");
        var snap = await metrics.SnapshotAsync();
        Assert.Equal(0, snap.ToolCalls);
        Assert.Empty(snap.ToolBreakdown);
    }

    [Fact]
    public async Task ToolBreakdown_SortedByCountDescThenName()
    {
        var metrics = new LocalMetrics();

        await metrics.OnToolUsedAsync("zulu");
        await metrics.OnToolUsedAsync("alpha");
        await metrics.OnToolUsedAsync("alpha");
        await metrics.OnToolUsedAsync("bravo");

        var snap = await metrics.SnapshotAsync();
        Assert.Equal("alpha", snap.ToolBreakdown[0].Name);
        Assert.Equal(2, snap.ToolBreakdown[0].Count);
        // bravo and zulu both have 1 count, sorted by name
        Assert.Equal("bravo", snap.ToolBreakdown[1].Name);
        Assert.Equal("zulu", snap.ToolBreakdown[2].Name);
    }

    [Fact]
    public async Task OnSubagentStop_IncrementsCounter()
    {
        var metrics = new LocalMetrics();

        await metrics.OnSubagentStopAsync();
        await metrics.OnSubagentStopAsync();

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.SubagentStops);
    }

    [Fact]
    public async Task OnAgentStop_IncrementsCounter()
    {
        var metrics = new LocalMetrics();

        await metrics.OnAgentStopAsync();
        await metrics.OnAgentStopAsync();

        var snap = await metrics.SnapshotAsync();
        Assert.Equal(2, snap.AgentStops);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var metrics = new LocalMetrics();
        var ex = Record.Exception(() => metrics.Dispose());
        Assert.Null(ex);
    }
}
