using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class ObservabilityShutdownFlushService(ObservabilityStartupOptions options,
														ObservabilityProfileRegistration profile,
														ILogger<ObservabilityShutdownFlushService> logger,
														TracerProvider? tracerProvider = null,
														MeterProvider? meterProvider = null,
														LoggerProvider? loggerProvider = null) : IHostedService
{
	private readonly object[] _providers = new object?[] { tracerProvider, meterProvider, loggerProvider }.OfType<object>().ToArray();

	internal int ProviderCount => _providers.Length;

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_providers.Length == 0)
			return;

		var flush = Task.WhenAll(_providers.Select(FlushProvider));
		if (!(await flush).All(static succeeded => succeeded))
			ObservabilityConfigurationDiagnosticThrottle.Report(logger, ObservabilityConfigurationDiagnosticCodes.OtlpExportFailure, profile.Profile);
	}

	private Task<bool> FlushProvider(object provider) =>
		Task.Run(() =>
				 {
					 try
					 {
						 var timeoutMilliseconds = GetFlushTimeoutMilliseconds(options.FlushTimeout);

						 return provider switch
								{
									TracerProvider tracerProvider => tracerProvider.ForceFlush(timeoutMilliseconds),
									MeterProvider meterProvider => meterProvider.ForceFlush(timeoutMilliseconds),
									LoggerProvider loggerProvider => loggerProvider.ForceFlush(timeoutMilliseconds),
									_ => true
								};
					 }
					 catch
					 {
						 return false;
					 }
				 });

	internal static int GetFlushTimeoutMilliseconds(TimeSpan timeout) =>
		Math.Clamp((int)Math.Ceiling(timeout.TotalMilliseconds), 1, 5000);
}