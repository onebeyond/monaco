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

	internal sealed class Handler : IRequestHandler<Command>
	{
		private readonly ILogger<LongRunningProcess> _logger;

		public Handler(ILogger<LongRunningProcess> logger)
		{
			_logger = logger;
		}

		public Task Handle(Command request, CancellationToken cancellationToken)
		{
			//Do some long-running process here
			_logger.LogInformation("Long running process for product created: {@Product}", request);
			
			return Task.CompletedTask;
		}
	}
}