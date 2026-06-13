using CommunityHub.Services;

namespace CommunityHub.Tests;

public class TenantHelpersTests
{
    [Fact]
    public void MergeTenants_FiltersInvalidTenants()
    {
        var tenants = TenantHelpers.MergeTenants(["tenant-a", "bad_tenant", "", "Lab-502", "tenant-b"]);

        Assert.Equal(["tenant-a", "Lab-502", "tenant-b"], tenants);
    }

    [Fact]
    public void MergeTenants_PreservesFirstSeenOrder()
    {
        var tenants = TenantHelpers.MergeTenants(["tenant-b", "tenant-a"], ["tenant-c"]);

        Assert.Equal(["tenant-b", "tenant-a", "tenant-c"], tenants);
    }

    [Fact]
    public void MergeTenants_DeduplicatesCaseVariants()
    {
        var tenants = TenantHelpers.MergeTenants(["Tenant-A", "TENANT-A", "tenant-a"]);

        Assert.Single(tenants);
        Assert.Equal("Tenant-A", tenants[0]); // first seen is kept
    }

    [Theory]
    [InlineData("MYTENANT")]
    [InlineData("MyTenant")]
    [InlineData("Lab-502")]
    [InlineData("A")]
    public void IsValidTenant_AcceptsUppercaseTenant(string tenant)
    {
        Assert.True(TenantHelpers.IsValidTenant(tenant));
    }
}
