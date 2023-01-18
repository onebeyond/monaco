using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Monaco.Template.Backend.Common.Api.Attributes;

namespace Monaco.Template.Backend.Common.Api.Middleware;

public class JwtClaimsMapperMiddleware
{
	private readonly RequestDelegate _next;
	private const string SchemeStr = $"{JwtBearerDefaults.AuthenticationScheme} ";
	private const string ScopeClaimType = "scope";
	private const string NameClaimType = "name";
	private const string RoleClaimType = "role";

	public JwtClaimsMapperMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public Task InvokeAsync(HttpContext context)
	{
		if (context.GetEndpoint()?.Metadata.Any(x => x is JwtMapClaimsAttribute) ?? false)
		{
			var authHeader = context.Request.Headers.Authorization.FirstOrDefault(x => x!.Contains(SchemeStr));
			if (!string.IsNullOrWhiteSpace(authHeader))
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

		return _next(context);
	}
}