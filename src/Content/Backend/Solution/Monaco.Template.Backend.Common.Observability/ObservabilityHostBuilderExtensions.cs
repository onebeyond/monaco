using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Monaco.Template.Backend.Common.Observability;

public static class ObservabilityHostBuilderExtensions
{
	private const string ApplicationMeterName = "Monaco.Template.Backend.Application";

	extension(IHostApplicationBuilder builder)
	{
		public IHostApplicationBuilder AddApiObservability() =>
			AddProfile(builder, ObservabilityHostProfile.Api);

		public IHostApplicationBuilder AddWorkerObservability() =>
			AddProfile(builder, ObservabilityHostProfile.Worker);

		public IHostApplicationBuilder AddGatewayObservability() =>
			AddProfile(builder, ObservabilityHostProfile.Gateway);
	}

	extension(IApplicationBuilder app)
	{
		public IApplicationBuilder UseIdentityEnrichment() =>
			app.UseMiddleware<IdentityEnrichmentMiddleware>();
	}

	private static IHostApplicationBuilder AddProfile(IHostApplicationBuilder builder, ObservabilityHostProfile profile)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var services = builder.Services;
		if (services.Any(descriptor => descriptor.ServiceType == typeof(ObservabilityProfileRegistration)))
			throw new InvalidOperationException("Observability profile is already registered for this host.");

		var options = ObservabilityOptionsBinder.Bind(builder.Configuration);
		services.Add(ServiceDescriptor.Singleton(options));
		services.AddSingleton(Options.Create(options));
		services.Add(ServiceDescriptor.Singleton(new ObservabilityProfileRegistration(profile)));

		if (options.SdkDisabled || options is { TracesEnabled: false, MetricsEnabled: false, LogsEnabled: false })
			return builder;

		var telemetryBuilder = services.AddOpenTelemetry()
									   .ConfigureResource(resourceBuilder => ObservabilityResource.Configure(resourceBuilder,
																											 profile,
																											 builder.Environment.ApplicationName,
																											 builder.Environment.EnvironmentName))
									   .UseOtlpExporter();

		if (options.LogsEnabled)
			telemetryBuilder.WithLogging(_ => { }, ConfigureLogging);
		if (options.TracesEnabled)
			telemetryBuilder.WithTracing(providerBuilder => ConfigureTracing(providerBuilder, profile));
		if (options.MetricsEnabled)
			telemetryBuilder.WithMetrics(providerBuilder => ConfigureMetrics(providerBuilder, profile));

		return builder;
	}

	private static void ConfigureLogging(OpenTelemetryLoggerOptions options)
	{
		options.IncludeFormattedMessage = true;
		options.IncludeScopes = true;
		options.ParseStateValues = true;
	}

	private static void ConfigureTracing(TracerProviderBuilder builder, ObservabilityHostProfile profile)
	{
		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Gateway)
			builder.AddAspNetCoreInstrumentation();

		if (profile is ObservabilityHostProfile.Gateway)
			builder.AddSource("Yarp.ReverseProxy");

		builder.AddHttpClientInstrumentation();

		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Worker)
			builder.AddSqlClientInstrumentation();
	}

	private static void ConfigureMetrics(MeterProviderBuilder builder, ObservabilityHostProfile profile)
	{
		builder.AddRuntimeInstrumentation();

		if (profile is ObservabilityHostProfile.Api)
			builder.AddMeter(ApplicationMeterName);

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