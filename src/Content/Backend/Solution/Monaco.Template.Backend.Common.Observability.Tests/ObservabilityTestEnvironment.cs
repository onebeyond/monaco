using Microsoft.Extensions.Hosting;

namespace Monaco.Template.Backend.Common.Observability.Tests;

internal static class ObservabilityTestEnvironment
{
	internal static readonly Lock SharedLock = new();

	private static readonly string[] OtelEnvironmentKeys =
	[
		"OTEL_SDK_DISABLED",
		"OTEL_SERVICE_NAME",
		"OTEL_RESOURCE_ATTRIBUTES",
		"OTEL_EXPORTER_OTLP_ENDPOINT",
		"OTEL_EXPORTER_OTLP_PROTOCOL",
		"OTEL_EXPORTER_OTLP_HEADERS",
		"OTEL_EXPORTER_OTLP_TIMEOUT",
		"OTEL_EXPORTER_OTLP_COMPRESSION",
		"OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
		"OTEL_EXPORTER_OTLP_TRACES_PROTOCOL",
		"OTEL_EXPORTER_OTLP_TRACES_HEADERS",
		"OTEL_EXPORTER_OTLP_TRACES_TIMEOUT",
		"OTEL_EXPORTER_OTLP_TRACES_COMPRESSION",
		"OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
		"OTEL_EXPORTER_OTLP_METRICS_PROTOCOL",
		"OTEL_EXPORTER_OTLP_METRICS_HEADERS",
		"OTEL_EXPORTER_OTLP_METRICS_TIMEOUT",
		"OTEL_EXPORTER_OTLP_METRICS_COMPRESSION",
		"OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
		"OTEL_EXPORTER_OTLP_LOGS_PROTOCOL",
		"OTEL_EXPORTER_OTLP_LOGS_HEADERS",
		"OTEL_EXPORTER_OTLP_LOGS_TIMEOUT",
		"OTEL_EXPORTER_OTLP_LOGS_COMPRESSION",
		"OTEL_TRACES_SAMPLER",
		"OTEL_TRACES_SAMPLER_ARG",
		"OTEL_BSP_SCHEDULE_DELAY",
		"OTEL_BSP_EXPORT_TIMEOUT",
		"OTEL_BSP_MAX_QUEUE_SIZE",
		"OTEL_BSP_MAX_EXPORT_BATCH_SIZE",
		"OTEL_METRIC_EXPORT_INTERVAL",
		"OTEL_METRIC_EXPORT_TIMEOUT",
		"OTEL_BLRP_SCHEDULE_DELAY",
		"OTEL_BLRP_EXPORT_TIMEOUT",
		"OTEL_BLRP_MAX_QUEUE_SIZE",
		"OTEL_BLRP_MAX_EXPORT_BATCH_SIZE",
		"OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY",
		"OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH",
		"OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS",
		"OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_CONTEXT_PROPAGATION"
	];

	internal static void WithClearedOtelEnvironment(Action action)
	{
		lock (SharedLock)
		{
			var previousValues = OtelEnvironmentKeys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
			try
			{
				foreach (var key in OtelEnvironmentKeys)
					Environment.SetEnvironmentVariable(key, null);

				action();
			}
			finally
			{
				foreach (var (key, value) in previousValues)
					Environment.SetEnvironmentVariable(key, value);
			}
		}
	}

	internal static IHost BuildHost(Action<IHostApplicationBuilder> addProfile, Action<IHostApplicationBuilder>? configure = null)
	{
		var builder = Host.CreateApplicationBuilder();
		configure?.Invoke(builder);
		addProfile(builder);
		return builder.Build();
	}
}