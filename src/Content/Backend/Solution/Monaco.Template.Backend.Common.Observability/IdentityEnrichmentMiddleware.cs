using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class IdentityEnrichmentMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ObservabilityStartupOptions _options;
	private readonly ILogger<IdentityEnrichmentMiddleware> _logger;

	public IdentityEnrichmentMiddleware(RequestDelegate next,
										ObservabilityStartupOptions options,
										ILogger<IdentityEnrichmentMiddleware> logger)
	{
		_next = next;
		_options = options;
		_logger = logger;
	}

	private const string UserIdTag = "user.id";
	private const string SubClaimType = "sub";
	private const string IssClaimType = "iss";

	public async Task InvokeAsync(HttpContext context)
	{
		if (_options.IdentityMode == ObservabilityIdentityMode.Disabled ||
			context.User.Identity?.IsAuthenticated != true)
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

		var userId = ResolveUserId(sub, context);
		if (userId is null)
		{
			await _next(context);
			return;
		}

		Activity.Current?.SetTag(UserIdTag, userId);
		using (_logger.BeginScope(new[] { new KeyValuePair<string, object?>(UserIdTag, userId) }))
			await _next(context);
	}

	private string? ResolveUserId(string sub, HttpContext context) =>
		_options.IdentityMode switch
		{
			ObservabilityIdentityMode.Subject => sub,
			ObservabilityIdentityMode.HmacSha256 => ResolveHmacUserId(sub, context),
			_ => null
		};

	private string? ResolveHmacUserId(string sub, HttpContext context)
	{
		if (_options.HmacSha256KeyBytes is null)
			return null;

		var iss = context.User.FindFirst(IssClaimType)?.Value;
		return string.IsNullOrEmpty(iss)
				   ? null
				   : HmacSha256Identity.Compute(_options.HmacSha256KeyBytes, iss, sub);
	}
}