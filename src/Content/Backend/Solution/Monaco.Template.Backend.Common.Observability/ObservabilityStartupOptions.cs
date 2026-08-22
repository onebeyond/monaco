using Microsoft.Extensions.Configuration;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed record ObservabilityStartupOptions(bool SdkDisabled,
												   bool TracesEnabled,
												   bool MetricsEnabled,
												   bool LogsEnabled)
{
	internal static ObservabilityStartupOptions Disabled { get; } = new(true, false, false, false);
}

internal static class ObservabilityOptionsBinder
{
	internal static ObservabilityStartupOptions Bind(IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		return configuration["OTEL_SDK_DISABLED"]?.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) == true
				   ? ObservabilityStartupOptions.Disabled
				   : new(false,
						 IsSignalEnabled(configuration, "Traces"),
						 IsSignalEnabled(configuration, "Metrics"),
						 IsSignalEnabled(configuration, "Logs"));
	}

	private static bool IsSignalEnabled(IConfiguration configuration, string signal)
	{
		var value = configuration[$"Observability:Signals:{signal}:Enabled"];
		return string.IsNullOrEmpty(value) || bool.Parse(value);
	}
}