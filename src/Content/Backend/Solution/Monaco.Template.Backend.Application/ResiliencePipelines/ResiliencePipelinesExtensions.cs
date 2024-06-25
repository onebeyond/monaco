using Microsoft.Extensions.DependencyInjection;
using CommonResiliencePipelinesExtensions = Monaco.Template.Backend.Common.Application.ResiliencePipelines.ResiliencePipelinesExtensions;

namespace Monaco.Template.Backend.Application.ResiliencePipelines;

public static class ResiliencePipelinesExtensions
{
	public static IServiceCollection AddResiliencePipelines(this IServiceCollection services) =>
		// Register additional pipelines chained below
		CommonResiliencePipelinesExtensions.AddResiliencePipelines(services);
}