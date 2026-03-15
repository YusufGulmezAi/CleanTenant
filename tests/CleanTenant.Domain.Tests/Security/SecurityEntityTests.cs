using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Security;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Domain.Tests.Security;

public class UserSessionTests
{
    [Fact]
    public void Create_ShouldSetAllFields()
    {
        var userId = Guid.CreateVersion7();
        var session = UserSession.Create(
            userId, "tokenHash", "refreshHash", "192.168.1.1",
            "Mozilla/5.0", "deviceHash",
            DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(7));

        session.UserId.Should().Be(userId);
        session.IpAddress.Should().Be("192.168.1.1");
        session.IsRevoked.Should().BeFalse();
        session.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Revoke_ShouldInvalidateSession()
    {
        var session = UserSession.Create(
            Guid.CreateVersion7(), "t", "r", "ip", "ua", "dh",
            DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(7));

        session.Revoke("admin@test.com");

        session.IsRevoked.Should().BeTrue();
        session.RevokedBy.Should().Be("admin@test.com");
        session.RevokedAt.Should().NotBeNull();
        session.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ShouldReturnFalse()
    {
        var session = UserSession.Create(
            Guid.CreateVersion7(), "t", "r", "ip", "ua", "dh",
            DateTime.UtcNow.AddMinutes(-1), // Süresi dolmuş
            DateTime.UtcNow.AddDays(7));

        session.IsValid().Should().BeFalse();
    }
}

public class UserBlockTests
{
    [Fact]
    public void Create_Permanent_ShouldHaveNoExpiry()
    {
        var block = UserBlock.Create(
            Guid.CreateVersion7(), BlockType.Permanent, "admin", "Kötüye kullanım");

        block.BlockType.Should().Be(BlockType.Permanent);
        block.ExpiresAt.Should().BeNull();
        block.IsActive().Should().BeTrue();
        block.IsLifted.Should().BeFalse();
    }

    [Fact]
    public void Create_Temporary_ShouldHaveExpiry()
    {
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var block = UserBlock.Create(
            Guid.CreateVersion7(), BlockType.Temporary, "admin", "Şüpheli aktivite", expiresAt);

        block.ExpiresAt.Should().Be(expiresAt);
        block.IsActive().Should().BeTrue();
    }

    [Fact]
    public void Lift_ShouldDeactivateBlock()
    {
        var block = UserBlock.Create(
            Guid.CreateVersion7(), BlockType.Permanent, "admin", "Test");

        block.Lift("superadmin");

        block.IsLifted.Should().BeTrue();
        block.LiftedBy.Should().Be("superadmin");
        block.LiftedAt.Should().NotBeNull();
        block.IsActive().Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {
        var block = UserBlock.Create(
            Guid.CreateVersion7(), BlockType.Temporary, "admin", "Test",
            DateTime.UtcNow.AddMinutes(-10)); // Süresi dolmuş

        block.IsActive().Should().BeFalse();
    }
}
