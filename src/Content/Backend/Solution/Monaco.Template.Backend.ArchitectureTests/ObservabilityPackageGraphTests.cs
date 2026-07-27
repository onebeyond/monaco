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

	[Fact(DisplayName = "Template excludes observability from E1 and no-host output")]
	public void TemplateExcludesObservabilityFromE1AndNoHostOutput()
	{
		var template = ReadSolutionFile(Path.Combine(".template.config", "template.json"));

		Assert.Contains("(!commonLibraries || (!apiService && !workerService && !apiGateway))", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.Common.Observability/**/*", template, StringComparison.Ordinal);
		Assert.Contains("(!commonLibraries || !apiService || !workerService || !apiGateway)", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.ArchitectureTests/ObservabilityPackageGraphTests.cs", template, StringComparison.Ordinal);
		Assert.DoesNotContain("Monaco.Template.Backend.*.Api*/**/*", template, StringComparison.Ordinal);
		Assert.Contains("(!apiService && !apiGateway)", template, StringComparison.Ordinal);

		foreach (var hostProject in new[] { "Monaco.Template.Backend.Api", "Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.ApiGateway" })
		{
			var program = ReadSolutionFile(Path.Combine(hostProject, "Program.cs"));

			Assert.Equal(2, CountOccurrences(program, "#if (commonLibraries)"));
		}
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