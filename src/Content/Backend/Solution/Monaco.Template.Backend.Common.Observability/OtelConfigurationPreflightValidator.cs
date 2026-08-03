using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed record OtelConfigurationPreflightResult(bool SdkDisabled, IReadOnlyList<string> DiagnosticCodes);

internal static class OtelConfigurationPreflightValidator
{
	internal static OtelConfigurationPreflightResult Validate(IConfiguration configuration, ObservabilityHostProfile profile)
	{
		if (IsSdkDisabled(configuration["OTEL_SDK_DISABLED"], out var diagnosticCodes))
			return new(true, diagnosticCodes);

		ValidateResourceSettings(configuration);
		ValidateOtlpSettings(configuration, "TRACES_");
		ValidateOtlpSettings(configuration, "METRICS_");
		ValidateOtlpSettings(configuration, "LOGS_");
		ValidateProcessorSettings(configuration);
		ValidateLoggingSettings(configuration, profile);

		return new(false, diagnosticCodes);
	}

	private static bool IsSdkDisabled(string? value, out List<string> diagnosticCodes)
	{
		diagnosticCodes = [];

		if (string.IsNullOrEmpty(value) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
			return false;
		if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
			return true;

		diagnosticCodes.Add(ObservabilityConfigurationDiagnosticCodes.InvalidSdkDisableValue);
		return false;
	}

	private static void ValidateOtlpSettings(IConfiguration configuration, string signalPrefix)
	{
		var endpoint = GetSignalValue(configuration, signalPrefix, "ENDPOINT");
		var protocol = GetSignalValue(configuration, signalPrefix, "PROTOCOL");
		var headers = GetSignalValue(configuration, signalPrefix, "HEADERS");
		var timeout = GetSignalValue(configuration, signalPrefix, "TIMEOUT");
		var compression = GetSignalValue(configuration, signalPrefix, "COMPRESSION");

		ValidateEndpoint(Present(endpoint) ?? "http://localhost:4317", endpoint.Key);
		ValidateProtocol(Present(protocol) ?? "grpc", protocol.Key);
		ValidateHeaders(Present(headers), headers.Key);
		ValidateInteger(Present(timeout) ?? "5000", timeout.Key, 1, 5000);
		ValidateCompression(Present(compression) ?? "none", compression.Key);
	}

	private static string? Present(OtlpSettingValue setting) => string.IsNullOrEmpty(setting.Value) ? null : setting.Value;

	internal static OtlpSettingValue GetSignalValue(IConfiguration configuration, string signalPrefix, string suffix)
	{
		var signalKey = $"OTEL_EXPORTER_OTLP_{signalPrefix}{suffix}";
		var signalValue = configuration[signalKey];
		if (!string.IsNullOrEmpty(signalValue))
			return new OtlpSettingValue(signalValue, signalKey);

		var commonKey = $"OTEL_EXPORTER_OTLP_{suffix}";
		return new OtlpSettingValue(configuration[commonKey], commonKey);
	}

	private static void ValidateResourceSettings(IConfiguration configuration)
	{
		var serviceName = configuration["OTEL_SERVICE_NAME"];
		if (!string.IsNullOrEmpty(serviceName) && serviceName.Any(char.IsControl))
			throw Invalid("OTEL_SERVICE_NAME");

		var resourceAttributes = configuration["OTEL_RESOURCE_ATTRIBUTES"];
		if (!string.IsNullOrEmpty(resourceAttributes))
			ObservabilityResource.ValidateAttributes(resourceAttributes);
	}

	private static void ValidateProcessorSettings(IConfiguration configuration)
	{
		ValidateAbsent(configuration, "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY");
		ValidateAbsent(configuration, "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH");
		ValidateAbsent(configuration, "OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS");
		ValidateAbsent(configuration, "OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_CONTEXT_PROPAGATION");

		var sampler = configuration["OTEL_TRACES_SAMPLER"];
		if (string.IsNullOrEmpty(sampler))
			sampler = "parentbased_always_on";
		var supportedSamplers = new[] { "always_on", "always_off", "traceidratio", "parentbased_always_on", "parentbased_always_off", "parentbased_traceidratio" };
		if (!supportedSamplers.Contains(sampler, StringComparer.OrdinalIgnoreCase))
			throw Invalid("OTEL_TRACES_SAMPLER");

		var samplerArgument = configuration["OTEL_TRACES_SAMPLER_ARG"];
		if (!string.IsNullOrEmpty(samplerArgument) &&
			((!sampler.Equals("traceidratio", StringComparison.OrdinalIgnoreCase) && !sampler.Equals("parentbased_traceidratio", StringComparison.OrdinalIgnoreCase)) ||
			 !double.TryParse(samplerArgument, CultureInfo.InvariantCulture, out var ratio) || !double.IsFinite(ratio) || ratio is < 0 or > 1))
			throw Invalid("OTEL_TRACES_SAMPLER_ARG");

		ValidateInteger(configuration, "OTEL_BSP_SCHEDULE_DELAY", 1, 5000, 5000);
		ValidateInteger(configuration, "OTEL_BSP_EXPORT_TIMEOUT", 1, 5000, 5000);
		var traceQueue = ValidateInteger(configuration, "OTEL_BSP_MAX_QUEUE_SIZE", 1, 2048, 2048);
		var traceBatch = ValidateInteger(configuration, "OTEL_BSP_MAX_EXPORT_BATCH_SIZE", 1, 512, 512);
		if (traceBatch > traceQueue)
			throw Invalid("OTEL_BSP_MAX_EXPORT_BATCH_SIZE");

		ValidateInteger(configuration, "OTEL_METRIC_EXPORT_INTERVAL", 1, 60000, 60000);
		ValidateInteger(configuration, "OTEL_METRIC_EXPORT_TIMEOUT", 1, 5000, 5000);
		ValidateInteger(configuration, "OTEL_BLRP_SCHEDULE_DELAY", 1, 5000, 5000);
		ValidateInteger(configuration, "OTEL_BLRP_EXPORT_TIMEOUT", 1, 5000, 5000);
		var logQueue = ValidateInteger(configuration, "OTEL_BLRP_MAX_QUEUE_SIZE", 1, 2048, 2048);
		var logBatch = ValidateInteger(configuration, "OTEL_BLRP_MAX_EXPORT_BATCH_SIZE", 1, 512, 512);
		if (logBatch > logQueue)
			throw Invalid("OTEL_BLRP_MAX_EXPORT_BATCH_SIZE");
	}

	private static void ValidateLoggingSettings(IConfiguration configuration, ObservabilityHostProfile profile)
	{
		var categories = new List<string> { "Default", "Microsoft.Hosting.Lifetime", "System" };
		var generatedRootNamespace = GetGeneratedRootNamespace(profile);
		if (!string.IsNullOrEmpty(generatedRootNamespace))
			categories.Add(generatedRootNamespace);
		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Gateway)
			categories.Add("Microsoft.AspNetCore");
		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Worker)
			categories.Add("Microsoft.EntityFrameworkCore");
#if (massTransitIntegration)
		if (profile is ObservabilityHostProfile.Api or ObservabilityHostProfile.Worker)
			categories.Add("MassTransit");
#endif
		if (profile is ObservabilityHostProfile.Gateway)
			categories.Add("Yarp.ReverseProxy");

		foreach (var category in categories)
		{
			var value = configuration[$"Logging:LogLevel:{category}"];
			if (!string.IsNullOrEmpty(value) &&
				(int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
				 !Enum.TryParse<LogLevel>(value, true, out var level) || !Enum.IsDefined(level)))
				throw Invalid($"Logging:LogLevel:{category}");
		}
	}

	private static string? GetGeneratedRootNamespace(ObservabilityHostProfile profile)
	{
		var applicationName = Assembly.GetEntryAssembly()?.GetName().Name;
		return string.IsNullOrEmpty(applicationName) ? null : ObservabilityResource.GetSolutionName(profile, applicationName);
	}

	private static int ValidateInteger(IConfiguration configuration, string key, int minimum, int maximum, int defaultValue) =>
		string.IsNullOrEmpty(configuration[key])
			? defaultValue
			: ValidateInteger(configuration[key]!, key, minimum, maximum);

	private static int ValidateInteger(string value, string key, int minimum, int maximum) =>
		int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum
			? parsed
			: throw Invalid(key);

	private static void ValidateAbsent(IConfiguration configuration, string key)
	{
		if (!string.IsNullOrEmpty(configuration[key]))
			throw Invalid(key);
	}

	private static void ValidateEndpoint(string value, string key)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
			endpoint.Scheme is not ("http" or "https") ||
			endpoint.Host.Length == 0 ||
			!string.IsNullOrEmpty(endpoint.UserInfo) ||
			!string.IsNullOrEmpty(endpoint.Fragment))
			throw Invalid(key);
	}

	private static void ValidateProtocol(string value, string key)
	{
		if (!value.Equals("grpc", StringComparison.OrdinalIgnoreCase) && !value.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase))
			throw Invalid(key);
	}

	private static void ValidateCompression(string value, string key)
	{
		if (!value.Equals("none", StringComparison.OrdinalIgnoreCase) && !value.Equals("gzip", StringComparison.OrdinalIgnoreCase))
			throw Invalid(key);
	}

	private static void ValidateHeaders(string? value, string key)
	{
		if (string.IsNullOrEmpty(value))
			return;

		if (value.Split(',')
				 .Select(header => new
								   {
									   header, 
									   separator = header.IndexOf('=')
								   })
				 .Where(h => h.separator <= 0 ||
							 h.separator == h.header.Length - 1 ||
							 !IsValidHeaderName(h.header[..h.separator]) ||
							 !HasValidPercentEncoding(h.header[(h.separator + 1)..]))
				 .Select(t => t.header)
				 .Any())
			throw Invalid(key);
	}

	private static bool IsValidHeaderName(string name) => name.All(character => char.IsAsciiLetterOrDigit(character) || character is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~');

	private static bool HasValidPercentEncoding(string value)
	{
		for (var index = 0; index < value.Length; index++)
			switch (value[index])
			{
				case '%' when index + 2 >= value.Length ||
							  !Uri.IsHexDigit(value[index + 1]) ||
							  !Uri.IsHexDigit(value[index + 2]):
					return false;
				case '%':
				{
					var decoded = (ParseHexDigit(value[index + 1]) << 4) |
								  ParseHexDigit(value[index + 2]);
					if (decoded is < 0x20 or 0x7F)
						return false;

					index += 2;
					continue;
				}
				case <= '\u001F' or '\u007F':
					return false;
			}

		return true;
	}

	private static int ParseHexDigit(char value) =>
		value switch
		{
			>= '0' and <= '9' => value - '0',
			>= 'A' and <= 'F' => value - 'A' + 10,
			>= 'a' and <= 'f' => value - 'a' + 10,
			_ => -1
		};

	private static InvalidOperationException Invalid(string key) => new($"Invalid {key}.");
}

internal sealed record OtlpSettingValue(string? Value, string Key);