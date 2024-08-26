using Asp.Versioning;

namespace Monaco.Template.Backend.Api.Endpoints.Extensions;

internal static class EndpointsExtensions
{
	/// <summary>
	/// Registers all Minimal API endpoints 
	/// </summary>
	/// <param name="builder"></param>
	/// <returns></returns>
	public static IEndpointRouteBuilder RegisterEndpoints(this IEndpointRouteBuilder builder)
	{
		var versionSet = builder.NewApiVersionSet()
								.HasApiVersion(new ApiVersion(1))
								.Build();

		return builder.AddCompanies(versionSet)
#if (filesSupport)
					  .AddCountries(versionSet)
					  .AddFiles(versionSet)
					  .AddProducts(versionSet);
#else
					  .AddCountries(versionSet);
#endif

	}
}