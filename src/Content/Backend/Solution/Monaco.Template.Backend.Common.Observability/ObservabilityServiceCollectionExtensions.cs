using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
		if (services.Any(descriptor => descriptor.ServiceType == typeof(ObservabilityProfileRegistration)))
			throw new InvalidOperationException("Observability profile is already registered for this host.");

		var resource = ObservabilityResource.Create(profile);

		services.AddOpenTelemetry()
				.ConfigureResource(builder => builder.AddAttributes(resource.Attributes))
				.UseOtlpExporter()
				.WithLogging(_ => { }, ConfigureLogging)
				.WithTracing(builder => ConfigureTracing(builder, profile))
				.WithMetrics(builder => ConfigureMetrics(builder, profile));

		services.Add(ServiceDescriptor.Singleton(new ObservabilityProfileRegistration(profile)));
		return services;
	}

	private static void ConfigureLogging(OpenTelemetryLoggerOptions options)
	{
		options.IncludeFormattedMessage = true;
		options.IncludeScopes = true;
		options.ParseStateValues = false;
	}

	private static void ConfigureTracing(TracerProviderBuilder builder, ObservabilityHostProfile profile)
	{
		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Gateway)
			builder.AddAspNetCoreInstrumentation();

		builder.AddHttpClientInstrumentation();

		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Worker)
			builder.AddSqlClientInstrumentation();
	}

	private static void ConfigureMetrics(MeterProviderBuilder builder, ObservabilityHostProfile profile)
	{
		builder.AddRuntimeInstrumentation();

		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Gateway)
			builder.AddAspNetCoreInstrumentation();

		builder.AddHttpClientInstrumentation();

		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Worker)
			builder.AddSqlClientInstrumentation();
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