using MediatR;

namespace Monaco.Template.Backend.Common.Domain.Model;

public class DomainEvent : INotification
{
	public DateTime DateOccurred { get; protected set; } = DateTime.UtcNow;
}