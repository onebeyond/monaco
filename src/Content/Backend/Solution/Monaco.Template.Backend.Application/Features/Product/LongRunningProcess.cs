using MediatR;
using Serilog;

namespace Monaco.Template.Backend.Application.Features.Product;

public class LongRunningProcess
{
	public record Command(Guid Id,
						  string Title,
						  string Description,
						  string Price,
						  Guid CompanyId) : IRequest;

	public sealed class Handler : IRequestHandler<Command>
	{
		public Task Handle(Command request, CancellationToken cancellationToken)
		{
			//Do some long-running process here
			Log.Information("Long running process for product created: {@Product}", request);
			
			return Task.CompletedTask;
		}
	}
}