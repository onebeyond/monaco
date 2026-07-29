using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

namespace Monaco.Template.Backend.Common.Observability;

internal static partial class ObservabilityResource
{
	private const string ResourceAttributesEnvironmentVariable = "OTEL_RESOURCE_ATTRIBUTES";
	private const string ServiceNameEnvironmentVariable = "OTEL_SERVICE_NAME";
	private static readonly UTF8Encoding StrictUtf8 = new(false, true);
	private static readonly string ProcessInstanceId = Guid.NewGuid().ToString("D");

	internal static Resource Create(ObservabilityHostProfile profile) =>
		Create(profile, GetApplicationName(), GetEnvironmentName());

	internal static Resource Create(ObservabilityHostProfile profile, string applicationName, string environmentName)
	{
		if (string.IsNullOrWhiteSpace(applicationName))
			throw new InvalidOperationException("Unable to determine the generated host application name.");

		var attributes = new Dictionary<string, object>(StringComparer.Ordinal)
						 {
							 ["service.name"] = applicationName,
							 ["service.namespace"] = GetSolutionName(profile, applicationName),
							 ["service.instance.id"] = ProcessInstanceId,
							 ["deployment.environment.name"] = NormalizeEnvironmentName(environmentName)
						 };

		foreach (var attribute in ParseResourceAttributes(Environment.GetEnvironmentVariable(ResourceAttributesEnvironmentVariable)))
			attributes[attribute.Key] = attribute.Value;

		var serviceName = Environment.GetEnvironmentVariable(ServiceNameEnvironmentVariable);
		if (serviceName is not null)
			attributes["service.name"] = ValidateServiceName(serviceName);

		return ResourceBuilder.CreateEmpty().AddAttributes(attributes).Build();
	}

	private static string GetApplicationName() =>
		Assembly.GetEntryAssembly()?.GetName().Name ??
		AppDomain.CurrentDomain.FriendlyName ??
		throw new InvalidOperationException("Unable to determine the generated host application name.");

	private static string GetEnvironmentName() =>
		Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") switch
		{
			null or "" => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
			var dotnetEnvironment => dotnetEnvironment
		};

	private static string GetSolutionName(ObservabilityHostProfile profile, string applicationName)
	{
		var suffix = profile switch
					 {
						 ObservabilityHostProfile.Api => ".Api",
						 ObservabilityHostProfile.Worker => ".Worker",
						 ObservabilityHostProfile.Gateway => ".Common.ApiGateway",
						 _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
					 };

		var solutionName = applicationName.EndsWith(suffix, StringComparison.Ordinal) ? applicationName[..^suffix.Length] : applicationName;
		return string.IsNullOrEmpty(solutionName) ? applicationName : solutionName;
	}

	private static string NormalizeEnvironmentName(string environmentName) => string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName.Trim();

	private static string ValidateServiceName(string serviceName)
	{
		if (serviceName.Length == 0 || serviceName.Any(char.IsControl))
			throw new InvalidOperationException("Invalid OTEL_SERVICE_NAME.");

		return serviceName;
	}

	private static IReadOnlyList<KeyValuePair<string, object>> ParseResourceAttributes(string? rawValue)
	{
		if (rawValue is null)
			return [];

		int byteCount;
		try
		{
			byteCount = StrictUtf8.GetByteCount(rawValue);
		}
		catch (EncoderFallbackException)
		{
			throw InvalidResourceAttributes();
		}

		if (byteCount is < 1 or > 4096)
			throw InvalidResourceAttributes();

		var items = rawValue.Split(',', StringSplitOptions.None);
		if (items.Length > 32)
			throw InvalidResourceAttributes();

		var attributes = new List<KeyValuePair<string, object>>(items.Length);
		var keys = new HashSet<string>(StringComparer.Ordinal);

		foreach (var item in items)
		{
			var separator = item.IndexOf('=');
			if (separator <= 0 || separator != item.LastIndexOf('='))
				throw InvalidResourceAttributes();

			var key = PercentDecode(item[..separator]);
			var value = PercentDecode(item[(separator + 1)..]);
			if (!IsValidKey(key) || !IsValidValue(value) || !keys.Add(key) || IsSecretIndicatingKey(key))
				throw InvalidResourceAttributes();

			attributes.Add(new KeyValuePair<string, object>(key, value));
		}

		return attributes;
	}

	private static string PercentDecode(string value)
	{
		var bytes = new List<byte>(StrictUtf8.GetByteCount(value));

		for (var index = 0; index < value.Length; index++)
		{
			if (value[index] == '%')
			{
				if (index + 2 >= value.Length || !TryParseHexByte(value[index + 1], value[index + 2], out var escapedByte))
					throw InvalidResourceAttributes();

				bytes.Add(escapedByte);
				index += 2;
				continue;
			}

			var length = char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]) ? 2 : 1;
			try
			{
				bytes.AddRange(StrictUtf8.GetBytes(value.Substring(index, length)));
			}
			catch (EncoderFallbackException)
			{
				throw InvalidResourceAttributes();
			}

			index += length - 1;
		}

		try
		{
			return StrictUtf8.GetString([.. bytes]);
		}
		catch (DecoderFallbackException)
		{
			throw InvalidResourceAttributes();
		}
	}

	private static bool IsValidKey(string key) =>
		StrictUtf8.GetByteCount(key) is >= 1 and <= 64 && ResourceKeyPattern().IsMatch(key);

	private static bool IsValidValue(string value) =>
		StrictUtf8.GetByteCount(value) is >= 1 and <= 256 && value.All(character => character is > '\u001F' and not '\u007F');

	private static bool IsSecretIndicatingKey(string key)
	{
		var loweredKey = key.ToLowerInvariant();
		if (loweredKey is "apikey" or "accesskey" or "privatekey" or "clientsecret" or "connectionstring")
			return true;

		var segments = loweredKey.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
		if (segments.Any(segment => segment is "authorization" or "cookie" ||
									segment.Contains("password") ||
									segment.Contains("passwd") ||
									segment.Contains("secret") ||
									segment.Contains("token") ||
									segment.Contains("credential") ||
									segment.Contains("apikey")))
			return true;

		for (var index = 0; index < segments.Length - 1; index++)
			if ((segments[index], segments[index + 1]) is ("api", "key") or ("access", "key") or ("private", "key") or ("client", "secret") or ("connection", "string"))
				return true;

		return false;
	}

	private static bool TryParseHexByte(char high, char low, out byte value)
	{
		var highValue = ParseHexDigit(high);
		var lowValue = ParseHexDigit(low);
		if (highValue < 0 || lowValue < 0)
		{
			value = 0;
			return false;
		}

		value = (byte)((highValue << 4) | lowValue);
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

	private static InvalidOperationException InvalidResourceAttributes() => new("Invalid OTEL_RESOURCE_ATTRIBUTES.");

	[GeneratedRegex("^[a-z][a-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant)]
	private static partial Regex ResourceKeyPattern();
}