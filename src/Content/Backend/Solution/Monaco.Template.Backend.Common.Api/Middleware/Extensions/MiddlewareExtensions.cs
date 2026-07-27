using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Monaco.Template.Backend.Common.Api.Middleware.Extensions;

public static class MiddlewareExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Adds the JwtClaimsMapperMiddleware to the service collection for dependency injection.
		/// </summary>
		/// <returns>The current IServiceCollection instance with the JwtClaimsMapperMiddleware registered.</returns>
		public IServiceCollection AddJwtClaimsMapper() =>
			services.AddScoped<JwtClaimsMapperMiddleware>();
	}
	
	extension(IApplicationBuilder app)
	{
		/// <summary>
		/// Uses a middleware for mapping all claims from a JWT token to the Context.User but without running any kind of authentication/authorization middleware
		/// </summary>
		/// <returns></returns>
		public IApplicationBuilder UseJwtClaimsMapper() =>
			app.UseMiddleware<JwtClaimsMapperMiddleware>();
	}
}