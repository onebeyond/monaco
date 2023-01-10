using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;

namespace Monaco.Template.Common.Application.Policies;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPolicies<T>(this IServiceCollection services) where T : Policies =>
		services.AddSingleton<T>()
				.AddSingleton<IReadOnlyPolicyRegistry<string>, PolicyRegistry>(provider => provider.GetRequiredService<T>()
																								   .GetPolicyRegistry());
}