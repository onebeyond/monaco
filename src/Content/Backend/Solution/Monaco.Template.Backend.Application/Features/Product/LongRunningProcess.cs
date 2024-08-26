using MediatR;
using Serilog;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class LongRunningProcess
{
	public sealed record Command(Guid Id,
								 string Title,
								 string Description,
								 decimal Price,
								 Guid CompanyId) : IRequest;

	internal sealed class Handler : IRequestHandler<Command>
	{
		public Task Handle(Command request, CancellationToken cancellationToken)
		{
			//Do some long-running process here
			Log.Information("Long running process for product created: {@Product}", request);
			
			return Task.CompletedTask;
		}
	}
}