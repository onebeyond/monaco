using MediatR;

namespace Monaco.Template.Backend.Common.Domain.Model;

/// <summary>
/// Represents a domain event that signifies an occurrence within the domain.
/// </summary>
/// <remarks>Domain events are used to capture and communicate significant changes or actions  within the domain
/// model. They are typically handled by event handlers to trigger  side effects or propagate changes to other parts of
/// the system.</remarks>
public class DomainEvent : INotification
{
	/// <summary>
	/// Gets the date and time when the event occurred, in Coordinated Universal Time (UTC).
	/// </summary>
	public DateTime DateOccurred { get; protected set; } = DateTime.UtcNow;
}