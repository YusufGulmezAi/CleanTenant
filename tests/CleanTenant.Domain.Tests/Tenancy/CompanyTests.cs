using CleanTenant.Domain.Tenancy;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Domain.Tests.Tenancy;

public class CompanyTests
{
    private readonly Guid _tenantId = Guid.CreateVersion7();

    [Fact]
    public void Create_WithValidParameters_ShouldReturnCompany()
    {
        var company = Company.Create(_tenantId, "ABC Gıda A.Ş.", "ABC-GIDA");

        company.Should().NotBeNull();
        company.Name.Should().Be("ABC Gıda A.Ş.");
        company.Code.Should().Be("ABC-GIDA");
        company.TenantId.Should().Be(_tenantId);
        company.IsActive.Should().BeTrue();
        company.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        var act = () => Company.Create(_tenantId, "", "CODE");
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => Company.Create(Guid.Empty, "Test", "CODE");
        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void Create_ShouldUppercaseCode()
    {
        var company = Company.Create(_tenantId, "Test", "  abc-gida  ");
        company.Code.Should().Be("ABC-GIDA");
    }

    [Fact]
    public void Create_ShouldRaiseCompanyCreatedEvent()
    {
        var company = Company.Create(_tenantId, "Test", "CODE");
        company.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CompanyCreatedEvent>();
    }

    [Fact]
    public void SetActiveStatus_WhenChanged_ShouldRaiseEvent()
    {
        var company = Company.Create(_tenantId, "Test", "CODE");
        company.ClearDomainEvents();

        company.SetActiveStatus(false);

        company.IsActive.Should().BeFalse();
        company.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CompanyStatusChangedEvent>();
    }

    [Fact]
    public void SetActiveStatus_WhenSame_ShouldNotRaiseEvent()
    {
        var company = Company.Create(_tenantId, "Test", "CODE");
        company.ClearDomainEvents();

        company.SetActiveStatus(true);

        company.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldChangeFields()
    {
        var company = Company.Create(_tenantId, "Old Name", "CODE");

        company.Update("New Name", "1234567890", "Kadıköy VD", "test@test.com", "555-1234", "İstanbul");

        company.Name.Should().Be("New Name");
        company.TaxNumber.Should().Be("1234567890");
        company.TaxOffice.Should().Be("Kadıköy VD");
    }
}
