using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Monaco.Template.Backend.Common.Application.ResiliencePipelines;

public static class ResiliencePipelinesExtensions
{
	public const string DbConcurrentExceptionPipelineKey = "DbConcurrentExceptionPipeline";

	public static IServiceCollection AddResiliencePipelines(this IServiceCollection services) =>
		services.AddResiliencePipeline(DbConcurrentExceptionPipelineKey,
									   builder => builder.AddRetry(new()
																   {
																	   ShouldHandle = new PredicateBuilder().Handle<DbUpdateConcurrencyException>(),
																	   MaxRetryAttempts = 3,
																	   Delay = TimeSpan.FromSeconds(1),
																	   BackoffType = DelayBackoffType.Linear,
																	   MaxDelay = TimeSpan.FromSeconds(3),
																	   UseJitter = true
																   }));
}