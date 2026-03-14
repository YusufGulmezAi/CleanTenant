using CleanTenant.Domain.Tenancy;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Domain.Tests.Tenancy;

/// <summary>
/// Tenant entity factory method ve iş kurallarının birim testleri.
/// 
/// <para><b>TEST İSİMLENDİRME KURALI:</b></para>
/// MethodAdı_Senaryo_BeklenenSonuç formatını kullanıyoruz.
/// Örnek: Create_WithValidParameters_ShouldReturnTenant
/// Bu format, test başarısız olduğunda hatanın ne olduğunu
/// test adından anlamamızı sağlar.
/// </summary>
public class TenantTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldReturnTenant()
    {
        // Arrange & Act
        var tenant = Tenant.Create("ABC Mali Müşavirlik", "abc-mali");

        // Assert
        tenant.Should().NotBeNull();
        tenant.Name.Should().Be("ABC Mali Müşavirlik");
        tenant.Identifier.Should().Be("abc-mali");
        tenant.IsActive.Should().BeTrue();
        tenant.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var act = () => Tenant.Create("", "abc-mali");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithEmptyIdentifier_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var act = () => Tenant.Create("ABC Mali Müşavirlik", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("identifier");
    }

    [Fact]
    public void Create_ShouldRaiseTenantCreatedEvent()
    {
        // Arrange & Act
        var tenant = Tenant.Create("Test Tenant", "test-tenant");

        // Assert
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantCreatedEvent>();
    }

    [Fact]
    public void SetActiveStatus_WhenChanged_ShouldRaiseStatusChangedEvent()
    {
        // Arrange
        var tenant = Tenant.Create("Test", "test");
        tenant.ClearDomainEvents(); // Create event'ini temizle

        // Act
        tenant.SetActiveStatus(false);

        // Assert
        tenant.IsActive.Should().BeFalse();
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantStatusChangedEvent>();
    }

    [Fact]
    public void SetActiveStatus_WhenSameValue_ShouldNotRaiseEvent()
    {
        // Arrange
        var tenant = Tenant.Create("Test", "test");
        tenant.ClearDomainEvents();

        // Act — zaten aktif, tekrar aktif yapıyoruz
        tenant.SetActiveStatus(true);

        // Assert — gereksiz event tetiklenmemeli
        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldTrimAndLowercaseIdentifier()
    {
        // Arrange & Act
        var tenant = Tenant.Create("Test", "  ABC-MALI  ");

        // Assert
        tenant.Identifier.Should().Be("abc-mali");
    }
}
