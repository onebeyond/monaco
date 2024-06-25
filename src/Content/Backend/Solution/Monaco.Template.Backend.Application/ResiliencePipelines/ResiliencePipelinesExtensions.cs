using Microsoft.Extensions.DependencyInjection;

namespace Monaco.Template.Backend.Application.ResiliencePipelines;

public static class ResiliencePipelinesExtensions
{
	public static IServiceCollection AddResiliencePipelines(this IServiceCollection services) =>
		// Register additional pipelines chained below
		Common.Application.ResiliencePipelines.ResiliencePipelinesExtensions.AddResiliencePipelines(services);
}