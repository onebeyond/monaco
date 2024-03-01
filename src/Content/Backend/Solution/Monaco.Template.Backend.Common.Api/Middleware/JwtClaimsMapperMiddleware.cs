using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Monaco.Template.Backend.Common.Api.Attributes;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Monaco.Template.Backend.Common.Api.Middleware;

/// <summary>
/// Middleware to read and parse a JWT coming in the Authorization header and later populate the request's Principal in order to have this data available for the remaining of the request execution.
/// This middleware is useful for cases where there's an API Gateway in front of the API but some endpoints still need to validate some user's claims as part of the business rules, so this makes that available.
/// It's assumed that the JWT token passed has already been validated by the eventual API Gateway on top of the API.
/// </summary>
public class JwtClaimsMapperMiddleware : IMiddleware
{
	private const string SchemeStr = $"{JwtBearerDefaults.AuthenticationScheme} ";
	private const string ScopeClaimType = "scope";
	private const string NameClaimType = "name";
	private const string RoleClaimType = "role";
	
	public Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		if (context.GetEndpoint()?.Metadata.Any(x => x is JwtMapClaimsAttribute) ?? false)
		{
			var authHeader = context.Request.Headers.Authorization.FirstOrDefault(x => x!.Contains(SchemeStr));
			if (authHeader is { Length: > 0 })
			{
				var jwtString = authHeader.Replace(SchemeStr, string.Empty);
				var handler = new JwtSecurityTokenHandler();
				var jwt = handler.ReadJwtToken(jwtString);
				var identity = new ClaimsIdentity(jwt.Claims, string.Empty, NameClaimType, RoleClaimType);
				var claim = identity.FindFirst(ScopeClaimType);
				if (claim?.Value.Contains(' ') ?? false)
				{
					var scopes = claim.Value.Split(' ');
					identity.AddClaims(scopes.Select(s => new Claim(ScopeClaimType, s, claim.ValueType, claim.Issuer)));
					identity.RemoveClaim(claim);
				}
				context.User = new ClaimsPrincipal(identity);
			}
		}

		return next(context);
	}
}