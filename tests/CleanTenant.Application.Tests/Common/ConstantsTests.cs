using CleanTenant.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Application.Tests.Common;

public class CacheKeysTests
{
    [Fact]
    public void Session_ShouldReturnFormattedKey()
    {
        var userId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var key = CacheKeys.Session(userId);
        key.Should().Be("ct:session:12345678-1234-1234-1234-123456789012");
    }

    [Fact]
    public void UserRoles_ShouldContainPrefix()
    {
        var key = CacheKeys.UserRoles(Guid.NewGuid());
        key.Should().StartWith("ct:user:");
        key.Should().EndWith(":roles");
    }

    [Fact]
    public void IpBlacklist_ShouldBeConstant()
    {
        CacheKeys.IpBlacklist.Should().Be("ct:blacklist:ips");
    }

    [Fact]
    public void RateLimit_ShouldIncludeEndpointAndClient()
    {
        var key = CacheKeys.RateLimit("/api/tenants", "192.168.1.1");
        key.Should().Be("ct:ratelimit:/api/tenants:192.168.1.1");
    }
}

public class PermissionsTests
{
    [Fact]
    public void Tenants_Permissions_ShouldFollowConvention()
    {
        Permissions.Tenants.Create.Should().Be("tenants.create");
        Permissions.Tenants.Read.Should().Be("tenants.read");
        Permissions.Tenants.Update.Should().Be("tenants.update");
        Permissions.Tenants.Delete.Should().Be("tenants.delete");
        Permissions.Tenants.All.Should().Be("tenants.*");
    }

    [Fact]
    public void FullAccess_ShouldBeWildcard()
    {
        Permissions.FullAccess.Should().Be("*.*");
    }

    [Fact]
    public void SystemRoles_ShouldHaveCorrectNames()
    {
        SystemRoles.SuperAdmin.Should().Be("SuperAdmin");
        SystemRoles.SystemUser.Should().Be("SystemUser");
    }
}
