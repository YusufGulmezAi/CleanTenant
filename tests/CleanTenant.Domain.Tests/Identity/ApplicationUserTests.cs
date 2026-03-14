using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Domain.Tests.Identity;

public class ApplicationUserTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldReturnUser()
    {
        var user = ApplicationUser.Create("test@test.com", "Test User");

        user.Should().NotBeNull();
        user.Email.Should().Be("test@test.com");
        user.FullName.Should().Be("Test User");
        user.IsActive.Should().BeTrue();
        user.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldLowercaseEmail()
    {
        var user = ApplicationUser.Create("  TEST@Test.COM  ", "Test User");
        user.Email.Should().Be("test@test.com");
    }

    [Fact]
    public void Create_ShouldRaiseUserCreatedEvent()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserCreatedEvent>();
    }

    [Fact]
    public void Create_WithEmptyEmail_ShouldThrow()
    {
        var act = () => ApplicationUser.Create("", "Test");
        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void EnableTwoFactor_WithEmailMethod_ShouldSucceed()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        user.ConfirmEmail();

        user.EnableTwoFactor(TwoFactorMethod.Email);

        user.TwoFactorEnabled.Should().BeTrue();
        user.PrimaryTwoFactorMethod.Should().Be(TwoFactorMethod.Email);
    }

    [Fact]
    public void EnableTwoFactor_WithoutConfirmedEmail_ShouldThrow()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        // E-posta doğrulanmamış

        var act = () => user.EnableTwoFactor(TwoFactorMethod.Email);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnableTwoFactor_Sms_WithoutConfirmedPhone_ShouldThrow()
    {
        var user = ApplicationUser.Create("test@test.com", "Test", "+905551234567");
        user.ConfirmEmail();
        // Telefon doğrulanmamış

        var act = () => user.EnableTwoFactor(TwoFactorMethod.Sms);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnableTwoFactor_Authenticator_WithoutKey_ShouldThrow()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        user.ConfirmEmail();

        var act = () => user.EnableTwoFactor(TwoFactorMethod.Authenticator);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisableTwoFactor_ShouldClearSettings()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        user.ConfirmEmail();
        user.EnableTwoFactor(TwoFactorMethod.Email);

        user.DisableTwoFactor();

        user.TwoFactorEnabled.Should().BeFalse();
        user.PrimaryTwoFactorMethod.Should().Be(TwoFactorMethod.None);
    }

    [Fact]
    public void RecordLogin_ShouldUpdateLastLoginAndResetFailCount()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        user.AccessFailedCount = 3;

        user.RecordLogin("192.168.1.1");

        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.LastLoginIp.Should().Be("192.168.1.1");
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public void RecordFailedLogin_ShouldIncrementAndLockout()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");

        // 5 başarısız deneme → kilit
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        user.AccessFailedCount.Should().Be(5);
        user.LockoutEnd.Should().NotBeNull();
        user.LockoutEnd.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void RecordFailedLogin_BelowThreshold_ShouldNotLockout()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");

        user.RecordFailedLogin(5, TimeSpan.FromMinutes(15));

        user.AccessFailedCount.Should().Be(1);
        user.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public void SetActiveStatus_ShouldRaiseEvent()
    {
        var user = ApplicationUser.Create("test@test.com", "Test");
        user.ClearDomainEvents();

        user.SetActiveStatus(false);

        user.IsActive.Should().BeFalse();
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserStatusChangedEvent>();
    }

    [Fact]
    public void UpdateProfile_ShouldChangeFields()
    {
        var user = ApplicationUser.Create("test@test.com", "Old Name");

        user.UpdateProfile("New Name", "+905559876543", "https://avatar.com/pic.jpg");

        user.FullName.Should().Be("New Name");
        user.PhoneNumber.Should().Be("+905559876543");
        user.AvatarUrl.Should().Be("https://avatar.com/pic.jpg");
    }
}
