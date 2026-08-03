using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monaco.Template.Backend.Common.Observability;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class ObservabilityConfigurationTests
{
	private const string TestExporterEventSourceName = "Monaco.Template.Backend.Tests.OtlpExporter";

	[Fact(DisplayName = "Startup configuration resolves strict Signal defaults and safe neutral fallbacks")]
	public void StartupConfigurationResolvesStrictSignalDefaultsAndSafeNeutralFallbacks()
	{
		var configuration = CreateConfiguration(("Observability:Signals:Traces:Enabled", "FALSE"),
												("Observability:SqlClient:QueryTextMode", "not-a-mode"),
												("Observability:Identity:Mode", "not-a-mode"));

		var options = ObservabilityOptionsBinder.Bind(configuration, ObservabilityHostProfile.Api);

		Assert.False(options.TracesEnabled);
		Assert.True(options.MetricsEnabled);
		Assert.True(options.LogsEnabled);
		Assert.Equal(ObservabilityQueryTextMode.SummaryOnly, options.QueryTextMode);
		Assert.Equal(ObservabilityIdentityMode.Disabled, options.IdentityMode);
		Assert.Equal(TimeSpan.FromSeconds(5), options.FlushTimeout);
	}

	[Theory(DisplayName = "Numeric QueryTextMode and IdentityMode values fall back safely with a diagnostic")]
	[InlineData("42")]
	[InlineData("-1")]
	[InlineData("0")]
	public void NumericNeutralModesFallBackSafelyWithDiagnostics(string value)
	{
		var options = ObservabilityOptionsBinder.Bind(CreateConfiguration(("Observability:SqlClient:QueryTextMode", value),
																		  ("Observability:Identity:Mode", value)),
													  ObservabilityHostProfile.Api);

		Assert.Equal(ObservabilityQueryTextMode.SummaryOnly, options.QueryTextMode);
		Assert.Equal(ObservabilityIdentityMode.Disabled, options.IdentityMode);
		Assert.Contains(ObservabilityConfigurationDiagnosticCodes.InvalidQueryTextMode, options.DiagnosticCodes);
		Assert.Contains(ObservabilityConfigurationDiagnosticCodes.InvalidIdentityMode, options.DiagnosticCodes);
	}

	[Fact(DisplayName = "Empty neutral values resolve as missing and keep their defaults")]
	public void EmptyNeutralValuesResolveAsMissingAndKeepTheirDefaults()
	{
		var options = ObservabilityOptionsBinder.Bind(CreateConfiguration(("Observability:Signals:Traces:Enabled", ""),
																		  ("Observability:Signals:Metrics:Enabled", ""),
																		  ("Observability:Signals:Logs:Enabled", ""),
																		  ("Observability:Shutdown:FlushTimeout", "")),
													  ObservabilityHostProfile.Api);

		Assert.True(options.TracesEnabled);
		Assert.True(options.MetricsEnabled);
		Assert.True(options.LogsEnabled);
		Assert.Equal(TimeSpan.FromSeconds(5), options.FlushTimeout);
	}

	[Fact(DisplayName = "Empty common OTLP, sampler, and timeout values resolve as missing")]
	public void EmptyCommonRootValuesResolveAsMissing()
	{
		var options = ObservabilityOptionsBinder.Bind(CreateConfiguration(("OTEL_EXPORTER_OTLP_ENDPOINT", ""),
																		  ("OTEL_EXPORTER_OTLP_PROTOCOL", ""),
																		  ("OTEL_EXPORTER_OTLP_TIMEOUT", ""),
																		  ("OTEL_EXPORTER_OTLP_COMPRESSION", ""),
																		  ("OTEL_TRACES_SAMPLER", "")),
													  ObservabilityHostProfile.Api);

		Assert.False(options.SdkDisabled);
		Assert.True(options.TracesEnabled);
	}

	[Fact(DisplayName = "SQL telemetry removes raw statements and emits only a bounded trace-safe summary")]
	public void SqlTelemetryRemovesRawStatementsAndEmitsOnlyABoundedTraceSafeSummary()
	{
		using var activity = new Activity("sql").Start();
		activity.SetTag("db.system.name", "mssql");
		activity.SetTag("db.operation.name", "SELECT");
		activity.SetTag("db.query.text", "SELECT Email FROM Company WHERE Id = @companyId");
		activity.SetTag("db.statement", "SELECT Email FROM Company WHERE Id = @companyId");
		activity.SetTag("db.query.parameter.companyId", "3fa85f64-5717-4562-b3fc-2c963f66afa6");
		activity.SetTag("db.connection_string", "Server=sql.example;Password=secret");

		new SqlClientTelemetryPrivacyProcessor(ObservabilityQueryTextMode.SanitizedText).OnEnd(activity);

		Assert.Equal("SELECT statement", activity.GetTagItem("db.query.text"));
		Assert.Null(activity.GetTagItem("db.statement"));
		Assert.Null(activity.GetTagItem("db.query.parameter.companyId"));
		Assert.Null(activity.GetTagItem("db.connection_string"));
	}

	[Fact(DisplayName = "SQL SummaryOnly and uncertain operations omit all query text")]
	public void SqlSummaryOnlyAndUncertainOperationsOmitAllQueryText()
	{
		using var activity = new Activity("sql").Start();
		activity.SetTag("db.system.name", "mssql");
		activity.SetTag("db.operation.name", "SELECT; DROP TABLE Company");
		activity.SetTag("db.query.text", "SELECT Email FROM Company WHERE Id = @companyId");
		activity.SetTag("db.statement", "SELECT Email FROM Company WHERE Id = @companyId");

		new SqlClientTelemetryPrivacyProcessor(ObservabilityQueryTextMode.SanitizedText).OnEnd(activity);

		Assert.Null(activity.GetTagItem("db.query.text"));
		Assert.Null(activity.GetTagItem("db.statement"));
	}

	[Fact(DisplayName = "Legacy SQL telemetry is scrubbed and fail-closed sanitization reports a fixed diagnostic")]
	public void LegacySqlTelemetryIsScrubbedAndFailClosedSanitizationReportsAFixedDiagnostic()
	{
		using var activity = new Activity("sql").Start();
		activity.SetTag("db.system", "mssql");
		activity.SetTag("db.statement", "SELECT Email FROM Company WHERE Id = @companyId");
		activity.SetTag("db.connection_string", "Server=sql.example;Password=secret");
		activity.SetTag("db.query.parameter.companyId", "3fa85f64-5717-4562-b3fc-2c963f66afa6");
		var logger = new CapturingLogger();
		ObservabilityConfigurationDiagnosticThrottle.Reset();

		new SqlClientTelemetryPrivacyProcessor(ObservabilityQueryTextMode.SanitizedText, ObservabilityHostProfile.Worker, logger).OnEnd(activity);

		Assert.Null(activity.GetTagItem("db.statement"));
		Assert.Null(activity.GetTagItem("db.connection_string"));
		Assert.Null(activity.GetTagItem("db.query.parameter.companyId"));
		Assert.Single(logger.Messages);
		Assert.Contains(ObservabilityConfigurationDiagnosticCodes.SqlSanitizationRejected, logger.Messages[0], StringComparison.Ordinal);
		Assert.Contains("Worker", logger.Messages[0], StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Missing and explicit SQL query modes retain the only supported privacy contract")]
	public void SqlQueryModesRetainTheOnlySupportedPrivacyContract()
	{
		var defaultOptions = ObservabilityOptionsBinder.Bind(CreateConfiguration(), ObservabilityHostProfile.Api);
		var explicitOptions = ObservabilityOptionsBinder.Bind(CreateConfiguration(("Observability:SqlClient:QueryTextMode", "sUmMaRyOnLy")), ObservabilityHostProfile.Api);

		Assert.Equal(ObservabilityQueryTextMode.SanitizedText, defaultOptions.QueryTextMode);
		Assert.Equal(ObservabilityQueryTextMode.SummaryOnly, explicitOptions.QueryTextMode);
		Assert.Equal(2, System.Enum.GetValues<ObservabilityQueryTextMode>().Length);
	}

	[Fact(DisplayName = "HTTP telemetry removes query operands and header values while preserving the path")]
	public void HttpTelemetryRemovesQueryOperandsAndHeaderValuesWhilePreservingThePath()
	{
		using var activity = new Activity("http").Start();
		activity.SetTag("url.path", "/companies/3fa85f64-5717-4562-b3fc-2c963f66afa6");
		activity.SetTag("url.query", "?email=ceo@example.test");
		activity.SetTag("url.full", "https://example.test/companies/3fa85f64-5717-4562-b3fc-2c963f66afa6?email=ceo@example.test");
		activity.SetTag("http.url", "https://example.test/companies/3fa85f64-5717-4562-b3fc-2c963f66afa6?email=ceo@example.test");
		activity.SetTag("http.target", "/companies/3fa85f64-5717-4562-b3fc-2c963f66afa6?email=ceo@example.test");
		activity.SetTag("http.request.header.authorization", "Bearer token");

		new HttpTelemetryPrivacyProcessor().OnEnd(activity);

		Assert.Equal("/companies/3fa85f64-5717-4562-b3fc-2c963f66afa6", activity.GetTagItem("url.path"));
		Assert.Null(activity.GetTagItem("url.query"));
		Assert.Null(activity.GetTagItem("url.full"));
		Assert.Null(activity.GetTagItem("http.url"));
		Assert.Null(activity.GetTagItem("http.target"));
		Assert.Null(activity.GetTagItem("http.request.header.authorization"));
	}

	[Fact(DisplayName = "OTLP self-traffic suppression matches the configured collector only")]
	public void OtlpSelfTrafficSuppressionMatchesTheConfiguredCollectorOnly()
	{
		var collector = new Uri("https://collector.example:4318");

		Assert.True(ObservabilityHostBuilderExtensions.IsOtlpExportRequest(new Uri("https://collector.example:4318/v1/traces"), collector));
		Assert.False(ObservabilityHostBuilderExtensions.IsOtlpExportRequest(new Uri("https://downstream.example:4318/v1/traces"), collector));
	}

	[Fact(DisplayName = "Log telemetry removes message, exception, and structured privacy canaries")]
	public void LogTelemetryRemovesMessageExceptionAndStructuredPrivacyCanaries()
	{
		var capture = new CapturingLogRecordProcessor();
		using var loggerFactory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
		{
			options.IncludeFormattedMessage = true;
			options.ParseStateValues = true;
			options.AddProcessor(new LogTelemetryPrivacyProcessor());
			options.AddProcessor(capture);
		}));
		var logger = loggerFactory.CreateLogger("PrivacyCanary");

		logger.LogError(new InvalidOperationException("Connection string Server=sql.example;Password=secret"),
						"Company email {Email} at {Address} with Authorization {Token}",
						"ceo@example.test",
						"1 Privacy Lane",
						"Bearer token");

		Assert.Null(capture.Body);
		Assert.Null(capture.FormattedMessage);
		Assert.Null(capture.Exception);
		Assert.Null(capture.Attributes);
	}

	[Theory(DisplayName = "Malformed strict Signal and shutdown settings fail before providers are created")]
	[InlineData("Observability:Signals:Traces:Enabled", "yes")]
	[InlineData("Observability:Signals:Metrics:Enabled", "no")]
	[InlineData("Observability:Signals:Logs:Enabled", "1")]
	[InlineData("Observability:Shutdown:FlushTimeout", "00:00:00")]
	[InlineData("Observability:Shutdown:FlushTimeout", "-00:00:01")]
	[InlineData("Observability:Shutdown:FlushTimeout", "not-a-timespan")]
	[InlineData("Observability:Shutdown:FlushTimeout", "00:00:05.001")]
	[InlineData("Observability:Shutdown:FlushTimeout", "00:00:06")]
	[InlineData("OTEL_EXPORTER_OTLP_ENDPOINT", "https://user:pass@localhost:4317")]
	[InlineData("OTEL_EXPORTER_OTLP_ENDPOINT", "not-a-uri")]
	[InlineData("OTEL_EXPORTER_OTLP_PROTOCOL", "http/json")]
	[InlineData("OTEL_EXPORTER_OTLP_TIMEOUT", "10000")]
	[InlineData("OTEL_EXPORTER_OTLP_TIMEOUT", "abc")]
	[InlineData("OTEL_EXPORTER_OTLP_COMPRESSION", "br")]
	[InlineData("OTEL_EXPORTER_OTLP_HEADERS", "authorization")]
	[InlineData("OTEL_EXPORTER_OTLP_HEADERS", "x-auth=a%0db")]
	[InlineData("OTEL_EXPORTER_OTLP_HEADERS", "x-auth=a%0Ab")]
	[InlineData("OTEL_EXPORTER_OTLP_HEADERS", "x-a=b\tc")]
	[InlineData("OTEL_TRACES_SAMPLER", "jaeger")]
	[InlineData("OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY", "in_memory")]
	[InlineData("OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH", "c:\\telemetry")]
	[InlineData("OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS", "true")]
	[InlineData("OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_CONTEXT_PROPAGATION", "true")]
	[InlineData("OTEL_BSP_SCHEDULE_DELAY", "0")]
	[InlineData("OTEL_BSP_EXPORT_TIMEOUT", "5001")]
	[InlineData("OTEL_BSP_MAX_QUEUE_SIZE", "2049")]
	[InlineData("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", "513")]
	[InlineData("OTEL_METRIC_EXPORT_INTERVAL", "60001")]
	[InlineData("OTEL_METRIC_EXPORT_TIMEOUT", "0")]
	[InlineData("OTEL_BLRP_SCHEDULE_DELAY", "5001")]
	[InlineData("OTEL_BLRP_EXPORT_TIMEOUT", "-1")]
	[InlineData("OTEL_BLRP_MAX_QUEUE_SIZE", "0")]
	[InlineData("OTEL_BLRP_MAX_EXPORT_BATCH_SIZE", "513")]
	[InlineData("Logging:LogLevel:Default", "flooble")]
	[InlineData("Logging:LogLevel:Default", "99")]
	[InlineData("Logging:LogLevel:Default", "0")]
	[InlineData("Logging:LogLevel:System", "verbose")]
	public void MalformedStrictSettingsFailBeforeProvidersAreCreated(string key, string value)
	{
		var exception = Assert.Throws<InvalidOperationException>(() => ObservabilityOptionsBinder.Bind(CreateConfiguration((key, value)), ObservabilityHostProfile.Api));

		Assert.Contains(key.StartsWith("Observability:", StringComparison.Ordinal) ? "Observability" : key, exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
	}

#if (massTransitIntegration)
	[Theory(DisplayName = "Malformed MassTransit and generated-namespace log levels fail startup")]
	[InlineData("Logging:LogLevel:MassTransit", "flooble")]
	public void MalformedHostApplicableLogLevelsFailStartup(string key, string value)
	{
		var exception = Assert.Throws<InvalidOperationException>(() => ObservabilityOptionsBinder.Bind(CreateConfiguration((key, value)), ObservabilityHostProfile.Api));

		Assert.Contains(key, exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
	}
#endif

	[Fact(DisplayName = "Malformed generated root namespace log level fails startup")]
	public void MalformedGeneratedRootNamespaceLogLevelFailsStartup()
	{
		var entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "testhost";
		var category = ObservabilityResource.GetSolutionName(ObservabilityHostProfile.Api, entryAssemblyName);

		var exception = Assert.Throws<InvalidOperationException>(() => ObservabilityOptionsBinder.Bind(CreateConfiguration(($"Logging:LogLevel:{category}", "flooble")),
																									   ObservabilityHostProfile.Api));

		Assert.Contains($"Logging:LogLevel:{category}", exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("flooble", exception.Message, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Sampler argument for a non-ratio sampler fails startup")]
	public void SamplerArgumentForNonRatioSamplerFailsStartup() =>
		Assert.Throws<InvalidOperationException>(() => ObservabilityOptionsBinder.Bind(CreateConfiguration(("OTEL_TRACES_SAMPLER", "always_on"),
																										   ("OTEL_TRACES_SAMPLER_ARG", "0.5")),
																					   ObservabilityHostProfile.Api));

	[Theory(DisplayName = "Non-finite or out-of-range sampler ratios fail startup")]
	[InlineData("NaN")]
	[InlineData("1.5")]
	[InlineData("-0.1")]
	[InlineData("abc")]
	public void NonFiniteOrOutOfRangeSamplerRatiosFailStartup(string ratio) =>
		Assert.Throws<InvalidOperationException>(() => ObservabilityOptionsBinder.Bind(CreateConfiguration(("OTEL_TRACES_SAMPLER", "traceidratio"),
																										   ("OTEL_TRACES_SAMPLER_ARG", ratio)),
																					   ObservabilityHostProfile.Api));

	[Fact(DisplayName = "Batch larger than the effective queue fails startup")]
	public void BatchLargerThanEffectiveQueueFailsStartup() =>
		Assert.Throws<InvalidOperationException>(() => ObservabilityOptionsBinder.Bind(CreateConfiguration(("OTEL_BSP_MAX_QUEUE_SIZE", "256"),
																										   ("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", "512")),
																					   ObservabilityHostProfile.Api));

	[Fact(DisplayName = "Inclusive boundaries are accepted")]
	public void InclusiveBoundariesAreAccepted()
	{
		var options = ObservabilityOptionsBinder.Bind(CreateConfiguration(("Observability:Shutdown:FlushTimeout", "00:00:05"),
																		  ("OTEL_EXPORTER_OTLP_TIMEOUT", "5000"),
																		  ("OTEL_BSP_MAX_QUEUE_SIZE", "512"),
																		  ("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", "512"),
																		  ("OTEL_TRACES_SAMPLER", "traceidratio"),
																		  ("OTEL_TRACES_SAMPLER_ARG", "1")),
													  ObservabilityHostProfile.Api);

		Assert.Equal(TimeSpan.FromSeconds(5), options.FlushTimeout);
		Assert.True(options.TracesEnabled);
	}

	[Fact(DisplayName = "Malformed SDK disable values stay enabled and schedule a safe diagnostic")]
	public void MalformedSdkDisableValueStaysEnabledAndSchedulesSafeDiagnostic()
	{
		var options = ObservabilityOptionsBinder.Bind(CreateConfiguration(("OTEL_SDK_DISABLED", "not-a-boolean")),
													  ObservabilityHostProfile.Api);

		Assert.False(options.SdkDisabled);
		Assert.Contains(ObservabilityConfigurationDiagnosticCodes.InvalidSdkDisableValue, options.DiagnosticCodes);
	}

	[Fact(DisplayName = "OTLP preflight resolves signal-specific keys over common keys per Signal independently")]
	public void OtlpPreflightResolvesSignalSpecificKeysOverCommonKeysPerSignalIndependently()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
																			 {
																				 ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://common.example:4317",
																				 ["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = "http://traces.example:4317"
																			 })
													  .AddInMemoryCollection(new Dictionary<string, string?>
																			 {
																				 ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://command-line.example:4317"
																			 })
													  .Build();

		var options = ObservabilityOptionsBinder.Bind(configuration, ObservabilityHostProfile.Api);

		Assert.False(options.SdkDisabled);
		Assert.True(options.TracesEnabled);
		Assert.True(options.MetricsEnabled);
		Assert.True(options.LogsEnabled);

		// A signal-specific value from a lower-priority provider beats a common value from a higher-priority provider.
		var tracesEndpoint = OtelConfigurationPreflightValidator.GetSignalValue(configuration, "TRACES_", "ENDPOINT");
		Assert.Equal("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", tracesEndpoint.Key);
		Assert.Equal("http://traces.example:4317", tracesEndpoint.Value);

		var metricsEndpoint = OtelConfigurationPreflightValidator.GetSignalValue(configuration, "METRICS_", "ENDPOINT");
		Assert.Equal("OTEL_EXPORTER_OTLP_ENDPOINT", metricsEndpoint.Key);
		Assert.Equal("http://command-line.example:4317", metricsEndpoint.Value);
	}

	[Fact(DisplayName = "Global disable leaves Console logging available without an OpenTelemetry provider")]
	public void GlobalDisableLeavesConsoleLoggingAvailableWithoutAnOpenTelemetryProvider() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.Configuration["OTEL_SDK_DISABLED"] = "true";
																	builder.AddApiObservability();

																	using var host = builder.Build();
																	var providers = host.Services.GetServices<ILoggerProvider>().ToArray();

																	Assert.Contains(providers, provider => provider.GetType().Name == "ConsoleLoggerProvider");
																	Assert.DoesNotContain(providers, provider => provider.GetType().Name == "OpenTelemetryLoggerProvider");
																	Assert.Empty(host.Services.GetServices<TracerProvider>());
																	Assert.Empty(host.Services.GetServices<MeterProvider>());
																	Assert.DoesNotContain(host.Services.GetServices<IHostedService>(),
																						  service => service.GetType().Namespace?.StartsWith("OpenTelemetry", StringComparison.Ordinal) is true);
																	Assert.Null(builder.Configuration["OTEL_BLRP_SCHEDULE_DELAY"]);
																});

	[Fact(DisplayName = "All disabled Signals construct no OpenTelemetry provider and apply no defaults")]
	public void AllDisabledSignalsConstructNoOpenTelemetryProvider() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.Configuration["Observability:Signals:Traces:Enabled"] = "false";
																	builder.Configuration["Observability:Signals:Metrics:Enabled"] = "false";
																	builder.Configuration["Observability:Signals:Logs:Enabled"] = "false";
																	builder.AddApiObservability();

																	using var host = builder.Build();

																	Assert.DoesNotContain(host.Services.GetServices<ILoggerProvider>(), provider => provider.GetType().Name == "OpenTelemetryLoggerProvider");
																	Assert.Empty(host.Services.GetServices<TracerProvider>());
																	Assert.Empty(host.Services.GetServices<MeterProvider>());
																	Assert.DoesNotContain(host.Services.GetServices<IHostedService>(),
																						  service => service.GetType().Namespace?.StartsWith("OpenTelemetry", StringComparison.Ordinal) is true);
																	Assert.Null(builder.Configuration["OTEL_BSP_SCHEDULE_DELAY"]);
																});

	[Fact(DisplayName = "Enabled composition registers one provider per enabled Signal and applies adopted defaults")]
	public void EnabledCompositionRegistersProvidersAndAppliesAdoptedDefaults() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.Configuration["OTEL_BSP_EXPORT_TIMEOUT"] = "2500";
																	builder.AddApiObservability();

																	using var host = builder.Build();

																	Assert.Single(host.Services.GetServices<TracerProvider>());
																	Assert.Single(host.Services.GetServices<MeterProvider>());
																	Assert.Single(host.Services.GetServices<ILoggerProvider>(), provider => provider.GetType().Name == "OpenTelemetryLoggerProvider");

																	// Operator-supplied values win; missing keys receive Monaco's adopted defaults.
																	Assert.Equal("2500", builder.Configuration["OTEL_BSP_EXPORT_TIMEOUT"]);
																	Assert.Equal("5000", builder.Configuration["OTEL_EXPORTER_OTLP_TIMEOUT"]);
																	Assert.Equal("5000", builder.Configuration["OTEL_BSP_SCHEDULE_DELAY"]);
																	Assert.Equal("2048", builder.Configuration["OTEL_BSP_MAX_QUEUE_SIZE"]);
																	Assert.Equal("512", builder.Configuration["OTEL_BSP_MAX_EXPORT_BATCH_SIZE"]);
																	Assert.Equal("60000", builder.Configuration["OTEL_METRIC_EXPORT_INTERVAL"]);
																	Assert.Equal("5000", builder.Configuration["OTEL_METRIC_EXPORT_TIMEOUT"]);
																	Assert.Equal("5000", builder.Configuration["OTEL_BLRP_SCHEDULE_DELAY"]);
																	Assert.Equal("5000", builder.Configuration["OTEL_BLRP_EXPORT_TIMEOUT"]);
																	Assert.Equal("2048", builder.Configuration["OTEL_BLRP_MAX_QUEUE_SIZE"]);
																	Assert.Equal("512", builder.Configuration["OTEL_BLRP_MAX_EXPORT_BATCH_SIZE"]);
																});

	[Fact(DisplayName = "Host builder composition captures immutable startup options")]
	public void HostBuilderCompositionCapturesImmutableStartupOptions() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.Configuration["Observability:Signals:Traces:Enabled"] = "false";
																	builder.AddApiObservability();
																	builder.Configuration["Observability:Signals:Traces:Enabled"] = "true";

																	using var host = builder.Build();
																	var options = host.Services.GetRequiredService<IOptions<ObservabilityStartupOptions>>().Value;

																	Assert.False(options.TracesEnabled);
																});

	[Fact(DisplayName = "Enabled composition registers one bounded shutdown coordinator for all enabled providers")]
	public void EnabledCompositionRegistersOneBoundedShutdownCoordinatorForAllEnabledProviders() =>
		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	var builder = Host.CreateApplicationBuilder();
																	builder.AddApiObservability();

																	using var host = builder.Build();
																	var coordinator = Assert.Single(host.Services.GetServices<IHostedService>().OfType<ObservabilityShutdownFlushService>());

																	Assert.Equal(3, coordinator.ProviderCount);
																});

	[Fact(DisplayName = "Configuration and exporter diagnostics are throttled per code and host role")]
	public void ConfigurationAndExporterDiagnosticsAreThrottledPerCodeAndHostRole()
	{
		var timeProvider = new AdjustableTimeProvider(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));
		var logger = new CapturingLogger();
		ObservabilityConfigurationDiagnosticThrottle.Reset();

		ObservabilityConfigurationDiagnosticThrottle.Report(logger, ObservabilityConfigurationDiagnosticCodes.OtlpExportFailure, ObservabilityHostProfile.Api, timeProvider);
		ObservabilityConfigurationDiagnosticThrottle.Report(logger, ObservabilityConfigurationDiagnosticCodes.OtlpExportFailure, ObservabilityHostProfile.Api, timeProvider);
		ObservabilityConfigurationDiagnosticThrottle.Report(logger, ObservabilityConfigurationDiagnosticCodes.OtlpExportFailure, ObservabilityHostProfile.Api, timeProvider);

		timeProvider.Advance(TimeSpan.FromMinutes(5));
		ObservabilityConfigurationDiagnosticThrottle.Report(logger, ObservabilityConfigurationDiagnosticCodes.OtlpExportFailure, ObservabilityHostProfile.Api, timeProvider);

		Assert.Equal(2, logger.Messages.Count);
		Assert.Contains("OBS_OTLP_EXPORT_FAILURE", logger.Messages[0], StringComparison.Ordinal);
		Assert.Contains("Api", logger.Messages[0], StringComparison.Ordinal);
		Assert.Contains("suppressedCount 2", logger.Messages[1], StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Exporter error events use the safe diagnostic seam")]
	public void ExporterErrorEventsUseTheSafeDiagnosticSeam()
	{
		var reports = 0;
		using var listener = new OtlpExporterFailureListener(TestExporterEventSourceName);
		listener.Start(() => reports++);
		using var exporter = new TestOtlpExporterEventSource();

		exporter.ExportFailed();

		SpinWait.SpinUntil(() => reports == 1, TimeSpan.FromSeconds(1));

		Assert.Equal(1, reports);
	}

	[Fact(DisplayName = "Positive sub-millisecond flush timeout rounds up for ForceFlush")]
	public void PositiveSubMillisecondFlushTimeoutRoundsUpForForceFlush() =>
		Assert.Equal(1, ObservabilityShutdownFlushService.GetFlushTimeoutMilliseconds(TimeSpan.FromTicks(1)));

	private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(value => value.Key, value => value.Value)).Build();

	private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;

		public void Advance(TimeSpan duration) => now = now.Add(duration);
	}

	private sealed class CapturingLogger : ILogger
	{
		public List<string> Messages { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Messages.Add(formatter(state, exception));
	}

	private sealed class CapturingLogRecordProcessor : BaseProcessor<LogRecord>
	{
		internal string? Body { get; private set; }

		internal string? FormattedMessage { get; private set; }

		internal Exception? Exception { get; private set; }

		internal IReadOnlyList<KeyValuePair<string, object?>>? Attributes { get; private set; }

		public override void OnEnd(LogRecord data)
		{
			Body = data.Body;
			FormattedMessage = data.FormattedMessage;
			Exception = data.Exception;
			Attributes = data.Attributes;
		}
	}

	[EventSource(Name = TestExporterEventSourceName)]
	private sealed class TestOtlpExporterEventSource : EventSource
	{
		[Event(1, Level = EventLevel.Error)]
		public void ExportFailed() =>
			WriteEvent(1);
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