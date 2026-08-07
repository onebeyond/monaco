using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed record ObservabilityStartupOptions(bool SdkDisabled,
												   bool TracesEnabled,
												   bool MetricsEnabled,
												   bool LogsEnabled,
												   ObservabilityQueryTextMode QueryTextMode,
												   ObservabilityIdentityMode IdentityMode,
												   byte[]? HmacSha256KeyBytes,
												   TimeSpan FlushTimeout,
												   IReadOnlyList<string> DiagnosticCodes)
{
	internal static ObservabilityStartupOptions Disabled { get; } = new(true,
																		false,
																		false,
																		false,
																		ObservabilityQueryTextMode.SanitizedText,
																		ObservabilityIdentityMode.Disabled,
																		null,
																		TimeSpan.FromSeconds(5),
																		[]);
}

internal static class ObservabilityOptionsBinder
{
	internal static ObservabilityStartupOptions Bind(IConfiguration configuration, ObservabilityHostProfile profile)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var preflight = OtelConfigurationPreflightValidator.Validate(configuration, profile);
		if (preflight.SdkDisabled)
			return ObservabilityStartupOptions.Disabled;

		var sectionOptions = BindSection(configuration);
		Validate(sectionOptions);

		var diagnosticCodes = new List<string>(preflight.DiagnosticCodes);
		var identityMode = ParseIdentityMode(sectionOptions.Identity.Mode, diagnosticCodes);
		var hmacKeyBytes = ParseHmacSha256Key(sectionOptions.Identity.HmacSha256Key, identityMode, diagnosticCodes);

		return new(false,
				   ParseToggle(sectionOptions.Signals.Traces.Enabled),
				   ParseToggle(sectionOptions.Signals.Metrics.Enabled),
				   ParseToggle(sectionOptions.Signals.Logs.Enabled),
				   ParseQueryTextMode(sectionOptions.SqlClient.QueryTextMode, diagnosticCodes),
				   identityMode,
				   hmacKeyBytes,
				   ParseFlushTimeout(sectionOptions.Shutdown.FlushTimeout),
				   diagnosticCodes);
	}

	private static bool ParseToggle(string? value) => string.IsNullOrEmpty(value) || bool.Parse(value);

	private static TimeSpan ParseFlushTimeout(string? value) =>
		string.IsNullOrEmpty(value) ? TimeSpan.FromSeconds(5) : TimeSpan.ParseExact(value, "c", CultureInfo.InvariantCulture);

	private static ObservabilitySectionOptions BindSection(IConfiguration configuration)
	{
		var options = new ObservabilitySectionOptions();
		try
		{
			configuration.GetSection(ObservabilitySectionOptions.SectionName).Bind(options);
			return options;
		}
		catch (InvalidOperationException exception)
		{
			throw new InvalidOperationException("Invalid Observability configuration.", exception);
		}
	}

	private static void Validate(ObservabilitySectionOptions options)
	{
		var result = new ObservabilitySectionOptionsValidator().Validate(Options.DefaultName, options);
		if (result.Failed)
			throw new InvalidOperationException(result.FailureMessage);
	}

	private static ObservabilityQueryTextMode ParseQueryTextMode(string? value, List<string> diagnosticCodes)
	{
		if (string.IsNullOrEmpty(value))
			return ObservabilityQueryTextMode.SanitizedText;

		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
			Enum.TryParse<ObservabilityQueryTextMode>(value, true, out var mode) && Enum.IsDefined(mode))
			return mode;

		diagnosticCodes.Add(ObservabilityConfigurationDiagnosticCodes.InvalidQueryTextMode);
		return ObservabilityQueryTextMode.SummaryOnly;
	}

	private static ObservabilityIdentityMode ParseIdentityMode(string? value, List<string> diagnosticCodes)
	{
		if (string.IsNullOrEmpty(value))
			return ObservabilityIdentityMode.Subject;

		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
			Enum.TryParse<ObservabilityIdentityMode>(value, true, out var mode) && Enum.IsDefined(mode))
			return mode;

		diagnosticCodes.Add(ObservabilityConfigurationDiagnosticCodes.InvalidIdentityMode);
		return ObservabilityIdentityMode.Disabled;
	}

	private static byte[]? ParseHmacSha256Key(string? keyValue, ObservabilityIdentityMode mode, List<string> diagnosticCodes)
	{
		if (mode != ObservabilityIdentityMode.HmacSha256)
			return null;

		if (HmacSha256Identity.TryDecodeKey(keyValue, out var keyBytes, out var diagnosticCode))
			return keyBytes;

		if (diagnosticCode is not null)
			diagnosticCodes.Add(diagnosticCode);

		return null;
	}
}

internal sealed class ObservabilitySectionOptions
{
	internal const string SectionName = "Observability";

	public ObservabilitySignalOptions Signals { get; set; } = new();

	public ObservabilitySqlClientOptions SqlClient { get; set; } = new();

	public ObservabilityIdentityOptions Identity { get; set; } = new();

	public ObservabilityShutdownOptions Shutdown { get; set; } = new();
}

internal sealed class ObservabilitySignalOptions
{
	public ObservabilitySignalEnabledOptions Traces { get; set; } = new();

	public ObservabilitySignalEnabledOptions Metrics { get; set; } = new();

	public ObservabilitySignalEnabledOptions Logs { get; set; } = new();
}

internal sealed class ObservabilitySignalEnabledOptions
{
	public string? Enabled { get; set; }
}

internal sealed class ObservabilitySqlClientOptions
{
	public string? QueryTextMode { get; set; }
}

internal sealed class ObservabilityIdentityOptions
{
	public string? Mode { get; set; }

	public string? HmacSha256Key { get; set; }
}

internal sealed class ObservabilityShutdownOptions
{
	public string? FlushTimeout { get; set; }
}

internal sealed class ObservabilitySectionOptionsValidator : IValidateOptions<ObservabilitySectionOptions>
{
	public ValidateOptionsResult Validate(string? name, ObservabilitySectionOptions options)
	{
		foreach (var (key, value) in StrictToggles(options))
			if (!string.IsNullOrEmpty(value) && !bool.TryParse(value, out _))
				return ValidateOptionsResult.Fail($"Invalid {key}.");

		var flushTimeout = options.Shutdown.FlushTimeout;
		if (string.IsNullOrEmpty(flushTimeout))
			return ValidateOptionsResult.Success;

		return TimeSpan.TryParseExact(flushTimeout, "c", CultureInfo.InvariantCulture, out var parsed) &&
			   parsed > TimeSpan.Zero && parsed <= TimeSpan.FromSeconds(5)
				   ? ValidateOptionsResult.Success
				   : ValidateOptionsResult.Fail("Invalid Observability:Shutdown:FlushTimeout.");
	}

	private static IEnumerable<(string Key, string? Value)> StrictToggles(ObservabilitySectionOptions options)
	{
		yield return ("Observability:Signals:Traces:Enabled", options.Signals.Traces.Enabled);
		yield return ("Observability:Signals:Metrics:Enabled", options.Signals.Metrics.Enabled);
		yield return ("Observability:Signals:Logs:Enabled", options.Signals.Logs.Enabled);
	}
}

internal enum ObservabilityQueryTextMode
{
	SanitizedText,
	SummaryOnly
}

internal enum ObservabilityIdentityMode
{
	Subject,
	HmacSha256,
	Disabled
}