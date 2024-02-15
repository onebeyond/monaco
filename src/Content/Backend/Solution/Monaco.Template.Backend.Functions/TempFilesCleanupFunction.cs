using MassTransit.Mediator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Application.Features.File;

namespace Monaco.Template.Backend.Functions
{
	public class TempFilesCleanupFunction
	{
		private readonly ILogger _logger;
		private readonly IMediator _mediator;

		public TempFilesCleanupFunction(ILoggerFactory loggerFactory, IMediator mediator)
		{
			_mediator = mediator;
			_logger = loggerFactory.CreateLogger<TempFilesCleanupFunction>();
		}

		[Function("TempFilesCleanup")]
		public void Run([TimerTrigger("0 0 0 * * *", RunOnStartup = true)] TimerInfo timer)
		{
			_logger.LogInformation("{Function} executed at: {Now}", nameof(TempFilesCleanupFunction), DateTime.Now);

			_mediator.Send(new CleanTempFile.CleanupCommand());
		}
	}
}
