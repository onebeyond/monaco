using System.Diagnostics;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monaco.Template.Backend.Common.Observability;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class ObservabilityConfigurationTests
{
	[Fact(DisplayName = "Signals are enabled by default without telemetry policy options")]
	public void SignalsAreEnabledByDefaultWithoutTelemetryPolicyOptions()
	{
		var options = ObservabilityOptionsBinder.Bind(CreateConfiguration());

		options.SdkDisabled.Should().BeFalse();
		options.TracesEnabled.Should().BeTrue();
		options.MetricsEnabled.Should().BeTrue();
		options.LogsEnabled.Should().BeTrue();
	}

	[Fact(DisplayName = "SDK disablement dominates malformed Signal settings and omits every provider")]
	public void SdkDisablementDominatesMalformedSignalSettingsAndOmitsEveryProvider() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.Configuration["OTEL_SDK_DISABLED"] = "true";
																	builder.Configuration["Observability:Signals:Traces:Enabled"] = "not-a-boolean";

																	builder.AddApiObservability();

																	using var host = builder.Build();
																	host.Services.GetServices<TracerProvider>().Should().BeEmpty();
																	host.Services.GetServices<MeterProvider>().Should().BeEmpty();
																	host.Services.GetServices<LoggerProvider>().Should().BeEmpty();
																});

	[Fact(DisplayName = "Disabled Signals omit providers while Console logging remains available")]
	public void DisabledSignalsOmitProvidersWhileConsoleLoggingRemainsAvailable() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.Configuration["Observability:Signals:Traces:Enabled"] = "false";
																	builder.Configuration["Observability:Signals:Metrics:Enabled"] = "false";
																	builder.Configuration["Observability:Signals:Logs:Enabled"] = "false";

																	builder.AddApiObservability();

																	using var host = builder.Build();
																	host.Services.GetServices<TracerProvider>().Should().BeEmpty();
																	host.Services.GetServices<MeterProvider>().Should().BeEmpty();
																	host.Services.GetServices<LoggerProvider>().Should().BeEmpty();
																	host.Services.GetServices<ILoggerProvider>().Should().Contain(provider => provider.GetType().Name == "ConsoleLoggerProvider");
																});

	[Fact(DisplayName = "Enabled composition creates one provider for each Signal")]
	public void EnabledCompositionCreatesOneProviderForEachSignal() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.AddApiObservability();

																	using var host = builder.Build();
																	host.Services.GetServices<TracerProvider>().Should().ContainSingle();
																	host.Services.GetServices<MeterProvider>().Should().ContainSingle();
																	host.Services.GetServices<LoggerProvider>().Should().ContainSingle();
																});

	[Fact(DisplayName = "Standard OpenTelemetry settings pass through without Monaco validation or mutation")]
	public void StandardOpenTelemetrySettingsPassThroughWithoutMonacoValidationOrMutation()
	{
		var builder = Host.CreateApplicationBuilder();
		builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "consumer-selected-endpoint";
		builder.Configuration["OTEL_TRACES_SAMPLER"] = "consumer-selected-sampler";
		builder.Configuration["OTEL_BSP_MAX_QUEUE_SIZE"] = "consumer-selected-queue";
		builder.Configuration["OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY"] = "consumer-selected-retry";

		builder.AddApiObservability();

		builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"].Should().Be("consumer-selected-endpoint");
		builder.Configuration["OTEL_TRACES_SAMPLER"].Should().Be("consumer-selected-sampler");
		builder.Configuration["OTEL_BSP_MAX_QUEUE_SIZE"].Should().Be("consumer-selected-queue");
		builder.Configuration["OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY"].Should().Be("consumer-selected-retry");
	}

	[Fact(DisplayName = "Host composition captures one immutable Signal snapshot")]
	public void HostCompositionCapturesOneImmutableSignalSnapshot()
	{
		var builder = Host.CreateApplicationBuilder();
		builder.Configuration["Observability:Signals:Traces:Enabled"] = "false";
		builder.AddApiObservability();
		builder.Configuration["Observability:Signals:Traces:Enabled"] = "true";

		using var host = builder.Build();
		host.Services.GetRequiredService<IOptions<ObservabilityStartupOptions>>().Value.TracesEnabled.Should().BeFalse();
	}

	[Fact(DisplayName = "A host cannot register more than one observability profile")]
	public void HostCannotRegisterMoreThanOneObservabilityProfile()
	{
		var builder = Host.CreateApplicationBuilder();
		builder.AddApiObservability();

		var action = builder.AddWorkerObservability;

		action.Should().Throw<InvalidOperationException>();
	}

	[Fact(DisplayName = "Authenticated requests attach the raw validated subject to the entry span and log scope")]
	public async Task AuthenticatedRequestsAttachRawValidatedSubjectToEntrySpanAndLogScope()
	{
		const string subject = "raw-subject-248289761001";
		var logger = new CapturingLogger<IdentityEnrichmentMiddleware>();
		var context = new DefaultHttpContext
					  {
						  User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "validated"))
					  };
		using var activity = new Activity("request").Start();
		var nextCalled = false;
		var middleware = new IdentityEnrichmentMiddleware(_ =>
														  {
															  nextCalled = true;
															  return Task.CompletedTask;
														  },
														  logger);

		await middleware.InvokeAsync(context);

		nextCalled.Should().BeTrue();
		activity.GetTagItem("user.id").Should().Be(subject);
		logger.Scopes.Should().ContainSingle()
			  .Which.Should().Contain(new KeyValuePair<string, object?>("user.id", subject));
	}

	[Fact(DisplayName = "Unauthenticated requests continue without identity telemetry")]
	public async Task UnauthenticatedRequestsContinueWithoutIdentityTelemetry()
	{
		var logger = new CapturingLogger<IdentityEnrichmentMiddleware>();
		var context = new DefaultHttpContext();
		using var activity = new Activity("request").Start();
		var nextCalled = false;
		var middleware = new IdentityEnrichmentMiddleware(_ =>
														  {
															  nextCalled = true;
															  return Task.CompletedTask;
														  },
														  logger);

		await middleware.InvokeAsync(context);

		nextCalled.Should().BeTrue();
		activity.GetTagItem("user.id").Should().BeNull();
		logger.Scopes.Should().BeEmpty();
	}

	[Fact(DisplayName = "Boundary exception handling logs the original exception without mutating the Activity")]
	public async Task BoundaryExceptionHandlingLogsOriginalExceptionWithoutMutatingActivity()
	{
		var logger = new CapturingLogger<BoundaryExceptionHandler>();
		var handler = new BoundaryExceptionHandler(logger);
		var context = new DefaultHttpContext();
		var exception = new InvalidOperationException("native exception detail");
		using var activity = new Activity("request").Start();
		activity.SetTag("exception.type", "existing");
		activity.AddEvent(new ActivityEvent("existing-event"));

		var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

		handled.Should().BeTrue();
		context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
		logger.Entries.Should().ContainSingle(entry => entry.LogLevel == LogLevel.Error && ReferenceEquals(entry.Exception, exception));
		activity.GetTagItem("exception.type").Should().Be("existing");
		activity.Events.Should().ContainSingle(@event => @event.Name == "existing-event");
	}

	private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(value => value.Key, value => value.Value)).Build();

	private sealed record CapturedLogEntry(LogLevel LogLevel, Exception? Exception);

	private sealed class CapturingLogger<T> : ILogger<T>
	{
		internal List<IReadOnlyCollection<KeyValuePair<string, object?>>> Scopes { get; } = [];
		internal List<CapturedLogEntry> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			if (state is IReadOnlyCollection<KeyValuePair<string, object?>> fields)
				Scopes.Add(fields);

			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel,
								EventId eventId,
								TState state,
								Exception? exception,
								Func<TState, Exception?, string> formatter) =>
			Entries.Add(new CapturedLogEntry(logLevel, exception));
	}

	private sealed class NullScope : IDisposable
	{
		internal static NullScope Instance { get; } = new();

		public void Dispose()
		{
		}
	}
}

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
}