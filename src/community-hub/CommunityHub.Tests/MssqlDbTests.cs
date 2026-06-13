using CommunityHub.Services;
using Microsoft.Data.SqlClient;

namespace CommunityHub.Tests;

public class MssqlDbTests
{
    [Fact]
    public void BuildConnectionString_EnablesPooling()
    {
        var connectionString = MssqlDb.BuildConnectionString("server.database.windows.net", "dashboard");

        var builder = new SqlConnectionStringBuilder(connectionString);
        Assert.True(builder.Pooling);
        Assert.Equal("server.database.windows.net", builder.DataSource);
        Assert.Equal("dashboard", builder.InitialCatalog);
        Assert.True(builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
        Assert.Equal(60, builder.ConnectTimeout);
        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, builder.Authentication);
    }

    [Fact]
    public void NormalizeConnectionStringForPooling_WithCredentials_EnablesPoolingAndKeepsCredentialAuth()
    {
        var normalized = MssqlDb.NormalizeConnectionStringForPooling(
            "Server=localhost;Database=dashboard;User Id=sa;Password=Password123!;Pooling=False");

        var builder = new SqlConnectionStringBuilder(normalized);
        Assert.True(builder.Pooling);
        Assert.Equal(SqlAuthenticationMethod.NotSpecified, builder.Authentication);
        Assert.Equal("sa", builder.UserID);
    }

    [Fact]
    public void NormalizeConnectionStringForPooling_WithoutCredentials_UsesManagedIdentityAuthentication()
    {
        var normalized = MssqlDb.NormalizeConnectionStringForPooling(
            "Server=server.database.windows.net;Database=dashboard;Encrypt=True;TrustServerCertificate=False;Pooling=False");

        var builder = new SqlConnectionStringBuilder(normalized);
        Assert.True(builder.Pooling);
        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, builder.Authentication);
    }

    [Fact]
    public void CreateConnection_NormalizesConnectionStringForPooling()
    {
        using var connection = MssqlDb.CreateConnection(
            "Server=localhost;Database=dashboard;User Id=sa;Password=Password123!;Pooling=False");

        Assert.Null(connection.AccessTokenCallback);
        Assert.True(new SqlConnectionStringBuilder(connection.ConnectionString).Pooling);
    }
}
