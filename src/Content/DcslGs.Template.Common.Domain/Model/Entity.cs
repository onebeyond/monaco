using DcslGs.Template.Common.Domain.Model.Contracts;

namespace DcslGs.Template.Common.Domain.Model;

public abstract class Entity : IEntity
{
    protected Entity()
    {
    }

    protected Entity(Guid id) : this()
    {
        Id = id;
    }

    /// <summary>
    /// The identifier of the entity
    /// </summary>
    public virtual Guid Id { get; }


    private readonly List<DomainEvent> _domainEvents = new();
    /// <summary>
    /// List that holds Domain Events for this entity instance 
    /// </summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Adds a domain event to the DomainEvents collection
    /// </summary>
    /// <param name="eventItem">The event being added</param>
    /// <param name="unique">
    ///     Allows to indicate if this event is meant to exist only once in the DomainEvents collection.
    ///     If true, eventItem will not be added if an instance of it already exists in the collection.
    /// </param>
    public void AddDomainEvent(DomainEvent eventItem, bool unique = false)
    {
        if (unique && _domainEvents.Any(x => x.GetType() == eventItem.GetType()))
            return;

        _domainEvents.Add(eventItem);
    }

    /// <summary>
    /// Remove a domain event from the DomainEvents collection
    /// </summary>
    /// <param name="eventItem">The event being removed from the collection</param>
    public void RemoveDomainEvent(DomainEvent eventItem)
    {
        if (_domainEvents.Contains(eventItem))
            _domainEvents.Remove(eventItem);
    }

    /// <summary>
    /// Removes all the events from the DomainEvents collection
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetRealType() != other.GetRealType())
            return false;

        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    public static bool operator ==(Entity? a, Entity? b)
    {
        if (a is null && b is null)
            return true;

        if (a is null || b is null)
            return false;

        return a.Equals(b);
    }

    public static bool operator !=(Entity a, Entity b) => !(a == b);

    public override int GetHashCode() => $"{GetRealType()}{Id}".GetHashCode();

    private Type GetRealType()
    {
        var type = GetType();
        return (type.ToString().Contains("Castle.Proxies.")
                    ? type.BaseType
                    : type)!;
    }
}