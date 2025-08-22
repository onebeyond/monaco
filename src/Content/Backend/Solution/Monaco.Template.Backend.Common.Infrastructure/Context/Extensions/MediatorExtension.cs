using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

public static class MediatorExtension
{
	public static async Task DispatchDomainEventsAsync(this IPublisher publisher, DbContext ctx)
	{
		while (true)
		{
			var aggregateRoots = ctx.ChangeTracker
									.Entries<AggregateRoot>()
									.Where(x => x.Entity
												 .DomainEvents
												 .Any())
									.ToList();

			var domainEvents = aggregateRoots.SelectMany(x => x.Entity.DomainEvents).ToList();

			aggregateRoots.ForEach(entity => entity.Entity.ClearDomainEvents());

			foreach (var domainEvent in domainEvents)
				await publisher.Publish(domainEvent);

			//If event handlers produced more domain events, keep processing them until there's no more
			if (ctx.ChangeTracker
				   .Entries<AggregateRoot>()
				   .Any(x => x.Entity
							  .DomainEvents
							  .Any()))
				continue;

			break;
		}
	}
}