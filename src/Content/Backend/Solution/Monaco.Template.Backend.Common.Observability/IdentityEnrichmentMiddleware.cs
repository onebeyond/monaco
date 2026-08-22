using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class IdentityEnrichmentMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<IdentityEnrichmentMiddleware> _logger;

	public IdentityEnrichmentMiddleware(RequestDelegate next,
										ILogger<IdentityEnrichmentMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	private const string UserIdTag = "user.id";
	private const string SubClaimType = "sub";

	public async Task InvokeAsync(HttpContext context)
	{
		if (context.User.Identity?.IsAuthenticated != true)
		{
			await _next(context);
			return;
		}

		var sub = context.User.FindFirst(SubClaimType)?.Value;
		if (string.IsNullOrEmpty(sub))
		{
			await _next(context);
			return;
		}

		Activity.Current?.SetTag(UserIdTag, sub);
		using (_logger.BeginScope(new[] { new KeyValuePair<string, object?>(UserIdTag, sub) }))
			await _next(context);
	}
}