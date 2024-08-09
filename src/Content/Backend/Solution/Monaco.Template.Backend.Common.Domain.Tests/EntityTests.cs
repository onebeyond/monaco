using AutoFixture;
using FluentAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Common.Domain.Tests.Factories.Entities;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Domain Entities", "Base Entity")]
public class EntityTests
{
	[Theory(DisplayName = "New entity without parameters succeeds")]
	[AutoDomainData]
	public void NewEntityWithoutParametersSucceeds(Entity sut)
	{
		sut.Id
		   .Should()
		   .BeEmpty();
		sut.DomainEvents
		   .Should()
		   .BeEmpty();
	}

	[Theory(DisplayName = "New entity with parameters succeeds")]
	[AutoDomainData]
	public void NewEntityWithParametersSucceeds(Guid id)
	{
		var sut = EntityFactory.CreateMock(id);

		sut.Id
		   .Should()
		   .Be(id);
		sut.DomainEvents
		   .Should()
		   .BeEmpty();
	}

	[Theory(DisplayName = "Add Domain Event succeeds")]
	[AutoDomainData]
	public void AddDomainEventSucceeds(Entity sut, DomainEvent domainEvent)
	{
		sut.AddDomainEvent(domainEvent);

		sut.DomainEvents
		   .Should()
		   .HaveCount(1)
		   .And
		   .Contain(domainEvent);
	}

	[Theory(DisplayName = "Add duplicated domain event allowing duplicates succeeds")]
	[AutoDomainData]
	public void AddDuplicatedDomainEventAllowingDuplicatesSucceeds(Entity sut, DomainEvent domainEvent)
	{
		sut.AddDomainEvent(domainEvent);
		sut.AddDomainEvent(domainEvent);

		sut.DomainEvents
		   .Should()
		   .HaveCount(2)
		   .And
		   .Contain([domainEvent, domainEvent]);
	}

	[Theory(DisplayName = "Add duplicated domain event not allowing duplicates adds only one")]
	[AutoDomainData]
	public void AddDuplicatedDomainEventNotAllowingDuplicatesAddsOnlyOne(Entity sut, DomainEvent domainEvent)
	{
		sut.AddDomainEvent(domainEvent, true);
		sut.AddDomainEvent(domainEvent, true);

		sut.DomainEvents
		   .Should()
		   .HaveCount(1)
		   .And
		   .Contain(domainEvent);
	}

	[Theory(DisplayName = "Add duplicated type of domain event not allowing duplicates adds only one")]
	[AutoDomainData]
	public void AddDuplicatedTypeOfDomainEventNotAllowingDuplicatesAddsOnlyOne(Entity sut, DomainEvent domainEvent1, DomainEvent domainEvent2)
	{
		sut.AddDomainEvent(domainEvent1, true);
		sut.AddDomainEvent(domainEvent2, true);

		sut.DomainEvents.Should().HaveCount(1).And.Contain(domainEvent1);
	}

	[Theory(DisplayName = "Remove Domain Event succeeds")]
	[AutoDomainData]
	public void RemoveDomainEventSucceeds(Entity sut, List<DomainEvent> domainEvents)
	{
		domainEvents.ForEach(x => sut.AddDomainEvent(x));

		sut.RemoveDomainEvent(domainEvents.First());

		sut.DomainEvents
		   .Should()
		   .HaveCount(2)
		   .And
		   .Contain([domainEvents[1], domainEvents[2]]);
	}

	[Theory(DisplayName = "Remove Domain Event succeeds")]
	[AutoDomainData]
	public void RemoveNonExistingDomainEventSucceeds(Entity sut, List<DomainEvent> domainEvents)
	{
		sut.AddDomainEvent(domainEvents[0]);
		sut.AddDomainEvent(domainEvents[1]);

		sut.DomainEvents
		   .Should()
		   .HaveCount(2);

		sut.RemoveDomainEvent(domainEvents[2]);

		sut.DomainEvents
		   .Should()
		   .HaveCount(2)
		   .And
		   .Contain([domainEvents[0], domainEvents[1]]);
	}

	[Theory(DisplayName = "Clear Domain Events succeeds")]
	[AutoDomainData]
	public void ClearDomainEventsSucceeds(Entity sut, List<DomainEvent> domainEvents)
	{
		domainEvents.ForEach(x => sut.AddDomainEvent(x));

		sut.DomainEvents
		   .Should()
		   .HaveCount(3);

		sut.ClearDomainEvents();

		sut.DomainEvents
		   .Should()
		   .BeEmpty();
	}

	[Theory(DisplayName = "Entity instance equals (method) itself succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodItselfSucceeds(Entity sut) =>
		sut.Equals(sut)
		   .Should()
		   .BeTrue();

	[Theory(DisplayName = "Entity instance equals (method) another instance same Id as same succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherInstanceSameIdAsSameSucceeds(Guid id)
	{
		var sut = EntityFactory.CreateMock(id);
		var instance = EntityFactory.CreateMock(id);

		sut.Equals(instance)
		   .Should()
		   .BeTrue();
	}

	[Theory(DisplayName = "Entity instance equals (method) another instance as different succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherInstanceAsDifferentSucceeds(Entity sut, Entity instance) =>
		sut.Equals(instance)
		   .Should()
		   .BeFalse();

	[Theory(DisplayName = "Entity instance equals (method) another different object as different succeeds ")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherDifferentObjectAsDifferentSucceeds(Entity sut, object instance) =>
		sut.Equals(instance)
		   .Should()
		   .BeFalse();

	[Theory(DisplayName = "Entity instance equals (method) another different not proxy object as different succeeds ")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherDifferentNotProxyObjectAsDifferentSucceeds(Entity sut) =>
		sut.Equals(new object())
		   .Should()
		   .BeFalse();

	[Theory(DisplayName = "Entity instance equals (operator) itself succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsOperatorItselfSucceeds(Entity sut) =>
#pragma warning disable CS1718 // Comparison made to same variable
		(sut == sut).Should()
					.BeTrue();
#pragma warning restore CS1718 // Comparison made to same variable


	[Theory(DisplayName = "Entity instance equals (operator) as different succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsOperatorNullAsDifferentSucceeds(Entity sut) =>
		(sut == null).Should()
					 .BeFalse();

	[Fact(DisplayName = "Entity instance null equals (operator) null as same succeeds")]
	public void EntityInstanceNullEqualsOperatorNullAsSameSucceeds() =>
		(null as Entity == null).Should()
								.BeTrue();

	[Theory(DisplayName = "Entity instance equals (operator) null as different succeeds")]
	[AutoDomainData]
	public void EntityInstanceNotEqualsOperatorNullAsDifferentSucceeds(Entity? sut) =>
		(sut != null).Should()
					 .BeTrue();

	[Theory(DisplayName = "Get Entity hash code succeeds")]
	[AutoDomainData]
	public void EntityGetHashCodeSucceeds(Entity sut)
	{
		var hashCode = $"{typeof(Entity).FullName}{sut.Id}".GetHashCode();

		sut.GetHashCode()
		   .Should()
		   .Be(hashCode);
	}
}