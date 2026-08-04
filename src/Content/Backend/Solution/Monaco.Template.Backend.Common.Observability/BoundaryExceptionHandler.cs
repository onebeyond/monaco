using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

public sealed class BoundaryExceptionHandler : IExceptionHandler
{
	private readonly ILoggerFactory _loggerFactory;

	public BoundaryExceptionHandler(ILoggerFactory loggerFactory)
	{
		_loggerFactory = loggerFactory;
	}

	public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken _)
	{
		BoundaryExceptionDiagnostics.Record(_loggerFactory, exception);
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return ValueTask.FromResult(true);
	}
}