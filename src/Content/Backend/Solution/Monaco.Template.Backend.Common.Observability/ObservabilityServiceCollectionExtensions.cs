using Microsoft.Extensions.DependencyInjection;

namespace Monaco.Template.Backend.Common.Observability;

public static class ObservabilityServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddApiObservabilityProfile() => AddProfile(services, ObservabilityHostProfile.Api);

		public IServiceCollection AddWorkerObservabilityProfile() => AddProfile(services, ObservabilityHostProfile.Worker);

		public IServiceCollection AddGatewayObservabilityProfile() => AddProfile(services, ObservabilityHostProfile.Gateway);
	}

	private static IServiceCollection AddProfile(IServiceCollection services, ObservabilityHostProfile profile)
	{
		services.Add(ServiceDescriptor.Singleton(new ObservabilityProfileRegistration(profile)));
		return services;
	}
}

internal enum ObservabilityHostProfile
{
	Api,
	Worker,
	Gateway
}

internal sealed record ObservabilityProfileRegistration(ObservabilityHostProfile Profile)
{
	internal static readonly HashSet<ObservabilityInstrumentation> ApiInstrumentations =
	[
		ObservabilityInstrumentation.Runtime,
		ObservabilityInstrumentation.AspNetCore,
		ObservabilityInstrumentation.Http,
		ObservabilityInstrumentation.SqlClient
	];

	internal static readonly HashSet<ObservabilityInstrumentation> WorkerInstrumentations =
	[
		ObservabilityInstrumentation.Runtime,
		ObservabilityInstrumentation.Http,
		ObservabilityInstrumentation.SqlClient
	];

	internal static readonly HashSet<ObservabilityInstrumentation> GatewayInstrumentations =
	[
		ObservabilityInstrumentation.Runtime,
		ObservabilityInstrumentation.AspNetCore,
		ObservabilityInstrumentation.Http
	];
}

internal enum ObservabilityInstrumentation
{
	Runtime,
	AspNetCore,
	Http,
	SqlClient
}