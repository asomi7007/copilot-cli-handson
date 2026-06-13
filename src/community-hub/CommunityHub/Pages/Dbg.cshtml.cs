using System.Data.Common;
using System.Reflection;
using CommunityHub.Config;
using CommunityHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CommunityHub.Pages;

public class DbgModel(IDebugSqlDumper sqlDumper, IDebugDataCleaner dataCleaner, ILogger<DbgModel> logger) : PageModel
{
    public string Version { get; private set; } = "dev";
    public bool HasSqlDump { get; private set; }
    public DebugDbInfo? DbInfo { get; private set; }
    public List<DebugTableDump> Tables { get; private set; } = [];
    public List<string> MissingTables { get; private set; } = [];
    public List<string> TenantOptions { get; private set; } = [];
    public string SelectedTenant { get; private set; } = "";
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var gate = EnsureDebugEnabled();
        if (gate is not null)
            return gate;

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteTenantAsync(string tenant)
    {
        var gate = EnsureDebugEnabled();
        if (gate is not null)
            return gate;

        SelectedTenant = tenant;
        if (string.IsNullOrWhiteSpace(tenant) || !TenantHelpers.IsValidTenant(tenant))
        {
            ErrorMessage = "Invalid tenant.";
            await LoadAsync();
            return Page();
        }

        try
        {
            await dataCleaner.DeleteTenantAsync(tenant);
            StatusMessage = $"Deleted data for tenant '{tenant}'.";
            SelectedTenant = dataCleaner.CurrentTenant;
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Failed to delete debug data for tenant {Tenant}", tenant);
            ErrorMessage = $"Failed to delete data for tenant '{tenant}'.";
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to delete debug data for tenant {Tenant}", tenant);
            ErrorMessage = $"Failed to delete data for tenant '{tenant}'.";
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAllAsync()
    {
        var gate = EnsureDebugEnabled();
        if (gate is not null)
            return gate;

        try
        {
            await dataCleaner.DeleteAllAsync();
            StatusMessage = "Deleted data for all tenants.";
            SelectedTenant = dataCleaner.CurrentTenant;
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Failed to delete debug data for all tenants");
            ErrorMessage = "Failed to delete data for all tenants.";
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to delete debug data for all tenants");
            ErrorMessage = "Failed to delete data for all tenants.";
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateTablesAsync()
    {
        var gate = EnsureDebugEnabled();
        if (gate is not null)
            return gate;

        try
        {
            var missingBefore = await sqlDumper.GetMissingTablesAsync();
            await sqlDumper.CreateTablesAsync();
            StatusMessage = missingBefore.Count == 0
                ? "No missing tables found; schema is already up to date."
                : $"Created {missingBefore.Count} missing table(s): {string.Join(", ", missingBefore)}.";
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Failed to bootstrap schema");
            ErrorMessage = "Failed to create tables.";
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to bootstrap schema");
            ErrorMessage = "Failed to create tables.";
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDropTableAsync(string schema, string table)
    {
        var gate = EnsureDebugEnabled();
        if (gate is not null)
            return gate;

        try
        {
            await sqlDumper.DropTableAsync(schema, table);
            StatusMessage = $"Dropped table '{schema}.{table}'.";
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid drop table request for {Schema}.{Table}", schema, table);
            ErrorMessage = "Invalid table.";
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Failed to drop table {Schema}.{Table}", schema, table);
            ErrorMessage = $"Failed to drop table '{schema}.{table}'.";
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to drop table {Schema}.{Table}", schema, table);
            ErrorMessage = $"Failed to drop table '{schema}.{table}'.";
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Version = typeof(AppConfig).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";
        HasSqlDump = sqlDumper.IsAvailable;
        TenantOptions = TenantHelpers.MergeTenants(await dataCleaner.ListTenantsAsync(), [SelectedTenant]);
        SelectedTenant = TenantHelpers.ResolveTenant(SelectedTenant, dataCleaner.CurrentTenant);

        if (HasSqlDump)
        {
            DbInfo = await sqlDumper.GetDbInfoAsync();
            Tables = await sqlDumper.DumpAsync();
            MissingTables = await sqlDumper.GetMissingTablesAsync();
        }
    }

    private IActionResult? EnsureDebugEnabled()
    {
        if (Environment.GetEnvironmentVariable("DBG") is not null)
            return null;

        logger.LogWarning(
            "Debug page access returned 404 because DBG is not set. RemoteIp={RemoteIp}",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        return NotFound();
    }
}
