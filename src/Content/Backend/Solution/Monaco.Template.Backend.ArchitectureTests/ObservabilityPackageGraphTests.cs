using System.Text.Json;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class ObservabilityPackageGraphTests
{
	private static readonly string[] OpenTelemetryPackages =
	[
		"OpenTelemetry.Extensions.Hosting",
		"OpenTelemetry.Exporter.OpenTelemetryProtocol",
		"OpenTelemetry.Instrumentation.Runtime",
		"OpenTelemetry.Instrumentation.Http",
		"OpenTelemetry.Instrumentation.AspNetCore",
		"OpenTelemetry.Instrumentation.SqlClient"
	];

	[Fact(DisplayName = "OpenTelemetry package versions are exact and centrally managed")]
	public void OpenTelemetryPackageVersionsAreExactAndCentrallyManaged()
	{
		var centralPackages = ReadSolutionFile("Directory.Packages.props");

		foreach (var package in OpenTelemetryPackages)
			Assert.Contains($"<PackageVersion Include=\"{package}\" Version=\"1.17.0\" />", centralPackages, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Only Common.Observability owns direct OpenTelemetry package references")]
	public void OnlyCommonObservabilityOwnsDirectOpenTelemetryPackageReferences()
	{
		var solutionDirectory = FindSolutionDirectory();
		var packageOwners = Directory.EnumerateFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories)
									 .Where(path => !IsBuildArtifact(path))
									 .Where(path => File.ReadAllText(path).Contains("PackageReference Include=\"OpenTelemetry.", StringComparison.Ordinal))
									 .Select(path => Path.GetFileName(path)!)
									 .ToArray();

		Assert.Equal(["Monaco.Template.Backend.Common.Observability.csproj"], packageOwners);
	}

	[Fact(DisplayName = "Common.Observability has no project dependencies or concealed package assets")]
	public void CommonObservabilityHasNoProjectDependenciesOrConcealedPackageAssets()
	{
		var project = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "Monaco.Template.Backend.Common.Observability.csproj"));

		Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
		Assert.DoesNotContain("PrivateAssets", project, StringComparison.Ordinal);
		Assert.Equal(OpenTelemetryPackages.Length, CountOccurrences(project, "PackageReference Include=\"OpenTelemetry."));
	}

	[Fact(DisplayName = "Application and domain projects remain independent from observability")]
	public void ApplicationAndDomainProjectsRemainIndependentFromObservability()
	{
		foreach (var projectName in new[] { "Monaco.Template.Backend.Application", "Monaco.Template.Backend.Domain", "Monaco.Template.Backend.Common.Domain" })
		{
			var project = ReadSolutionFile(Path.Combine(projectName, $"{projectName}.csproj"));

			Assert.DoesNotContain("OpenTelemetry", project, StringComparison.Ordinal);
			Assert.DoesNotContain("Common.Observability", project, StringComparison.Ordinal);
		}
	}

	[Fact(DisplayName = "Every eligible host references Common.Observability once and selects one applicable profile")]
	public void EligibleHostsReferenceThePackageOnceAndSelectOneApplicableProfile()
	{
		AssertHostUsesSingleProfile("Monaco.Template.Backend.Api", "AddApiObservabilityProfile");
		AssertHostUsesSingleProfile("Monaco.Template.Backend.Worker", "AddWorkerObservabilityProfile");
		AssertHostUsesSingleProfile("Monaco.Template.Backend.Common.ApiGateway", "AddGatewayObservabilityProfile");
	}

	[Fact(DisplayName = "Profile definitions contain only their applicable instrumentation")]
	public void ProfileDefinitionsContainOnlyTheirApplicableInstrumentation()
	{
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityServiceCollectionExtensions.cs"));

		AssertProfileContainsOnly(profiles, "ApiInstrumentations", "WorkerInstrumentations", "Runtime", "AspNetCore", "Http", "SqlClient");
		AssertProfileContainsOnly(profiles, "WorkerInstrumentations", "GatewayInstrumentations", "Runtime", "Http", "SqlClient");
		AssertProfileContainsOnly(profiles, "GatewayInstrumentations", "internal enum ObservabilityInstrumentation", "Runtime", "AspNetCore", "Http");
	}

	[Fact(DisplayName = "Shared observability composition uses one unified OTLP exporter and applicable signal instrumentation")]
	public void SharedObservabilityCompositionUsesOneUnifiedOtlpExporterAndApplicableSignalInstrumentation()
	{
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityServiceCollectionExtensions.cs"));

		Assert.Equal(1, CountOccurrences(profiles, "UseOtlpExporter"));
		Assert.DoesNotContain("AddOtlpExporter", profiles, StringComparison.Ordinal);
		Assert.Contains("WithLogging", profiles, StringComparison.Ordinal);
		Assert.Contains("WithTracing", profiles, StringComparison.Ordinal);
		Assert.Contains("WithMetrics", profiles, StringComparison.Ordinal);
		Assert.Contains("IncludeFormattedMessage = true", profiles, StringComparison.Ordinal);
		Assert.Contains("IncludeScopes = true", profiles, StringComparison.Ordinal);
		Assert.Contains("ParseStateValues = false", profiles, StringComparison.Ordinal);
		Assert.Contains("ConfigureTracing", profiles, StringComparison.Ordinal);
		Assert.Contains("ConfigureMetrics", profiles, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Template excludes observability from E1 and no-host output")]
	public void TemplateExcludesObservabilityFromE1AndNoHostOutput()
	{
		var template = ReadSolutionFile(Path.Combine(".template.config", "template.json"));

		Assert.Contains("(!commonLibraries || (!apiService && !workerService && !apiGateway))", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.Common.Observability/**/*", template, StringComparison.Ordinal);
		Assert.Contains("(!commonLibraries || !apiService || !workerService || !apiGateway)", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.ArchitectureTests/ObservabilityPackageGraphTests.cs", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.ArchitectureTests/ObservabilityResourceTests.cs", template, StringComparison.Ordinal);
		Assert.DoesNotContain("Monaco.Template.Backend.*.Api*/**/*", template, StringComparison.Ordinal);
		Assert.Contains("(!apiService && !apiGateway)", template, StringComparison.Ordinal);

		foreach (var hostProject in new[] { "Monaco.Template.Backend.Api", "Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.ApiGateway" })
		{
			var program = ReadSolutionFile(Path.Combine(hostProject, "Program.cs"));

			Assert.Equal(2, CountOccurrences(program, "#if (commonLibraries)"));
		}
	}

	[Fact(DisplayName = "Generated local observability guide is host-gated, safe, and discoverable")]
	public void GeneratedLocalObservabilityGuideIsHostGatedSafeAndDiscoverable()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);
		var template = ReadSolutionFile(Path.Combine(".template.config", "template.json"));
		var solution = ReadSolutionFile("Monaco.Template.Backend.slnx");

		Assert.Contains("curl -sSL https://aspire.dev/install.sh | bash\n", guide, StringComparison.Ordinal);
		Assert.Contains("irm https://aspire.dev/install.ps1 | iex", guide, StringComparison.Ordinal);
		Assert.Contains("curl -sSL https://aspire.dev/install.sh | bash -s -- --version \"13.4.0\"", guide, StringComparison.Ordinal);
		Assert.Contains("iex \"& { $(irm https://aspire.dev/install.ps1) } -Version '13.4.0'\"", guide, StringComparison.Ordinal);
		Assert.Contains("aspire --version", guide, StringComparison.Ordinal);
		Assert.Contains("stable core `13.4.0` exactly", guide, StringComparison.Ordinal);
		Assert.Contains("prerelease", guide, StringComparison.Ordinal);
		Assert.Contains("+buildMetadata", guide, StringComparison.Ordinal);
		Assert.Contains("Preserve the full, unmodified version output as evidence", guide, StringComparison.Ordinal);
		Assert.Contains("Consumers are not hard-pinned", guide, StringComparison.Ordinal);
		Assert.Contains("aspire dashboard run --otlp-grpc-url http://localhost:4317", guide, StringComparison.Ordinal);
		Assert.Contains("tokenized URL", guide, StringComparison.Ordinal);
		Assert.Contains("loopback-only", guide, StringComparison.Ordinal);
		Assert.Contains("do not guess or use a bare Dashboard URL", guide, StringComparison.Ordinal);
		Assert.Contains("never retain, persist, log, upload, or copy it into evidence", guide, StringComparison.Ordinal);
		Assert.Contains("in-memory and lost on restart", guide, StringComparison.Ordinal);
		Assert.Contains("only telemetry-specific prerequisite", guide, StringComparison.Ordinal);
		Assert.Contains("SQL Server, RabbitMQ, Blob/Azurite, identity/Keycloak, migrations", guide, StringComparison.Ordinal);
		Assert.Contains("provision and verify them separately", guide, StringComparison.Ordinal);
		Assert.Contains("<!--#if (!commonLibraries) -->", guide, StringComparison.Ordinal);
		Assert.Contains("static external-observability handoff only", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("--allow-anonymous", guide, StringComparison.Ordinal);
		Assert.Equal(CountOccurrences(guide, "<!--#if"), CountOccurrences(guide, "<!--#endif -->"));

		using var templateDocument = JsonDocument.Parse(template);
		var root = templateDocument.RootElement;

		Assert.Contains(root.GetProperty("sources")[0].GetProperty("modifiers").EnumerateArray(), modifier =>
																									  modifier.TryGetProperty("condition", out var condition) &&
																									  condition.GetString() == "(!apiService && !workerService && !apiGateway)" &&
																									  modifier.GetProperty("exclude").EnumerateArray().Any(excluded => excluded.GetString() == "OBSERVABILITY.md"));

		var postAction = Assert.Single(root.GetProperty("postActions").EnumerateArray());
		Assert.Equal("AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C", postAction.GetProperty("actionId").GetString());
		Assert.Equal("(apiService || workerService || apiGateway)", postAction.GetProperty("condition").GetString());

		var primaryOutput = Assert.Single(root.GetProperty("primaryOutputs").EnumerateArray());
		Assert.Equal("Monaco.Template.Backend.slnx", primaryOutput.GetProperty("path").GetString());

		var solutionItems = solution.IndexOf("<Folder Name=\"/Solution Items/\">", StringComparison.Ordinal);
		var guideGate = solution.IndexOf("<!--#if (apiService || workerService || apiGateway) -->", solutionItems, StringComparison.Ordinal);
		var guideEntry = solution.IndexOf("<File Path=\"OBSERVABILITY.md\" />", solutionItems, StringComparison.Ordinal);
		var guideGateEnd = solution.IndexOf("<!--#endif -->", guideGate, StringComparison.Ordinal);
		Assert.True(solutionItems >= 0 && guideGate > solutionItems && guideEntry > guideGate && guideGateEnd > guideEntry,
					"The OBSERVABILITY.md Solution Item must sit inside its own Runtime Host conditional within /Solution Items/.");
	}

	[Fact(DisplayName = "Generated local observability guide documents only applicable Story 1.4 host profiles")]
	public void GeneratedLocalObservabilityGuideDocumentsOnlyApplicableStory14HostProfiles()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.Contains("<!--#if (commonLibraries) -->\n## Runtime host profiles and resource identity", guide, StringComparison.Ordinal);
		Assert.Contains("<!--#if (apiService) -->\n### API profile", guide, StringComparison.Ordinal);
		Assert.Contains("<!--#if (workerService) -->\n### Worker profile", guide, StringComparison.Ordinal);
		Assert.Contains("<!--#if (apiGateway) -->\n### Gateway profile", guide, StringComparison.Ordinal);
		Assert.Contains("generated fallbacks, then `OTEL_RESOURCE_ATTRIBUTES` collisions, then `OTEL_SERVICE_NAME`", guide, StringComparison.Ordinal);
		Assert.Contains("low-cardinality, non-secret operational metadata", guide, StringComparison.Ordinal);
		Assert.Contains("static external-observability handoff only", guide, StringComparison.Ordinal);
	}

	private static void AssertHostUsesSingleProfile(string hostProject, string profileMethod)
	{
		var project = ReadSolutionFile(Path.Combine(hostProject, $"{hostProject}.csproj"));
		var program = ReadSolutionFile(Path.Combine(hostProject, "Program.cs"));

		Assert.Equal(1, CountOccurrences(project, "Monaco.Template.Backend.Common.Observability.csproj"));
		Assert.Equal(1, CountOccurrences(program, profileMethod));
	}

	private static string ReadSolutionFile(string relativePath) => File.ReadAllText(Path.Combine(FindSolutionDirectory(), relativePath));

	private static int CountOccurrences(string value, string searchValue) => value.Split(searchValue, StringSplitOptions.None).Length - 1;

	private static void AssertProfileContainsOnly(string profiles, string profileName, string nextProfileName, params string[] expectedInstrumentations)
	{
		var profileStart = profiles.IndexOf(profileName, StringComparison.Ordinal);
		var profileEnd = profiles.IndexOf(nextProfileName, profileStart, StringComparison.Ordinal);
		var profile = profiles[profileStart..profileEnd];

		foreach (var instrumentation in expectedInstrumentations)
			Assert.Contains($"ObservabilityInstrumentation.{instrumentation}", profile, StringComparison.Ordinal);

		Assert.Equal(expectedInstrumentations.Length, CountOccurrences(profile, "ObservabilityInstrumentation."));
	}

	private static bool IsBuildArtifact(string path) => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
														path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

	private static string FindSolutionDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;
		}

		throw new DirectoryNotFoundException("Could not locate Monaco.Template.Backend.slnx from the test output directory.");
	}
}