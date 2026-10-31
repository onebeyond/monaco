using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace Monaco.Eng.GeneratedShapeHarness;

[Collection(GeneratedShapeCollectionFixture.Name)]
public sealed class PackageFeedProofTests
{
	private const string SolutionName = "FeedProof";
	private const string PackageVersion = "0.0.1-alpha1";

	private static readonly string[] CommonLibrarySuffixes =
	[
		"Common.Api",
		"Common.Api.Application",
		"Common.Application",
		"Common.BlobStorage",
		"Common.BlobStorage.Tests",
		"Common.Domain",
		"Common.Domain.Tests",
		"Common.Infrastructure",
		"Common.Infrastructure.Tests",
		"Common.Observability",
		"Common.Observability.Tests",
		"Common.Tests"
	];

	private static readonly string[] CommonPackageSuffixes =
	[
		"Common.Domain",
		"Common.Infrastructure",
		"Common.Application",
		"Common.BlobStorage",
		"Common.Api",
		"Common.Api.Application",
		"Common.Observability",
		"Common.Tests"
	];

	private readonly GeneratedShapeHarness _harness;

	public PackageFeedProofTests(GeneratedShapeCollectionFixture fixture) => _harness = fixture.Harness;

	[Fact(DisplayName = "Package-mode feed restores and builds the API, Worker, and Gateway shapes")]
	public async Task PackageModeFeedRestoresAndBuildsThreeShapes()
	{
		var source = await _harness.GenerateIntoAsync("FeedProof-source", SolutionName, [], CancellationToken.None);
		AssertSourceGraph(source);

		await _harness.PackCommonProjectsAsync(source, CommonPackageSuffixes, CancellationToken.None);
		foreach (var suffix in CommonPackageSuffixes)
			File.Exists(Path.Combine(_harness.FeedDirectory, $"{SolutionName}.{suffix}.{PackageVersion}.nupkg")).Should().BeTrue(suffix);

		var config = _harness.WriteFeedConfig();
		GeneratedShapeHarness.IsInside(config, _harness.TempRoot).Should().BeTrue();
		GeneratedShapeHarness.IsInside(config, source.OutputDirectory).Should().BeFalse();
		var configText = File.ReadAllText(config);
		configText.Should().Contain("https://api.nuget.org/v3/index.json");
		configText.Should().Contain(Path.GetFullPath(_harness.FeedDirectory));

		var api = await GeneratePackageShapeAsync("FeedProof-api", "--commonLibraries", "false", "--workerService", "false", "--apiGateway", "false");
		var worker = await GeneratePackageShapeAsync("FeedProof-worker", "--commonLibraries", "false", "--apiService", "false", "--apiGateway", "false");
		var gateway = await GeneratePackageShapeAsync("FeedProof-gateway", "--commonLibraries", "false", "--apiGateway", "true");

		AssertPackageGraph(api, gatewayRemains: false);
		AssertPackageGraph(worker, gatewayRemains: false);
		AssertPackageGraph(gateway, gatewayRemains: true);
		foreach (var shape in new[] { api, worker, gateway })
			GeneratedShapeHarness.IsInside(config, shape.OutputDirectory).Should().BeFalse();

		foreach (var shape in new[] { api, worker, gateway })
		{
			var built = await _harness.Build.RunAsync(shape, config, CancellationToken.None);
			built.ExitCode.Should().Be(0, "build {0} output: {1}", shape.Id, Tail(built.Output));
			AssertEnvelope(built, shape);
			AssertCommonPackagesRestoredFromGeneratedShapeFeed(shape);
		}
	}

	private void AssertCommonPackagesRestoredFromGeneratedShapeFeed(GeneratedShape shape)
	{
		var feed = NormalizePath(_harness.FeedDirectory);
		var packages = NormalizePath(_harness.PackagesDirectory);
		var sawCommonPackage = false;
		foreach (var assetsPath in Directory.EnumerateFiles(shape.OutputDirectory, "project.assets.json", SearchOption.AllDirectories))
		{
			using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
			var commonPackages = document.RootElement.GetProperty("libraries").EnumerateObject()
										 .Select(library => library.Name)
										 .Where(name => name.StartsWith($"{SolutionName}.Common.", StringComparison.OrdinalIgnoreCase))
										 .ToArray();
			if (commonPackages.Length == 0)
				continue;

			sawCommonPackage = true;
			var restore = document.RootElement.GetProperty("project").GetProperty("restore");
			var sources = restore.GetProperty("sources").EnumerateObject().Select(source => NormalizePath(source.Name));
			sources.Should().Contain(source => string.Equals(source, feed, StringComparison.OrdinalIgnoreCase),
									  $"{shape.Id} should restore {string.Join(", ", commonPackages)} from generated-shape-feed");
			var folders = document.RootElement.GetProperty("packageFolders").EnumerateObject().Select(folder => NormalizePath(folder.Name)).ToArray();
			folders.Should().OnlyContain(folder => string.Equals(folder, packages, StringComparison.OrdinalIgnoreCase), shape.Id);
			NormalizePath(restore.GetProperty("packagesPath").GetString()!).Should().BeEquivalentTo(packages);
		}

		sawCommonPackage.Should().BeTrue($"{shape.Id} restored no {SolutionName}.Common.* package");
	}

	private static string NormalizePath(string path) =>
		Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

	private async Task<GeneratedShape> GeneratePackageShapeAsync(string shapeId, params string[] arguments) =>
		await _harness.GenerateIntoAsync(shapeId, SolutionName, arguments, CancellationToken.None);

	private static void AssertSourceGraph(GeneratedShape source)
	{
		foreach (var suffix in CommonLibrarySuffixes)
			Directory.Exists(ProjectDirectory(source, suffix)).Should().BeTrue(suffix);

		Directory.Exists(ProjectDirectory(source, "Common.ApiGateway")).Should().BeFalse();
		var references = ProjectReferences(source);
		references.Should().Contain(include => include.Contains($"{SolutionName}.Common.Domain.csproj", StringComparison.Ordinal));
		references.Should().Contain(include => include.Contains($"{SolutionName}.Common.Observability.csproj", StringComparison.Ordinal));
	}

	private static void AssertPackageGraph(GeneratedShape shape, bool gatewayRemains)
	{
		foreach (var suffix in CommonLibrarySuffixes)
			Directory.Exists(ProjectDirectory(shape, suffix)).Should().BeFalse($"{shape.Id} {suffix}");

		Directory.Exists(ProjectDirectory(shape, "Common.ApiGateway")).Should().Be(gatewayRemains, shape.Id);
		ProjectReferences(shape).Should().NotContain(include => ReferencesExcludedLibrary(include));
		PackageReferences(shape).Should().NotBeEmpty(shape.Id);
		PackageReferences(shape).Should().OnlyContain(reference => reference.Version == null, shape.Id);

		var versionPins = Files(shape)
			.Where(path => File.ReadAllText(path).Contains($"Version=\"{PackageVersion}\"", StringComparison.Ordinal))
			.Select(path => Path.GetRelativePath(shape.OutputDirectory, path))
			.ToArray();
		versionPins.Should().Equal("Directory.Packages.props");

		var ownPackageVersions = Files(shape)
			.Where(path => File.ReadAllText(path).Contains($"<PackageVersion>{PackageVersion}</PackageVersion>", StringComparison.Ordinal))
			.Select(path => Path.GetRelativePath(shape.OutputDirectory, path))
			.ToArray();
		ownPackageVersions.Should().OnlyContain(path => path.EndsWith(".Messages.csproj", StringComparison.Ordinal));
	}

	private void AssertEnvelope(EvidenceEnvelope evidence, GeneratedShape shape)
	{
		evidence.SourceSha.Should().Be(_harness.SourceSha);
		evidence.PackageId.Should().Be("Monaco.Template");
		evidence.PackageVersion.Should().Be(_harness.PackageVersion);
		evidence.PackageSha256.Should().Be(_harness.PackageSha256);
		evidence.ShapeId.Should().Be(shape.Id);
		evidence.ProtocolInvocation.Should().Be(GeneratedShapeCollectionFixture.Protocol);
		evidence.Options.Should().Be(shape.Options);
	}

	private static bool ReferencesExcludedLibrary(string include) =>
		CommonLibrarySuffixes.Any(suffix => include.Contains($"{SolutionName}.{suffix}.csproj", StringComparison.Ordinal) ||
											 include.Contains($"{SolutionName}.{suffix}\\", StringComparison.Ordinal) ||
											 include.Contains($"{SolutionName}.{suffix}/", StringComparison.Ordinal));

	private static IEnumerable<string> ProjectReferences(GeneratedShape shape) =>
		Projects(shape).SelectMany(project => Descendants(project, "ProjectReference")
													  .Select(element => element.Attribute("Include")?.Value ?? ""));

	private static IEnumerable<(string Include, string? Version)> PackageReferences(GeneratedShape shape) =>
		Projects(shape).SelectMany(project => Descendants(project, "PackageReference")
													  .Where(element => (element.Attribute("Include")?.Value ?? "").Contains(".Common.", StringComparison.Ordinal))
													  .Select(element => (element.Attribute("Include")!.Value,
																		  element.Attribute("Version")?.Value ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value)));

	private static IEnumerable<string> Projects(GeneratedShape shape) =>
		Directory.EnumerateFiles(shape.OutputDirectory, "*.csproj", SearchOption.AllDirectories)
				 .Where(path => !IsBuildOutput(path));

	private static IEnumerable<string> Files(GeneratedShape shape) =>
		Directory.EnumerateFiles(shape.OutputDirectory, "*", SearchOption.AllDirectories)
				 .Where(path => !IsBuildOutput(path));

	private static bool IsBuildOutput(string path) =>
		path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
		path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

	private static string ProjectDirectory(GeneratedShape shape, string suffix) =>
		Path.Combine(shape.OutputDirectory, $"{shape.Name}.{suffix}");

	private static IEnumerable<XElement> Descendants(string projectPath, string localName) =>
		XDocument.Load(projectPath).Descendants().Where(element => element.Name.LocalName == localName);

	private static string Tail(string? output)
	{
		if (string.IsNullOrEmpty(output))
			return "";

		var tail = output.Length > 4000 ? output[^4000..] : output;
		return tail.Replace("{", "(").Replace("}", ")");
	}
}
