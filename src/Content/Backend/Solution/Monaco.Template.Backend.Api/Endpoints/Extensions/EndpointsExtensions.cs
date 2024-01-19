using Asp.Versioning;

namespace Monaco.Template.Backend.Api.Endpoints.Extensions;

public static class EndpointsExtensions
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
					  .AddCountries(versionSet)
					  .AddFiles(versionSet)
					  .AddProducts(versionSet);
	}
}