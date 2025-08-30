using AwesomeAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests.Factories;

[ExcludeFromCodeCoverage]
[Trait("Common Domain Entities", "Aggregate Root")]
public class AggregateRootTests
{
	[Theory(DisplayName = "New aggregate root succeeds")]
	[AutoDomainData]
	public void NewAggregateRootSucceeds(AggregateWrapper sut)
	{
		sut.DomainEvents
		   .Should()
		   .BeEmpty();
	}

	[Theory(DisplayName = "Add Domain Event succeeds")]
	[AutoDomainData]
	public void AddDomainEventSucceeds(AggregateWrapper sut, DomainEvent domainEvent)
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
	public void AddDuplicatedDomainEventAllowingDuplicatesSucceeds(AggregateWrapper sut, DomainEvent domainEvent)
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
	public void AddDuplicatedDomainEventNotAllowingDuplicatesAddsOnlyOne(AggregateWrapper sut, DomainEvent domainEvent)
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
	public void AddDuplicatedTypeOfDomainEventNotAllowingDuplicatesAddsOnlyOne(AggregateWrapper sut, DomainEvent domainEvent1, DomainEvent domainEvent2)
	{
		sut.AddDomainEvent(domainEvent1, true);
		sut.AddDomainEvent(domainEvent2, true);

		sut.DomainEvents
		   .Should()
		   .HaveCount(1)
		   .And
		   .Contain(domainEvent1);
	}

	[Theory(DisplayName = "Remove Domain Event succeeds")]
	[AutoDomainData]
	public void RemoveDomainEventSucceeds(AggregateWrapper sut, List<DomainEvent> domainEvents)
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
	public void RemoveNonExistingDomainEventSucceeds(AggregateWrapper sut, List<DomainEvent> domainEvents)
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
	public void ClearDomainEventsSucceeds(AggregateWrapper sut, List<DomainEvent> domainEvents)
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
}

public class AggregateWrapper : AggregateRoot
{
	public new void AddDomainEvent(DomainEvent eventItem, bool unique = false) =>
		base.AddDomainEvent(eventItem, unique);

	public new void RemoveDomainEvent(DomainEvent domainEvent) =>
		base.RemoveDomainEvent(domainEvent);

	public new void ClearDomainEvents() =>
		base.ClearDomainEvents();
}