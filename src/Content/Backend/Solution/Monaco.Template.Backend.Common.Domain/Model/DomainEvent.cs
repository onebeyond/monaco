using MediatR;

namespace Monaco.Template.Backend.Common.Domain.Model;

public class DomainEvent : INotification
{
	public DomainEvent()
	{
		DateOccurred = DateTime.UtcNow;
	}

	public DateTime DateOccurred { get; protected set; }
}