using AwesomeAssertions;

namespace Monaco.Template.Backend.Common.Observability.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Application Services", "Observability")]
public sealed class ObservabilityPackageGraphTests
{
	[Fact(DisplayName = "OpenTelemetry package versions are exact and centrally managed")]
	public void OpenTelemetryPackageVersionsAreExactAndCentrallyManaged()
	{
		var centralPackages = ParsedSolutionXml.Load("Directory.Packages.props");

		foreach (var package in ParsedSolutionXml.OpenTelemetryPackages)
			Assert.Equal("1.17.0", ParsedSolutionXml.PackageVersion(centralPackages, package));

		Assert.Equal(ParsedSolutionXml.OpenTelemetryPackages.Length,
					 ParsedSolutionXml.PackageVersions(centralPackages, "OpenTelemetry.")
									  .Count);
		ParsedSolutionXml.PackageVersions(centralPackages, "OpenTelemetry.")
						 .Should()
						 .NotContain(package => package.Contains("Jaeger", StringComparison.Ordinal) ||
												package.Contains("Prometheus", StringComparison.Ordinal) ||
												package.Contains("InMemory", StringComparison.Ordinal) ||
												package.Contains("MassTransit", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "Only Common.Observability owns direct OpenTelemetry package references")]
	public void OnlyCommonObservabilityOwnsDirectOpenTelemetryPackageReferences()
	{
		var packageOwners = ParsedSolutionXml.CsprojDocuments()
											 .Where(project => ParsedSolutionXml.PackageReferences(project.Document)
																				.Any(include => include.StartsWith("OpenTelemetry.", StringComparison.Ordinal)))
											 .Select(project => Path.GetFileName(project.Path)!)
											 .ToArray();

		Assert.Equal([
						 "Monaco.Template.Backend.Common.Observability.csproj"
					 ],
					 packageOwners);
	}

	[Fact(DisplayName = "Common.Observability has no project dependencies or concealed package assets")]
	public void CommonObservabilityHasNoProjectDependenciesOrConcealedPackageAssets()
	{
		var project = ParsedSolutionXml.Load(Path.Combine("Monaco.Template.Backend.Common.Observability", "Monaco.Template.Backend.Common.Observability.csproj"));

		ParsedSolutionXml.ProjectReferences(project)
						 .Should()
						 .BeEmpty();
		ParsedSolutionXml.HasPrivateAssets(project)
						 .Should()
						 .BeFalse();
		ParsedSolutionXml.Sdk(project)
						 .Should()
						 .Be("Microsoft.NET.Sdk");
		ParsedSolutionXml.PackageReferences(project)
						 .Should()
						 .NotContain(package => package.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
		ParsedSolutionXml.FrameworkReferences(project)
						 .Should()
						 .NotContain("Microsoft.AspNetCore.App");
		ParsedSolutionXml.PackageReferences(project)
						 .Where(package => package.StartsWith("OpenTelemetry.", StringComparison.Ordinal))
						 .Should()
						 .BeEquivalentTo(ParsedSolutionXml.OpenTelemetryPackages);
	}

	[Fact(DisplayName = "Application and domain projects remain independent from observability")]
	public void ApplicationAndDomainProjectsRemainIndependentFromObservability()
	{
		foreach (var projectName in new[] { "Monaco.Template.Backend.Application", "Monaco.Template.Backend.Domain", "Monaco.Template.Backend.Common.Domain" })
		{
			var project = ParsedSolutionXml.Load(Path.Combine(projectName, $"{projectName}.csproj"));
			var references = ParsedSolutionXml.PackageReferences(project)
											  .Concat(ParsedSolutionXml.ProjectReferences(project));

			references.Should()
					  .NotContain(reference => reference.Contains("OpenTelemetry", StringComparison.Ordinal) ||
											   reference.Contains("Common.Observability", StringComparison.Ordinal));
		}
	}

	[Fact(DisplayName = "Every eligible host references Common.Observability once")]
	public void EligibleHostsReferenceThePackageOnce()
	{
		foreach (var hostProject in new[] { "Monaco.Template.Backend.Api", "Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.ApiGateway" })
		{
			var project = ParsedSolutionXml.Load(Path.Combine(hostProject, $"{hostProject}.csproj"));

			ParsedSolutionXml.ProjectReferences(project)
							 .Count(include => include.Contains("Monaco.Template.Backend.Common.Observability.csproj", StringComparison.Ordinal))
							 .Should()
							 .Be(1);
			ParsedSolutionXml.PackageReferences(project)
							 .Count(include => include == "Monaco.Template.Backend.Common.Observability")
							 .Should()
							 .Be(1);
		}
	}

	[Fact(DisplayName = "Routing keeps no Collector or vendor artifacts")]
	public void RoutingKeepsNoCollectorVendorArtifacts()
	{
		ParsedSolutionXml.FileNames()
						 .Should()
						 .NotContain(fileName => fileName.Contains("collector", StringComparison.OrdinalIgnoreCase) ||
												 fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
												 fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
												 fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
												 fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact(DisplayName = "Retired telemetry policy units and host-copied boundary handlers remain absent")]
	public void RetiredTelemetryPolicyUnitsAndHostCopiedBoundaryHandlersRemainAbsent()
	{
		var observabilityDirectory = Path.Combine(ParsedSolutionXml.SolutionDirectory, "Monaco.Template.Backend.Common.Observability");
		foreach (var policyUnit in new[]
								   {
									   "BoundaryExceptionDiagnostics.cs",
									   "HmacSha256Identity.cs",
									   "HttpTelemetryPrivacyProcessor.cs",
									   "LogTelemetryPrivacyProcessor.cs",
									   "ObservabilityConfigurationDiagnostics.cs",
									   "ObservabilityExporterDiagnosticListener.cs",
									   "ObservabilityShutdownFlushService.cs",
									   "OtelConfigurationPreflightValidator.cs",
									   "SqlClientTelemetryPrivacyProcessor.cs"
								   })
			Assert.False(File.Exists(Path.Combine(observabilityDirectory, policyUnit)), $"{policyUnit} must remain absent.");

		File.Exists(Path.Combine(ParsedSolutionXml.SolutionDirectory, "Monaco.Template.Backend.Api", "BoundaryExceptionHandler.cs"))
			.Should()
			.BeFalse();
		File.Exists(Path.Combine(ParsedSolutionXml.SolutionDirectory, "Monaco.Template.Backend.Common.ApiGateway", "BoundaryExceptionHandler.cs"))
			.Should()
			.BeFalse();
	}

	[Fact(DisplayName = "Common delivery keeps source references and emits package references for every runtime edge")]
	public void CommonDeliveryKeepsSourceReferencesAndEmitsPackageReferencesForEveryRuntimeEdge()
	{
		var centralPackages = ParsedSolutionXml.Load("Directory.Packages.props");

		AssertDeliveryEdge("Monaco.Template.Backend.Domain", "Monaco.Template.Backend.Common.Domain");
		AssertDeliveryEdge("Monaco.Template.Backend.Application", "Monaco.Template.Backend.Common.Application");
		AssertDeliveryEdge("Monaco.Template.Backend.Application", "Monaco.Template.Backend.Common.Infrastructure");
		AssertDeliveryEdge("Monaco.Template.Backend.Application", "Monaco.Template.Backend.Common.BlobStorage");
		AssertDeliveryEdge("Monaco.Template.Backend.Api", "Monaco.Template.Backend.Common.Api.Application");
		AssertDeliveryEdge("Monaco.Template.Backend.Api", "Monaco.Template.Backend.Common.Api");
		AssertDeliveryEdge("Monaco.Template.Backend.Api", "Monaco.Template.Backend.Common.Observability");
		AssertDeliveryEdge("Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.Observability");
		AssertDeliveryEdge("Monaco.Template.Backend.Common.ApiGateway", "Monaco.Template.Backend.Common.Api");
		AssertDeliveryEdge("Monaco.Template.Backend.Common.ApiGateway", "Monaco.Template.Backend.Common.Observability");
		AssertDeliveryEdge("Monaco.Template.Backend.Application.Tests", "Monaco.Template.Backend.Common.Tests");
		AssertDeliveryEdge("Monaco.Template.Backend.Domain.Tests", "Monaco.Template.Backend.Common.Tests");

		foreach (var package in new[]
								{
									"Monaco.Template.Backend.Common.Domain",
									"Monaco.Template.Backend.Common.Application",
									"Monaco.Template.Backend.Common.Infrastructure",
									"Monaco.Template.Backend.Common.BlobStorage",
									"Monaco.Template.Backend.Common.Api.Application",
									"Monaco.Template.Backend.Common.Api",
									"Monaco.Template.Backend.Common.Observability",
									"Monaco.Template.Backend.Common.Tests"
								})
			Assert.Equal("0.0.1-alpha1", ParsedSolutionXml.PackageVersion(centralPackages, package));
	}

	private static void AssertDeliveryEdge(string projectName, string commonProjectName)
	{
		var project = ParsedSolutionXml.Load(Path.Combine(projectName, $"{projectName}.csproj"));

		ParsedSolutionXml.HasProjectReference(project, commonProjectName)
						 .Should()
						 .BeTrue();
		ParsedSolutionXml.HasPackageReference(project, commonProjectName)
						 .Should()
						 .BeTrue();
	}
}