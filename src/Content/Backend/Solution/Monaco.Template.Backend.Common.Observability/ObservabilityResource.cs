using OpenTelemetry.Resources;

namespace Monaco.Template.Backend.Common.Observability;

internal static class ObservabilityResource
{
	private static readonly string ProcessInstanceId = Guid.NewGuid().ToString("D");

	internal static ResourceBuilder Configure(ResourceBuilder resourceBuilder,
											  ObservabilityHostProfile profile,
											  string applicationName,
											  string environmentName)
	{
		ArgumentNullException.ThrowIfNull(resourceBuilder);

		if (string.IsNullOrWhiteSpace(applicationName))
			throw new InvalidOperationException("Unable to determine the generated host application name.");

		var attributes = new Dictionary<string, object>(StringComparer.Ordinal)
						 {
							 ["service.name"] = applicationName,
							 ["service.namespace"] = GetSolutionName(profile, applicationName),
							 ["service.instance.id"] = ProcessInstanceId,
							 ["deployment.environment.name"] = NormalizeEnvironmentName(environmentName)
						 };

		return resourceBuilder.AddAttributes(attributes)
							  .AddEnvironmentVariableDetector();
	}

	internal static string GetSolutionName(ObservabilityHostProfile profile, string applicationName)
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

	private static string NormalizeEnvironmentName(string environmentName) =>
		string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName.Trim();
}