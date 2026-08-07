using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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

		var options = ObservabilityOptionsBinder.Bind(builder.Configuration, profile);
		services.Add(ServiceDescriptor.Singleton(options));
		services.AddSingleton(Options.Create(options));

		if (!options.SdkDisabled && (options.TracesEnabled || options.MetricsEnabled || options.LogsEnabled))
		{
			// The configuration matrix adopts bounded defaults that diverge from the pinned SDK's own
			// defaults; supply them once at startup so unset keys resolve deterministically while
			// explicit operator values keep winning.
			ApplyAdoptedDefaults(builder.Configuration, options);

			var resource = ObservabilityResource.Create(profile, builder.Configuration);
			var telemetryBuilder = services.AddOpenTelemetry()
										   .ConfigureResource(resourceBuilder => resourceBuilder.AddAttributes(resource.Attributes))
										   .UseOtlpExporter();

			if (options.LogsEnabled)
				telemetryBuilder.WithLogging(_ => { }, ConfigureLogging);
			if (options.TracesEnabled)
				telemetryBuilder.WithTracing(providerBuilder => ConfigureTracing(providerBuilder, builder.Configuration, profile, options.QueryTextMode));
			if (options.MetricsEnabled)
				telemetryBuilder.WithMetrics(providerBuilder => ConfigureMetrics(providerBuilder, profile));

			// IHostedService stops in reverse registration order. Insert this coordinator first so
			// business hosted services stop before the one bounded, concurrent telemetry flush.
			services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ObservabilityShutdownFlushService>());
			services.Insert(1, ServiceDescriptor.Singleton<IHostedService, ObservabilityExporterDiagnosticListener>());
		}

		services.Add(ServiceDescriptor.Singleton(new ObservabilityProfileRegistration(profile)));
		if (options.DiagnosticCodes.Count > 0)
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ObservabilityConfigurationDiagnosticReporter>());

		return builder;
	}

	private static void ApplyAdoptedDefaults(IConfiguration configuration, ObservabilityStartupOptions options)
	{
		SetMissing(configuration, "OTEL_EXPORTER_OTLP_TIMEOUT", "5000");

		if (options.TracesEnabled)
		{
			SetMissing(configuration, "OTEL_BSP_SCHEDULE_DELAY", "5000");
			SetMissing(configuration, "OTEL_BSP_EXPORT_TIMEOUT", "5000");
			SetMissing(configuration, "OTEL_BSP_MAX_QUEUE_SIZE", "2048");
			SetMissing(configuration, "OTEL_BSP_MAX_EXPORT_BATCH_SIZE", "512");
		}

		if (options.MetricsEnabled)
		{
			SetMissing(configuration, "OTEL_METRIC_EXPORT_INTERVAL", "60000");
			SetMissing(configuration, "OTEL_METRIC_EXPORT_TIMEOUT", "5000");
		}

		if (options.LogsEnabled)
		{
			SetMissing(configuration, "OTEL_BLRP_SCHEDULE_DELAY", "5000");
			SetMissing(configuration, "OTEL_BLRP_EXPORT_TIMEOUT", "5000");
			SetMissing(configuration, "OTEL_BLRP_MAX_QUEUE_SIZE", "2048");
			SetMissing(configuration, "OTEL_BLRP_MAX_EXPORT_BATCH_SIZE", "512");
		}
	}

	private static void SetMissing(IConfiguration configuration, string key, string adoptedDefault)
	{
		if (string.IsNullOrEmpty(configuration[key]))
			configuration[key] = adoptedDefault;
	}

	private static void ConfigureLogging(OpenTelemetryLoggerOptions options)
	{
		options.IncludeFormattedMessage = false;
		options.IncludeScopes = false;
		options.ParseStateValues = false;
		options.AddProcessor(new LogTelemetryPrivacyProcessor());
	}

	private static void ConfigureTracing(TracerProviderBuilder builder,
										 IConfiguration configuration,
										 ObservabilityHostProfile profile,
										 ObservabilityQueryTextMode queryTextMode)
	{
		var traceExportEndpoint = GetTraceExportEndpoint(configuration);
		builder.AddProcessor(new HttpTelemetryPrivacyProcessor());

		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Gateway)
			builder.AddAspNetCoreInstrumentation(options => { options.RecordException = false; });

		builder.AddHttpClientInstrumentation(options =>
											 {
												 options.FilterHttpRequestMessage = request => !IsOtlpExportRequest(request.RequestUri, traceExportEndpoint);
												 options.RecordException = false;
											 });

		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Worker)
		{
			builder.AddProcessor(serviceProvider => new SqlClientTelemetryPrivacyProcessor(queryTextMode,
																						   profile,
																						   serviceProvider.GetRequiredService<ILogger<SqlClientTelemetryPrivacyProcessor>>()));
			builder.AddSqlClientInstrumentation(options => { options.RecordException = false; });
		}
	}

	private static Uri GetTraceExportEndpoint(IConfiguration configuration)
	{
		var endpoint = OtelConfigurationPreflightValidator.GetSignalValue(configuration, "TRACES_", "ENDPOINT").Value;
		return new Uri(string.IsNullOrEmpty(endpoint)
						   ? "http://localhost:4317"
						   : endpoint,
					   UriKind.Absolute);
	}

	internal static bool IsOtlpExportRequest(Uri? requestUri, Uri exportEndpoint) =>
		requestUri is not null &&
		requestUri.Scheme.Equals(exportEndpoint.Scheme, StringComparison.OrdinalIgnoreCase) &&
		requestUri.Host.Equals(exportEndpoint.Host, StringComparison.OrdinalIgnoreCase) &&
		requestUri.Port == exportEndpoint.Port &&
		IsOtlpExportPath(requestUri.AbsolutePath);

	private static bool IsOtlpExportPath(string path) =>
		path is "/v1/traces" or
			"/v1/metrics" or
			"/v1/logs" or
			"/opentelemetry.proto.collector.trace.v1.TraceService/Export" or
			"/opentelemetry.proto.collector.metrics.v1.MetricsService/Export" or
			"/opentelemetry.proto.collector.logs.v1.LogsService/Export";

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