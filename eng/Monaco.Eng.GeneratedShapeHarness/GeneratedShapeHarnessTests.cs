using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace Monaco.Eng.GeneratedShapeHarness;

[CollectionDefinition(GeneratedShapeCollectionFixture.Name)]
public sealed class GeneratedShapeCollection : ICollectionFixture<GeneratedShapeCollectionFixture>;

public sealed class GeneratedShapeCollectionFixture : IAsyncLifetime
{
	public const string Name = "Generated shape harness";
	public const string Protocol = "protocol-invocation:generated-shape-harness";

	public GeneratedShapeHarness Harness { get; private set; } = null!;

	public async Task InitializeAsync() =>
		Harness = await GeneratedShapeHarness.ProvisionAsync(GeneratedShapeHarness.FindProductRoot(), Protocol, CancellationToken.None);

	public async Task DisposeAsync()
	{
		if (Harness is not null)
			await Harness.DisposeAsync();
	}
}

[Collection(GeneratedShapeCollectionFixture.Name)]
public sealed class GeneratedShapeHarnessTests
{
	private readonly GeneratedShapeHarness _harness;

	public GeneratedShapeHarnessTests(GeneratedShapeCollectionFixture fixture) => _harness = fixture.Harness;

	[Fact(DisplayName = "Isolated provision packs Monaco.Template once and generates eleven shapes")]
	public void IsolatedProvisionPacksOnceAndGeneratesElevenShapes()
	{
		_harness.PackCount.Should().Be(1);
		_harness.PackageId.Should().Be("Monaco.Template");
		_harness.PackageVersion.Should().Be(NuspecVersion(_harness.ProductRoot));
		_harness.ProtocolInvocation.Should().Be(GeneratedShapeCollectionFixture.Protocol);
		_harness.SourceSha.Should().MatchRegex("^[0-9a-f]{40}$");
		_harness.PackageSha256.Should().MatchRegex("^[0-9a-f]{64}$");
		Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_harness.PackagePath))).ToLowerInvariant().Should().Be(_harness.PackageSha256);
		Directory.GetFiles(_harness.TempRoot, $"Monaco.Template.{_harness.PackageVersion}.nupkg").Should().ContainSingle();
		Directory.Exists(_harness.HivePath).Should().BeTrue();
		Directory.EnumerateFileSystemEntries(_harness.HivePath, "*", SearchOption.AllDirectories).Should().NotBeEmpty();

		var userHive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".templateengine");
		GeneratedShapeHarness.IsInside(_harness.HivePath, userHive).Should().BeFalse();
		GeneratedShapeHarness.IsInside(_harness.TempRoot, _harness.ProductRoot).Should().BeFalse();
		_harness.UserTemplateEngineUnchanged.Should().BeTrue();

		_harness.ProvisionedShapes.Select(shape => shape.Id).Should().Equal("R1", "R2", "R3", "R4", "R5", "R6", "R7", "E1", "E2", "E3", "E4");
		foreach (var shape in _harness.ProvisionedShapes)
		{
			Directory.Exists(shape.OutputDirectory).Should().BeTrue(shape.Id);
			File.Exists(shape.SolutionPath).Should().BeTrue(shape.Id);
			GeneratedShapeHarness.IsInside(shape.OutputDirectory, _harness.TempRoot).Should().BeTrue();
			var evidencePath = Path.Combine(_harness.EvidenceDirectory, $"{shape.Id}.json");
			File.Exists(evidencePath).Should().BeTrue(shape.Id);
			AssertIdentity(evidencePath, shape);
		}

		_harness.Shapes.Single(shape => shape.Id == "R1").Arguments.Should().BeEmpty();
	}

	[Fact(DisplayName = "A second generate reuses the nupkg")]
	public void SecondGenerateReusesThePackedTemplate()
	{
		var packageWrite = File.GetLastWriteTimeUtc(_harness.PackagePath);
		var generated = _harness.Generate("PackOnceExtra");

		_harness.PackCount.Should().Be(1);
		File.GetLastWriteTimeUtc(_harness.PackagePath).Should().Be(packageWrite);
		Directory.GetFiles(_harness.TempRoot, $"Monaco.Template.{_harness.PackageVersion}.nupkg").Should().ContainSingle();
		Directory.Exists(generated.OutputDirectory).Should().BeTrue();
		_harness.UserTemplateEngineUnchanged.Should().BeTrue();
	}

	[Fact(DisplayName = "Receiver, load, and fault tools run outside generated trees")]
	public async Task ToolSlotsRunOutsideGeneratedTreesAndRefuseAnInsideDirectory()
	{
		var shape = _harness.Shapes.Single(candidate => candidate.Id == "R1");
		foreach (var slot in new[] { _harness.Receiver, _harness.Load, _harness.Fault })
		{
			var evidence = await slot.RunAsync(shape, CancellationToken.None);
			evidence.WorkingDirectory.Should().Be(_harness.ToolsDirectory);
			GeneratedShapeHarness.IsInside(evidence.WorkingDirectory!, shape.OutputDirectory).Should().BeFalse();
			evidence.Output!.Trim().Should().BeEquivalentTo(_harness.ToolsDirectory);
			AssertEnvelope(evidence, shape);
		}

		var act = async () => await _harness.Receiver.RunAsync(shape, shape.OutputDirectory, CancellationToken.None);
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*generated tree*");
	}

	[Fact(DisplayName = "An adapter returns evidence and does not decide a domain result")]
	public void AdapterReturnsEvidenceWithoutDecidingADomainResult()
	{
		var shape = _harness.Shapes.Single(candidate => candidate.Id == "R3");
		var evidence = _harness.Evidence.Read(shape);

		AssertEnvelope(evidence, shape);
		evidence.Kind.Should().Be("evidence");
	}

	[Fact(DisplayName = "R1 and R2 omit the shipped source-only package graph test")]
	public void RuntimeShapesOmitShippedSourceOnlyPackageGraphTest()
	{
		foreach (var id in new[] { "R1", "R2" })
		{
			var shape = _harness.Shapes.Single(candidate => candidate.Id == id);
			Directory.EnumerateFiles(shape.OutputDirectory, "ObservabilityPackageGraphTests.cs", SearchOption.AllDirectories)
					 .Should()
					 .BeEmpty(id);
		}
	}

	private static string NuspecVersion(string productRoot)
	{
		var nuspec = Path.Combine(productRoot, "src", "Monaco.Template.nuspec");
		return XDocument.Load(nuspec)
						.Descendants()
						.First(element => element.Name.LocalName == "version")
						.Value
						.Trim();
	}

	private void AssertIdentity(string evidencePath, GeneratedShape shape)
	{
		using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
		var root = document.RootElement;
		root.GetProperty("sourceSha").GetString().Should().Be(_harness.SourceSha);
		root.GetProperty("packageId").GetString().Should().Be(_harness.PackageId);
		root.GetProperty("packageVersion").GetString().Should().Be(_harness.PackageVersion);
		root.GetProperty("packageSha256").GetString().Should().Be(_harness.PackageSha256);
		root.GetProperty("shapeId").GetString().Should().Be(shape.Id);
		root.GetProperty("protocolInvocation").GetString().Should().Be(GeneratedShapeCollectionFixture.Protocol);
		var options = root.GetProperty("options");
		options.GetProperty("api").GetBoolean().Should().Be(shape.Options.Api);
		options.GetProperty("worker").GetBoolean().Should().Be(shape.Options.Worker);
		options.GetProperty("gateway").GetBoolean().Should().Be(shape.Options.Gateway);
		options.GetProperty("auth").GetBoolean().Should().Be(shape.Options.Auth);
		options.GetProperty("massTransit").GetBoolean().Should().Be(shape.Options.MassTransit);
		options.GetProperty("files").GetBoolean().Should().Be(shape.Options.Files);
		options.GetProperty("tests").GetBoolean().Should().Be(shape.Options.Tests);
		options.GetProperty("common").GetBoolean().Should().Be(shape.Options.Common);
		options.GetProperty("keycloak").GetBoolean().Should().Be(shape.Options.Keycloak);
	}

	private void AssertEnvelope(EvidenceEnvelope evidence, GeneratedShape shape)
	{
		evidence.SourceSha.Should().Be(_harness.SourceSha);
		evidence.PackageId.Should().Be(_harness.PackageId);
		evidence.PackageVersion.Should().Be(_harness.PackageVersion);
		evidence.PackageSha256.Should().Be(_harness.PackageSha256);
		evidence.ShapeId.Should().Be(shape.Id);
		evidence.Options.Should().Be(shape.Options);
		evidence.ProtocolInvocation.Should().Be(GeneratedShapeCollectionFixture.Protocol);
	}
}

public sealed class ShapeCatalogTests
{
	[Fact(DisplayName = "The catalog keeps the runtime and static-edge option vectors")]
	public void CatalogKeepsTheRuntimeAndStaticEdgeOptionVectors()
	{
		ShapeDefinition[] expected =
		[
			new("R1", new(true, true, false, true, true, true, true, true, true), []),
			new("R2", new(true, true, true, true, true, true, true, true, true), ["--apiGateway", "true"]),
			new("R3", new(true, false, false, false, false, false, true, true, false), ["--workerService", "false", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--keycloakConfig", "false"]),
			new("R4", new(true, false, false, false, true, true, true, true, false), ["--workerService", "false", "--auth", "false", "--keycloakConfig", "false"]),
			new("R5", new(false, true, false, false, true, true, false, true, false), ["--apiService", "false", "--auth", "false", "--tests", "false", "--keycloakConfig", "false"]),
			new("R6", new(false, true, false, false, false, false, false, true, false), ["--apiService", "false", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--tests", "false", "--keycloakConfig", "false"]),
			new("R7", new(true, true, false, true, true, true, false, true, true), ["--tests", "false"]),
			new("E1", new(true, true, false, true, true, true, false, false, false), ["--tests", "false", "--commonLibraries", "false", "--keycloakConfig", "false"]),
			new("E2", new(false, false, false, false, false, false, false, true, false), ["--apiService", "false", "--workerService", "false", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--tests", "false", "--keycloakConfig", "false"]),
			new("E3", new(false, false, true, false, false, false, false, true, false), ["--apiService", "false", "--workerService", "false", "--apiGateway", "true", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--tests", "false", "--keycloakConfig", "false"]),
			new("E4", new(false, true, false, false, true, true, true, true, false), ["--apiService", "false", "--auth", "false", "--keycloakConfig", "false"])
		];

		ShapeCatalog.All.Select(definition => definition.Id).Should().Equal(expected.Select(definition => definition.Id));
		for (var index = 0; index < expected.Length; index++)
		{
			ShapeCatalog.All[index].Options.Should().Be(expected[index].Options);
			ShapeCatalog.All[index].Arguments.Should().Equal(expected[index].Arguments);
		}

		ShapeCatalog.All.Should().OnlyContain(definition => definition.Options == ShapeCatalog.Apply(definition.Arguments));
	}

	[Fact(DisplayName = "The harness stays outside the template solution and the guide exclude is gone")]
	public void HarnessStaysOutsideTheTemplateSolutionAndTheGuideExcludeIsGone()
	{
		var product = GeneratedShapeHarness.FindProductRoot();
		var solution = File.ReadAllText(Path.Combine(product, "src", "Content", "Backend", "Solution", "Monaco.Template.Backend.slnx"));
		solution.Should().NotContain("Monaco.Eng.GeneratedShapeHarness");
		var template = File.ReadAllText(Path.Combine(product, "src", "Content", "Backend", "Solution", ".template.config", "template.json"));
		template.Should().NotContain("ObservabilityGuideTemplateGenerationTests.cs");
		template.Should().Contain("ObservabilityPackageGraphTests.cs");
		File.Exists(Path.Combine(product,
								 "src",
								 "Content",
								 "Backend",
								 "Solution",
								 "Monaco.Template.Backend.ArchitectureTests",
								 "ObservabilityGuideTemplateGenerationTests.cs"))
			.Should()
			.BeFalse();
	}
}

public sealed class GeneratedShapeCleanupTests
{
	[Fact(DisplayName = "Generate failure removes the temp root, processes, secrets, and feed config")]
	public async Task GenerateFailureRemovesTempRootProcessesSecretsAndFeedConfig()
	{
		string? temp = null;
		string? secret = null;
		string? feedConfig = null;
		string? shapeDirectory = null;
		int? processId = null;

		var exception = await Record.ExceptionAsync(() =>
			GeneratedShapeHarness.ProvisionAsync(GeneratedShapeHarness.FindProductRoot(),
												 "protocol-invocation:failure-cleanup",
												 (harness, shape, index) =>
												 {
													 if (index != 0)
														 return Task.CompletedTask;

													 temp = harness.TempRoot;
													 shapeDirectory = shape.OutputDirectory;
													 if (!Directory.Exists(shapeDirectory))
														 throw new InvalidOperationException("Expected the first shape directory to exist.");
													 if (GeneratedShapeHarness.IsInside(temp, harness.ProductRoot))
														 throw new InvalidOperationException("Temp root is inside the product root.");

													 secret = harness.Runtime.WriteConnectionString("Server=.;Database=Harness;User ID=sa;Password=secret;");
													 if (!GeneratedShapeHarness.IsInside(secret, temp) || GeneratedShapeHarness.IsInside(secret, shapeDirectory))
														 throw new InvalidOperationException("Secret file is not outside the generated tree.");

													 feedConfig = harness.WriteFeedConfig();
													 if (!GeneratedShapeHarness.IsInside(feedConfig, temp) || GeneratedShapeHarness.IsInside(feedConfig, shapeDirectory))
														 throw new InvalidOperationException("Feed config is not outside the generated tree.");

													 processId = harness.StartRegisteredSleep(shape);
													 if (!IsRunning(processId.Value))
														 throw new InvalidOperationException("Registered process exited before cleanup.");

													 throw new InvalidOperationException("Generate failed after one shape.");
												 },
												 CancellationToken.None));

		exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Generate failed after one shape.");
		Directory.Exists(temp).Should().BeFalse();
		Directory.Exists(shapeDirectory).Should().BeFalse();
		File.Exists(secret).Should().BeFalse();
		File.Exists(feedConfig).Should().BeFalse();
		IsRunning(processId!.Value).Should().BeFalse();
	}

	private static bool IsRunning(int processId)
	{
		try
		{
			var process = System.Diagnostics.Process.GetProcessById(processId);
			return !process.HasExited;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
