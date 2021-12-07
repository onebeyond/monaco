namespace DcslGs.Template.Common.Domain.Model.Contracts;

public interface IEntity
{
    /// <summary>
    /// The identifier of the entity
    /// </summary>
    Guid Id { get; }

    #region Domain events properties and methods

    /// <summary>
    /// List that holds Domain Events for this entity instance 
    /// </summary>
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    /// <summary>
    /// Adds a domain event to the DomainEvents collection
    /// </summary>
    /// <param name="eventItem">The event being added</param>
    /// <param name="unique">
    ///     Allows to indicate if this event is meant to exist only once in the DomainEvents collection.
    ///     If true, eventItem will not be added if an instance of it already exists in the collection.
    /// </param>
    void AddDomainEvent(DomainEvent eventItem, bool unique = false);
    /// <summary>
    /// Remove a domain event from the DomainEvents collection
    /// </summary>
    /// <param name="eventItem">The event being removed from the collection</param>
    void RemoveDomainEvent(DomainEvent eventItem);
    /// <summary>
    /// Removes all the events from the DomainEvents collection
    /// </summary>
    void ClearDomainEvents();

    #endregion
}