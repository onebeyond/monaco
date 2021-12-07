using AutoFixture;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DcslGs.Template.Common.Domain.Model;
using DcslGs.Template.Common.Domain.Tests.AutoFixture;
using Xunit;

namespace DcslGs.Template.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
public class EntityTests
{
    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "New entity without parameters succeeds")]
    [AnonymousData]
    public void NewEntityWithoutParametersSucceeds(Entity sut)
    {
        sut.Id.Should().BeEmpty();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "New entity with parameters succeeds")]
    [AnonymousData]
    public void NewEntityWithParametersSucceeds(Guid id)
    {
        var fixture = new Fixture();
        fixture.Customize<Entity>(c => c.FromFactory(() => new Mock<Entity>(id) { CallBase = true }.Object));

        var sut = fixture.Create<Entity>();

        sut.Id.Should().Be(id);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Add Domain Event succeeds")]
    [AnonymousData]
    public void AddDomainEventSucceeds(Entity sut, DomainEvent domainEvent)
    {
        sut.AddDomainEvent(domainEvent);

        sut.DomainEvents.Should().HaveCount(1).And.Contain(domainEvent);
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Add duplicated domain event allowing duplicates succeeds")]
    [AnonymousData]
    public void AddDuplicatedDomainEventAllowingDuplicatesSucceeds(Entity sut, DomainEvent domainEvent)
    {
        sut.AddDomainEvent(domainEvent);
        sut.AddDomainEvent(domainEvent);

        sut.DomainEvents.Should().HaveCount(2).And.Contain(new[] { domainEvent, domainEvent });
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Add duplicated domain event not allowing duplicates adds only one")]
    [AnonymousData]
    public void AddDuplicatedDomainEventNotAllowingDuplicatesAddsOnlyOne(Entity sut, DomainEvent domainEvent)
    {
        sut.AddDomainEvent(domainEvent, true);
        sut.AddDomainEvent(domainEvent, true);

        sut.DomainEvents.Should().HaveCount(1).And.Contain(domainEvent);
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Add duplicated type of domain event not allowing duplicates adds only one")]
    [AnonymousData]
    public void AddDuplicatedTypeOfDomainEventNotAllowingDuplicatesAddsOnlyOne(Entity sut, DomainEvent domainEvent1, DomainEvent domainEvent2)
    {
        sut.AddDomainEvent(domainEvent1, true);
        sut.AddDomainEvent(domainEvent2, true);

        sut.DomainEvents.Should().HaveCount(1).And.Contain(domainEvent1);
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Remove Domain Event succeeds")]
    [AnonymousData]
    public void RemoveDomainEventSucceeds(Entity sut, List<DomainEvent> domainEvents)
    {
        domainEvents.ForEach(x => sut.AddDomainEvent(x));

        sut.RemoveDomainEvent(domainEvents.First());

        sut.DomainEvents.Should().HaveCount(2).And.Contain(new[] { domainEvents[1], domainEvents[2] });
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Remove Domain Event succeeds")]
    [AnonymousData]
    public void RemoveNonExistingDomainEventSucceeds(Entity sut, List<DomainEvent> domainEvents)
    {
        sut.AddDomainEvent(domainEvents[0]);
        sut.AddDomainEvent(domainEvents[1]);

        sut.DomainEvents.Should().HaveCount(2);

        sut.RemoveDomainEvent(domainEvents[2]);

        sut.DomainEvents.Should().HaveCount(2).And.Contain(new[] { domainEvents[0], domainEvents[1] });
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Clear Domain Events succeeds")]
    [AnonymousData]
    public void ClearDomainEventsSucceeds(Entity sut, List<DomainEvent> domainEvents)
    {
        domainEvents.ForEach(x => sut.AddDomainEvent(x));

        sut.DomainEvents.Should().HaveCount(3);

        sut.ClearDomainEvents();

        sut.DomainEvents.Should().BeEmpty();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (method) itself succeeds")]
    [AnonymousData]
    public void EntityInstanceEqualsMethodItselfSucceeds(Entity sut)
    {
        sut.Equals(sut).Should().BeTrue();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (method) another instance same Id as same succeeds")]
    [AnonymousData]
    public void EntityInstanceEqualsMethodAnotherInstanceSameIdAsSameSucceeds(Guid id)
    {
        var fixture = new Fixture();
        fixture.Customize<Entity>(c => c.FromFactory(() => new Mock<Entity>(id) { CallBase = true }.Object));

        var sut = fixture.Create<Entity>();
        var instance = fixture.Create<Entity>();

        sut.Equals(instance).Should().BeTrue();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (method) another instance as different succeeds")]
    [AnonymousData]
    public void EntityInstanceEqualsMethodAnotherInstanceAsDifferentSucceeds(Entity sut, Entity instance)
    {
        sut.Equals(instance).Should().BeFalse();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (method) another different object as different succeeds ")]
    [AnonymousData]
    public void EntityInstanceEqualsMethodAnotherDifferentObjectAsDifferentSucceeds(Entity sut, object instance)
    {
        sut.Equals(instance).Should().BeFalse();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (method) another different not proxy object as different succeeds ")]
    [AnonymousData]
    public void EntityInstanceEqualsMethodAnotherDifferentNotProxyObjectAsDifferentSucceeds(Entity sut)
    {
        sut.Equals(new object()).Should().BeFalse();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (operator) itself succeeds")]
    [AnonymousData]
    public void EntityInstanceEqualsOperatorItselfSucceeds(Entity sut)
    {
        // ReSharper disable once EqualExpressionComparison
#pragma warning disable CS1718 // Comparison made to same variable
        (sut == sut).Should().BeTrue();
#pragma warning restore CS1718 // Comparison made to same variable
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (operator) as different succeeds")]
    [AnonymousData]
    public void EntityInstanceEqualsOperatorNullAsDifferentSucceeds(Entity sut)
    {
        (sut == null).Should().BeFalse();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Fact(DisplayName = "Entity instance null equals (operator) null as same succeeds")]
    public void EntityInstanceNullEqualsOperatorNullAsSameSucceeds()
    {
        ((Entity)null == null).Should().BeTrue();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Entity instance equals (operator) null as different succeeds")]
    [AnonymousData]
    public void EntityInstanceNotEqualsOperatorNullAsDifferentSucceeds(Entity sut)
    {
        (sut != null).Should().BeTrue();
    }

    [Trait("Common domain Entities", "Base Entity")]
    [Theory(DisplayName = "Get Entity hash code succeeds")]
    [AnonymousData]
    public void EntityGetHashCodeSucceeds(Entity sut)
    {
        var hashCode = "DcslGs.Template.Common.Domain.Model.Entity00000000-0000-0000-0000-000000000000".GetHashCode();

        sut.GetHashCode().Should().Be(hashCode);
    }
}