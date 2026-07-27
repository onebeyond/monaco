using MediatR;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class LongRunningProcess
{
	public sealed record Command(Guid Id,
								 string Title,
								 string Description,
								 decimal Price,
								 Guid CompanyId) : IRequest;

	internal sealed class Handler(ILogger<Handler> logger) : IRequestHandler<Command>
	{
		public Task Handle(Command request, CancellationToken cancellationToken)
		{
			//Do some long-running process here
			logger.LogInformation(new EventId(2000, "LongRunningProcessCompleted"), "Long-running process command completed.");
			
			return Task.CompletedTask;
		}
	}
}
