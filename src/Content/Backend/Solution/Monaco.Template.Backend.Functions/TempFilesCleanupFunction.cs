using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Application.Features.File;

namespace Monaco.Template.Backend.Functions
{
	public class TempFilesCleanupFunction
	{
		private readonly ILogger _logger;
		private readonly ISender _sender;

		public TempFilesCleanupFunction(ILoggerFactory loggerFactory, ISender sender)
		{
			_sender = sender;
			_logger = loggerFactory.CreateLogger<TempFilesCleanupFunction>();
		}

		[Function(nameof(TempFilesCleanupFunction))]
		public async Task Run([TimerTrigger("0 0 0 * * *", RunOnStartup = true)] TimerInfo timer)
		{
			_logger.LogInformation("{Function} executed at: {Now}", nameof(TempFilesCleanupFunction), DateTime.Now);

			await _sender.Send(new CleanTempFiles.Command());
		}
	}
}
