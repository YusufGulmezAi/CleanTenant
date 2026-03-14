using CleanTenant.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Domain.Tests.Common;

/// <summary>BaseEntity domain event yönetimi testleri.</summary>
public class BaseEntityTests
{
    // Test için concrete entity
    private class TestEntity : BaseEntity { }
    private record TestEvent(string Message) : IDomainEvent;

    [Fact]
    public void AddDomainEvent_ShouldAddToCollection()
    {
        var entity = new TestEntity();
        var ev = new TestEvent("test");

        entity.AddDomainEvent(ev);

        entity.DomainEvents.Should().ContainSingle().Which.Should().Be(ev);
    }

    [Fact]
    public void RemoveDomainEvent_ShouldRemoveFromCollection()
    {
        var entity = new TestEntity();
        var ev = new TestEvent("test");
        entity.AddDomainEvent(ev);

        entity.RemoveDomainEvent(ev);

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAll()
    {
        var entity = new TestEntity();
        entity.AddDomainEvent(new TestEvent("1"));
        entity.AddDomainEvent(new TestEvent("2"));
        entity.AddDomainEvent(new TestEvent("3"));

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_ShouldBeReadOnly()
    {
        var entity = new TestEntity();
        entity.DomainEvents.Should().BeAssignableTo<IReadOnlyCollection<IDomainEvent>>();
    }

    [Fact]
    public void MultipleDomainEvents_ShouldPreserveOrder()
    {
        var entity = new TestEntity();
        var ev1 = new TestEvent("first");
        var ev2 = new TestEvent("second");

        entity.AddDomainEvent(ev1);
        entity.AddDomainEvent(ev2);

        entity.DomainEvents.Should().HaveCount(2);
        entity.DomainEvents.First().Should().Be(ev1);
        entity.DomainEvents.Last().Should().Be(ev2);
    }
}

/// <summary>UserLevel enum hiyerarşi testleri.</summary>
public class UserLevelTests
{
    [Theory]
    [InlineData(Domain.Enums.UserLevel.SuperAdmin, Domain.Enums.UserLevel.SystemUser, true)]
    [InlineData(Domain.Enums.UserLevel.SuperAdmin, Domain.Enums.UserLevel.CompanyMember, true)]
    [InlineData(Domain.Enums.UserLevel.TenantAdmin, Domain.Enums.UserLevel.TenantUser, true)]
    [InlineData(Domain.Enums.UserLevel.CompanyUser, Domain.Enums.UserLevel.CompanyAdmin, false)]
    [InlineData(Domain.Enums.UserLevel.TenantUser, Domain.Enums.UserLevel.TenantAdmin, false)]
    [InlineData(Domain.Enums.UserLevel.CompanyMember, Domain.Enums.UserLevel.SuperAdmin, false)]
    public void UserLevel_HierarchyComparison_ShouldBeCorrect(
        Domain.Enums.UserLevel currentLevel,
        Domain.Enums.UserLevel targetLevel,
        bool shouldBeHigher)
    {
        // Sayısal karşılaştırma ile hiyerarşi kontrolü
        var isHigher = (int)currentLevel > (int)targetLevel;
        isHigher.Should().Be(shouldBeHigher);
    }
}
