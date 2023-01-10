using Microsoft.AspNetCore.Builder;

namespace Monaco.Template.Common.Api.Middleware.Extensions;

public static class MiddlewareExtensions
{
	/// <summary>
	/// Uses the Serilog Context Enricher middleware to inject the current user into the Serilog Context.
	/// </summary>
	/// <param name="app"></param>
	/// <returns></returns>
	public static IApplicationBuilder UseSerilogContextEnricher(this IApplicationBuilder app) =>
		app.UseMiddleware<SerilogContextEnricherMiddleware>();

	/// <summary>
	/// Uses a middleware for mapping all claims from a JWT token to the Context.User but without running any kind of authentication/authorization middleware
	/// </summary>
	/// <param name="builder"></param>
	/// <returns></returns>
	public static IApplicationBuilder UseJwtClaimsMapper(this IApplicationBuilder builder) =>
		builder.UseMiddleware<JwtClaimsMapperMiddleware>();
}