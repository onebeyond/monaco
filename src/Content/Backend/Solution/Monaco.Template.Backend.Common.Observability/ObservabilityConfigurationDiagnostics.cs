using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

internal static class ObservabilityConfigurationDiagnosticCodes
{
	internal const string InvalidSdkDisableValue = "OBS_CONFIG_INVALID_SDK_DISABLE";
	internal const string InvalidQueryTextMode = "OBS_CONFIG_INVALID_QUERY_TEXT_MODE";
	internal const string InvalidIdentityMode = "OBS_CONFIG_INVALID_IDENTITY_MODE";
	internal const string InvalidHmacKey = "OBS_CONFIG_INVALID_HMAC_KEY";
	internal const string OtlpExportFailure = "OBS_OTLP_EXPORT_FAILURE";
	internal const string SqlSanitizationRejected = "OBS_SQL_SANITIZATION_REJECTED";
}

internal sealed class ObservabilityConfigurationDiagnosticReporter(ObservabilityStartupOptions options,
																   ObservabilityProfileRegistration profile,
																   ILogger<ObservabilityConfigurationDiagnosticReporter> logger) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		foreach (var code in options.DiagnosticCodes)
			ObservabilityConfigurationDiagnosticThrottle.Report(logger, code, profile.Profile);

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class ObservabilityConfigurationDiagnosticThrottle
{
	private static readonly ConcurrentDictionary<(string Code, ObservabilityHostProfile Profile), DiagnosticState> States = [];

	internal static void Report(ILogger logger, string code, ObservabilityHostProfile profile, TimeProvider? timeProvider = null)
	{
		var state = States.GetOrAdd((code, profile), static _ => new DiagnosticState());
		var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

		lock (state)
		{
			if (!state.Emitted)
			{
				state.Emitted = true;
				state.NextSummaryAt = now.AddMinutes(5);
				logger.LogWarning("Observability configuration diagnostic {Code} for host role {HostRole}.", code, profile);
				return;
			}

			if (now < state.NextSummaryAt)
			{
				state.SuppressedCount++;
				return;
			}

			if (state.SuppressedCount > 0)
				logger.LogWarning("Observability configuration diagnostic summary {Code} for host role {HostRole}; suppressedCount {SuppressedCount}.", code, profile, state.SuppressedCount);

			state.SuppressedCount = 0;
			state.NextSummaryAt = now.AddMinutes(5);
		}
	}

	internal static void Reset() =>
		States.Clear();

	private sealed class DiagnosticState
	{
		internal bool Emitted { get; set; }
		internal DateTimeOffset NextSummaryAt { get; set; }
		internal int SuppressedCount { get; set; }
	}
}