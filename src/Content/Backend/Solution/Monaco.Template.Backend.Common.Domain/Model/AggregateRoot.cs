namespace Monaco.Template.Backend.Common.Domain.Model;

/// <summary>
/// Represents the base class for aggregate roots in a domain-driven design context.
/// </summary>
/// <remarks>An aggregate root is the entry point to an aggregate, which is a cluster of domain objects that can
/// be treated as a single unit.  This class provides functionality for managing domain events associated with the
/// aggregate root, including adding, removing, and clearing events. It ensures that domain events are encapsulated
/// within the aggregate and can be processed as needed.</remarks>
public abstract class AggregateRoot : Entity
{
	protected AggregateRoot()
	{
	}

	protected AggregateRoot(Guid id) : base(id)
	{
	}

	private readonly List<DomainEvent> _domainEvents = [];
	/// <summary>
	/// List that holds Domain Events for this entity instance 
	/// </summary>
	public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents;

	/// <summary>
	/// Adds a domain event to the DomainEvents collection
	/// </summary>
	/// <param name="eventItem">The event being added</param>
	/// <param name="unique">
	///     Allows to indicate if this event is meant to exist only once in the DomainEvents collection.
	///     If true, eventItem will not be added if an instance of it already exists in the collection.
	/// </param>
	protected void AddDomainEvent(DomainEvent eventItem, bool unique = false)
	{
		if (unique && _domainEvents.Any(x => x.GetType() == eventItem.GetType()))
			return;

		_domainEvents.Add(eventItem);
	}

	/// <summary>
	/// Remove a domain event from the DomainEvents collection
	/// </summary>
	/// <param name="eventItem">The event being removed from the collection</param>
	protected void RemoveDomainEvent(DomainEvent eventItem) =>
		_domainEvents.Remove(eventItem);

	/// <summary>
	/// Removes all the events from the DomainEvents collection
	/// </summary>
	public void ClearDomainEvents() =>
		_domainEvents.Clear();
}