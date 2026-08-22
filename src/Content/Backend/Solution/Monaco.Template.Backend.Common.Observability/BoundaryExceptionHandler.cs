using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

public sealed class BoundaryExceptionHandler : IExceptionHandler
{
	private readonly ILogger<BoundaryExceptionHandler> _logger;

	public BoundaryExceptionHandler(ILogger<BoundaryExceptionHandler> logger)
	{
		_logger = logger;
	}

	public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken _)
	{
		_logger.LogError(exception, "Unhandled HTTP request failure.");
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return ValueTask.FromResult(true);
	}
}