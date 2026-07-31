using Monaco.Template.Backend.Common.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class ObservabilityResourceTests
{
	[Fact(DisplayName = "Generated host fallbacks identify every runtime host consistently")]
	public void GeneratedHostFallbacksIdentifyEveryRuntimeHostConsistently()
	{
		AssertFallback(ObservabilityHostProfile.Api, "Contoso.Api", "Contoso", "Contoso.Api");
		AssertFallback(ObservabilityHostProfile.Worker, "Contoso.Worker", "Contoso", "Contoso.Worker");
		AssertFallback(ObservabilityHostProfile.Gateway, "Contoso.Common.ApiGateway", "Contoso", "Contoso.Common.ApiGateway");
	}

	private static void AssertFallback(ObservabilityHostProfile profile, string applicationName, string expectedNamespace, string expectedServiceName)
	{
		var attributes = GetAttributes(ObservabilityResource.Create(profile, applicationName, "Development"));

		Assert.Equal(expectedServiceName, attributes["service.name"]);
		Assert.Equal(expectedNamespace, attributes["service.namespace"]);
		Assert.Equal("Development", attributes["deployment.environment.name"]);
		Assert.NotNull(attributes["service.instance.id"]);
		Assert.DoesNotContain("service.version", attributes.Keys);
	}

	[Fact(DisplayName = "Resource attributes override fallbacks and service name has final precedence")]
	public void ResourceAttributesOverrideFallbacksAndServiceNameHasFinalPrecedence() =>
		WithEnvironment("service.name=attribute-name,service.namespace=attribute-namespace,service.version=1.2.3,service.instance.id=instance-1,deployment.environment.name=Staging", "service-name", () =>
						{
							var attributes = GetAttributes(ObservabilityResource.Create(ObservabilityHostProfile.Api, "Contoso.Api", "Development"));

							Assert.Equal("service-name", attributes["service.name"]);
							Assert.Equal("attribute-namespace", attributes["service.namespace"]);
							Assert.Equal("1.2.3", attributes["service.version"]);
							Assert.Equal("instance-1", attributes["service.instance.id"]);
							Assert.Equal("Staging", attributes["deployment.environment.name"]);
						});

	[Fact(DisplayName = "Malformed resource input fails without exposing supplied values")]
	public void MalformedResourceInputFailsWithoutExposingSuppliedValues()
	{
		const string rawValue = "authorization=never-log-this";

		WithEnvironment(rawValue, null, () =>
										{
											var exception = Assert.Throws<InvalidOperationException>(() => ObservabilityResource.Create(ObservabilityHostProfile.Api, "Contoso.Api", "Development"));

											Assert.Contains("OTEL_RESOURCE_ATTRIBUTES", exception.Message, StringComparison.Ordinal);
											Assert.DoesNotContain(rawValue, exception.Message, StringComparison.Ordinal);
										});
	}

	[Fact(DisplayName = "Uppercase resource attribute keys are rejected")]
	public void UppercaseResourceAttributeKeysAreRejected() =>
		WithEnvironment("Service.Name=uppercase-name",
						null,
						() =>
						{
							var exception = Assert.Throws<InvalidOperationException>(() => ObservabilityResource.Create(ObservabilityHostProfile.Api, "Contoso.Api", "Development"));

							Assert.Contains("OTEL_RESOURCE_ATTRIBUTES", exception.Message, StringComparison.Ordinal);
						});

	[Fact(DisplayName = "Empty resource attribute and service name values resolve as missing")]
	public void EmptyResourceAttributeAndServiceNameValuesResolveAsMissing() =>
		WithEnvironment("", "", () =>
								{
									var attributes = GetAttributes(ObservabilityResource.Create(ObservabilityHostProfile.Api, "Contoso.Api", "Development"));

									Assert.Equal("Contoso.Api", attributes["service.name"]);
									Assert.Equal("Contoso", attributes["service.namespace"]);
									Assert.DoesNotContain("service.version", attributes.Keys);
								});

	[Fact(DisplayName = "Shared logging composition retains Console and adds one OpenTelemetry provider")]
	public void SharedLoggingCompositionRetainsConsoleAndAddsOneOpenTelemetryProvider()
	{
		lock (ObservabilityTestEnvironment.SharedLock)
		{
			var builder = Host.CreateApplicationBuilder();
			builder.AddApiObservability();

			using var host = builder.Build();
			var providers = host.Services.GetServices<ILoggerProvider>().ToArray();

			Assert.Contains(providers, provider => provider.GetType().Name == "ConsoleLoggerProvider");
			Assert.Single(providers, provider => provider.GetType().Name == "OpenTelemetryLoggerProvider");
			host.Services.GetRequiredService<ILoggerFactory>()
				.CreateLogger("Contoso.Application.LongRunningProcess.Command")
				.LogInformation(new EventId(2000, "LongRunningProcessCompleted"), "Long-running process command completed.");
		}
	}

	private static void WithEnvironment(string? resourceAttributes, string? serviceName, Action action)
	{
		lock (ObservabilityTestEnvironment.SharedLock)
		{
			var previousAttributes = Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES");
			var previousServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");

			try
			{
				Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", resourceAttributes);
				Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", serviceName);
				action();
			}
			finally
			{
				Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", previousAttributes);
				Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", previousServiceName);
			}
		}
	}

	private static Dictionary<string, object> GetAttributes(OpenTelemetry.Resources.Resource resource) =>
		resource.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
}