using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class IdentityEnrichmentMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<IdentityEnrichmentMiddleware> _logger;

	public IdentityEnrichmentMiddleware(RequestDelegate next, ILogger<IdentityEnrichmentMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	private const string UserIdTag = "user.id";
	private const string UserClaimsScopeKey = "user.claims";
	private const string UserClaimEventName = "user.claim";
	private const string UserClaimTypeTag = "user.claim.type";
	private const string UserClaimValueTag = "user.claim.value";
	private const string UserClaimValueTypeTag = "user.claim.value_type";
	private const string UserClaimIssuerTag = "user.claim.issuer";
	private const string UserClaimOriginalIssuerTag = "user.claim.original_issuer";
	private const string SubClaimType = "sub";

	public async Task InvokeAsync(HttpContext context)
	{
		if (context.User.Identity?.IsAuthenticated != true)
		{
			await _next(context);
			return;
		}

		var claims = context.User
							.Claims
							.Select(claim => new ValidatedClaimContext(claim.Type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer))
							.ToImmutableArray();
		var activity = Activity.Current;
		var sub = claims.FirstOrDefault(claim => claim.Type == SubClaimType && !string.IsNullOrEmpty(claim.Value))?.Value;
		if (!string.IsNullOrEmpty(sub))
			activity?.SetTag(UserIdTag, sub);

		foreach (var claim in claims)
			activity?.AddEvent(CreateClaimEvent(claim));

		var scope = string.IsNullOrEmpty(sub)
						? new[] { new KeyValuePair<string, object?>(UserClaimsScopeKey, claims) }
						: new[] { new KeyValuePair<string, object?>(UserIdTag, sub), new KeyValuePair<string, object?>(UserClaimsScopeKey, claims) };

		using (_logger.BeginScope(scope))
			await _next(context);
	}

	private static ActivityEvent CreateClaimEvent(ValidatedClaimContext claim) =>
		new(UserClaimEventName,
			tags: new ActivityTagsCollection
				  {
					  { UserClaimTypeTag, claim.Type },
					  { UserClaimValueTag, claim.Value },
					  { UserClaimValueTypeTag, claim.ValueType },
					  { UserClaimIssuerTag, claim.Issuer },
					  { UserClaimOriginalIssuerTag, claim.OriginalIssuer }
				  });

	internal sealed record ValidatedClaimContext(string Type,
												 string Value,
												 string ValueType,
												 string Issuer,
												 string OriginalIssuer);
}