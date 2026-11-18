using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace Monaco.Eng.Tier1;

[ExcludeFromCodeCoverage]
public sealed class Tier1ResultTests
{
	private const string ValidConfiguration = """
		{
			"OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317",
			"OTEL_EXPORTER_OTLP_PROTOCOL": "grpc",
			"CorsPolicies": [
				{
					"Headers": [ "Authorization", "Content-Type" ]
				}
			]
		}
		""";

	[Fact(DisplayName = "Full pass exits zero")]
	public async Task FullPass_ExitsZero()
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var result = await RunAsync(root, new Case());
			result.ExitCode.Should().Be(0);
			result.Report.Should().Be("Tier 1 passed.");
			result.Failures.Should().BeEmpty();
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact(DisplayName = "Mixed failures list both ids, shape, and mode, and later protocols still run")]
	public async Task MixedFailures_ListsBothIdsShapeAndMode_AndContinues()
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var scenario = new Case { FailedSourceClass = "ObservabilityPackageGraphTests", FailedCell = "R3-DU" };
			var result = await RunAsync(root, scenario);

			result.ExitCode.Should().NotBe(0);
			result.Failures.Should().Contain(failure => failure.Protocol == "T1-02");
			result.Failures.Should().Contain(failure => failure.Protocol == "T1-14" && failure.Shape == "R3" && failure.Mode == "DU");
			result.Report.Should().Contain("T1-02");
			result.Report.Should().Contain("T1-14");
			result.Report.Should().Contain("shape=R3");
			result.Report.Should().Contain("mode=DU");
			result.Failures.Should().NotContain(failure => failure.Protocol == "T1-01");

			var kinds = scenario.Calls.Select(Kind).ToArray();
			var source = Array.IndexOf(kinds, "solution");
			var runtime = Array.IndexOf(kinds, "runtime");
			var migrations = Array.IndexOf(kinds, "migrations");
			var architecture = Array.IndexOf(kinds, "architecture");
			var guide = Array.IndexOf(kinds, "guide");
			var generated = Array.FindLastIndex(kinds, kind => kind == "generated");
			source.Should().BeGreaterThanOrEqualTo(0);
			runtime.Should().BeGreaterThan(source);
			kinds.Count(kind => kind == "runtime").Should().Be(1);
			migrations.Should().BeGreaterThan(runtime);
			architecture.Should().BeGreaterThan(migrations);
			guide.Should().BeGreaterThan(architecture);
			generated.Should().BeGreaterThan(guide);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact(DisplayName = "Docker down fails each Docker protocol by name and a skip is not a pass")]
	public async Task DockerDown_FailsEachDockerProtocolByName_AndSkipIsNotAPass()
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var scenario = new Case { Docker = false, SkipSourceTest = true, ZeroExitDespiteFailures = true };
			var result = await RunAsync(root, scenario);

			result.ExitCode.Should().NotBe(0);
			result.Report.Should().NotContain("Tier 1 passed.");
			foreach (var protocol in Tier1Run.DockerProtocols)
			{
				result.Failures.Should().Contain(failure => failure.Protocol == protocol);
				result.Report.Should().Contain(protocol);
			}

			result.Failures.Should().Contain(failure => failure.Protocol == "T1-01");
			foreach (var shape in new[] { "R1", "R2", "R3", "R4" })
				result.Failures.Should().Contain(failure => failure.Protocol == "generated-tests" && failure.Shape == shape);

			result.Report.Should().Contain("a skip is not a pass");
			scenario.ProvisionCalls.Should().Be(1);
			var kinds = scenario.Calls.Select(Kind).ToArray();
			kinds.Should().Contain("solution");
			kinds.Should().Contain("architecture");
			kinds.Should().Contain("guide");
			Array.IndexOf(kinds, "architecture").Should().BeGreaterThan(Array.IndexOf(kinds, "solution"));
			Array.IndexOf(kinds, "guide").Should().BeGreaterThan(Array.IndexOf(kinds, "architecture"));
			kinds.Should().NotContain("runtime");
			kinds.Should().NotContain("migrations");
			kinds.Should().NotContain("generated");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact(DisplayName = "Static edges come from one provision and do not start hosts")]
	public async Task StaticEdges_ComeFromOneProvision_AndDoNotStartHosts()
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var scenario = new Case { CaptureScannedShapes = true };
			var result = await RunAsync(root, scenario);

			result.ExitCode.Should().Be(0, result.Report);
			scenario.ProvisionCalls.Should().Be(1);
			scenario.ProvisionInvocation.Should().Be("tier1");
			scenario.ScannedShapeIds.Should().Contain(["E1", "E2", "E3", "E4"]);

			var flat = scenario.Calls.SelectMany(call => call).ToArray();
			flat.Should().NotContain("build");
			flat.Should().NotContain("start");
			foreach (var call in scenario.Calls.Where(call => call.Contains("run")))
				call.Should().Contain("eng/migrations.cs");

			var tested = scenario.Calls.Where(call => call.Length > 1 && call[0] == "test").Select(call => call[1]).ToArray();
			tested.Should().Contain(path => path.EndsWith("R1.slnx", StringComparison.Ordinal));
			tested.Should().Contain(path => path.EndsWith("R2.slnx", StringComparison.Ordinal));
			tested.Should().Contain(path => path.EndsWith("R3.slnx", StringComparison.Ordinal));
			tested.Should().Contain(path => path.EndsWith("R4.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.Contains($"{Path.DirectorySeparatorChar}E1{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || path.EndsWith("E1.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.EndsWith("E2.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.EndsWith("E3.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.EndsWith("E4.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.EndsWith("R5.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.EndsWith("R6.slnx", StringComparison.Ordinal));
			tested.Should().NotContain(path => path.EndsWith("R7.slnx", StringComparison.Ordinal));

			var runtime = scenario.Calls.Single(call => Kind(call) == "runtime");
			runtime.Should().Contain(argument => argument.Contains("HighFidelityEmission", StringComparison.Ordinal) &&
												 argument.Contains("RuntimeOracleTests", StringComparison.Ordinal) &&
												 argument.Contains("FaultIsolation", StringComparison.Ordinal) &&
												 argument.Contains("SamplingPropagation", StringComparison.Ordinal));

			var kinds = scenario.Calls.Select(Kind).ToArray();
			Array.IndexOf(kinds, "architecture").Should().BeGreaterThan(Array.IndexOf(kinds, "migrations"));
			Array.IndexOf(kinds, "guide").Should().BeGreaterThan(Array.IndexOf(kinds, "architecture"));
			var solution = scenario.Calls.Single(call => Kind(call) == "solution");
			solution.Should().Contain(argument => argument.Contains("FullyQualifiedName!~MigrationMaintenanceCommandTests", StringComparison.Ordinal) &&
												  argument.Contains("FullyQualifiedName!~SupportedRuntimeShapeCompositionTests", StringComparison.Ordinal) &&
												  argument.Contains("FullyQualifiedName!~StaticEdgeTruthfulnessTests", StringComparison.Ordinal));
			var architecture = scenario.Calls.Single(call => Kind(call) == "architecture");
			architecture.Should().Contain(argument => argument.Contains("Monaco.Template.Backend.ArchitectureTests.csproj", StringComparison.Ordinal));
			architecture.Should().Contain(argument => argument.Contains("FullyQualifiedName~SupportedRuntimeShapeCompositionTests", StringComparison.Ordinal) &&
													  argument.Contains("FullyQualifiedName~StaticEdgeTruthfulnessTests", StringComparison.Ordinal));
			var guide = scenario.Calls.Single(call => Kind(call) == "guide");
			guide.Should().Contain(argument => argument.Contains("FullyQualifiedName~ObservabilityGuideTemplateGenerationTests", StringComparison.Ordinal));
			scenario.Environments.Should().Contain(("architecture", Monaco.Eng.GeneratedShapeHarness.GeneratedShapeHarness.ProvisionRootVariable, root));
			scenario.Environments.Should().Contain(("guide", Monaco.Eng.GeneratedShapeHarness.GeneratedShapeHarness.ProvisionRootVariable, root));
			scenario.Environments.Select(entry => entry.Kind).Should().OnlyContain(kind => kind == "architecture" || kind == "guide");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact(DisplayName = "Committed header fails the named secret check and localhost grpc stays valid")]
	public async Task CommittedHeader_FailsNamedCheck_AndLocalhostGrpcStaysValid()
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			await AssertSecretAsync(root, """{ "OTEL_EXPORTER_OTLP_HEADERS": "api-key=abc" }""", "OTEL_EXPORTER_OTLP_HEADERS");
			await AssertSecretAsync(root, """{ "Authorization": "Bearer secret" }""", "authorization");
			await AssertSecretAsync(root, """{ "authorization": "Bearer secret" }""", "authorization");
			await AssertSecretAsync(root, """{ "ApplicationInsights": { "InstrumentationKey": "abc" } }""", "ApplicationInsights");
			await AssertSecretAsync(root, """{ "AuditLog": "removed" }""", "audit");

			var valid = await RunAsync(root, new Case());
			valid.ExitCode.Should().Be(0, valid.Report);
			valid.Failures.Should().NotContain(failure => failure.Protocol == "secrets");
			valid.Report.Should().Be("Tier 1 passed.");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact(DisplayName = "A solution run with no usable trx still names T1-13")]
	public async Task UnusableSolution_NamesT1_13()
	{
		var result = await RunTemp(new Case { UnusableSolution = true });
		result.ExitCode.Should().NotBe(0);
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-13");
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-01");
	}

	[Fact(DisplayName = "T1-13 requires both composition and static-edge classes")]
	public async Task StaticEdgeClassMissing_FailsT1_13()
	{
		var result = await RunTemp(new Case { OmitStaticEdge = true });
		result.ExitCode.Should().NotBe(0);
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-13" && failure.Detail == "StaticEdgeTruthfulnessTests not run");
		result.Failures.Should().NotContain(failure => failure.Detail == "SupportedRuntimeShapeCompositionTests not run");
	}

	[Fact(DisplayName = "A missing product-create row is T1-05 not run")]
	public async Task MissingProductCreate_IsT1_05NotRun()
	{
		var result = await RunTemp(new Case { OmitProductCreate = true });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-05");
		result.Failures[0].Detail.Should().Be("not run");
	}

	[Fact(DisplayName = "A failed product-create row is T1-05 rather than T1-01")]
	public async Task FailedProductCreate_IsT1_05()
	{
		var result = await RunTemp(new Case { FailProductCreate = true });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-05");
		result.Failures[0].Detail.Should().Contain("Create new Product succeeds");
	}

	[Fact(DisplayName = "Stamp matches a whole word and COMP-03 stays T1-11")]
	public async Task StampWord_DoesNotClaimTimestamp_AndComp03StaysT1_11()
	{
		var result = await RunTemp(new Case { FailTimestamp = true, FailComp03 = true });
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-01" && failure.Detail.Contains("timestamp", StringComparison.Ordinal));
		result.Failures.Should().NotContain(failure => failure.Protocol == "T1-11" && failure.Detail.Contains("timestamp", StringComparison.Ordinal));
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-11" && failure.Detail.Contains("COMP-03", StringComparison.Ordinal));
		result.Failures.Should().NotContain(failure => failure.Protocol == "T1-13" && failure.Detail.Contains("COMP-03", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "A failed guide-generation row names T1-04 and T1-18")]
	public async Task FailedGuideGeneration_NamesBothProtocols_AndLaterProtocolsRun()
	{
		var scenario = new Case { FailGuide = true };
		var result = await RunTemp(scenario);
		result.Failures.Select(failure => failure.Protocol).Should().BeEquivalentTo(["T1-04", "T1-18"]);
		scenario.Calls.Select(Kind).Should().Contain("runtime");
	}

	[Fact(DisplayName = "A non-zero migrations verify is T1-12 and later protocols still run")]
	public async Task MigrationsVerify_NonZero_IsT1_12()
	{
		var scenario = new Case { MigrationsExit = 1 };
		var result = await RunTemp(scenario);
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-12");
		scenario.Calls.Select(Kind).Should().Contain("generated");
	}

	[Fact(DisplayName = "A failed messaging verdict maps once to T1-06")]
	public async Task HighFidelityMessagingFailure_MapsOnceToT1_06()
	{
		var result = await RunTemp(new Case { FailedHighFidelityShape = "FullApiWorker", FailedHighFidelityVerdict = "messaging" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-06");
		result.Failures[0].Shape.Should().Be("R1");
		result.Failures[0].Mode.Should().BeNull();
	}

	[Fact(DisplayName = "One failed high-fidelity verdict maps once and has no mode")]
	public async Task HighFidelityHttpFailure_MapsOnce()
	{
		var result = await RunTemp(new Case { FailedHighFidelityShape = "FullApiWorker", FailedHighFidelityVerdict = "HTTP" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-06");
		result.Failures[0].Shape.Should().Be("R1");
		result.Failures[0].Mode.Should().BeNull();
		result.Report.Should().NotContain("mode=");
	}

	[Fact(DisplayName = "A high-fidelity failure before the result line is T1-09 and T1-17 for that shape")]
	public async Task HighFidelityGovernance_BeforeResultLine_MapsThoseTwo()
	{
		var result = await RunTemp(new Case { FailedHighFidelityShape = "LeanApi", FailedHighFidelityVerdict = "governance" });
		result.Failures.Select(failure => failure.Protocol).Should().BeEquivalentTo(["T1-09", "T1-17"]);
		result.Failures.Should().OnlyContain(failure => failure.Shape == "R3" && failure.Mode == null);
	}

	[Fact(DisplayName = "A missing high-fidelity shape is not run and has no mode")]
	public async Task MissingHighFidelityShape_IsNotRun()
	{
		var result = await RunTemp(new Case { OmitHighFidelityShape = "WorkerBaseline" });
		result.Failures.Should().HaveCount(6);
		result.Failures.Should().OnlyContain(failure => failure.Shape == "R6" && failure.Mode == null && failure.Detail == "not run");
		result.Report.Should().NotContain("mode=");
	}

	[Fact(DisplayName = "A missing oracle cell is T1-14 for that shape and mode")]
	public async Task MissingOracleCell_IsNotRun()
	{
		var result = await RunTemp(new Case { OmitOracleCell = "R4-PO" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-14");
		result.Failures[0].Shape.Should().Be("R4");
		result.Failures[0].Mode.Should().Be("PO");
		result.Failures[0].Detail.Should().Be("not run");
	}

	[Fact(DisplayName = "A fault cell failure is T1-15 for that shape and protocol")]
	public async Task FaultCellFailure_IsT1_15()
	{
		var result = await RunTemp(new Case { FailedFault = "FAULT-WORKER timeout" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-15");
		result.Failures[0].Shape.Should().Be("R5");
		result.Failures[0].Mode.Should().Be("timeout");
	}

	[Fact(DisplayName = "A missing fault cell is T1-15 not run")]
	public async Task MissingFaultCell_IsNotRun()
	{
		var result = await RunTemp(new Case { OmitFaultCell = "FAULT-GATEWAY saturate" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-15");
		result.Failures[0].Shape.Should().Be("R2");
		result.Failures[0].Mode.Should().Be("saturate");
		result.Failures[0].Detail.Should().Be("not run");
	}

	[Fact(DisplayName = "A sampling cell failure is T1-16 for that shape and scenario")]
	public async Task SamplingCellFailure_IsT1_16()
	{
		var result = await RunTemp(new Case { FailedSampling = "SAMP-MSG SAMP-03" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-16");
		result.Failures[0].Shape.Should().Be("R1");
		result.Failures[0].Mode.Should().Be("SAMP-03");
	}

	[Fact(DisplayName = "A missing sampling cell is T1-16 not run")]
	public async Task MissingSamplingCell_IsNotRun()
	{
		var result = await RunTemp(new Case { OmitSamplingCell = "SAMP-HTTP SAMP-04" });
		result.Failures.Should().ContainSingle();
		result.Failures[0].Protocol.Should().Be("T1-16");
		result.Failures[0].Shape.Should().Be("R2");
		result.Failures[0].Mode.Should().Be("SAMP-04");
		result.Failures[0].Detail.Should().Be("not run");
	}

	[Fact(DisplayName = "A generated suite keeps every failed test name")]
	public async Task GeneratedSuite_KeepsEveryFailedName()
	{
		var result = await RunTemp(new Case { GeneratedFailures = ["FirstFact", "SecondFact"] });
		var details = result.Failures.Where(failure => failure.Protocol == "generated-tests" && failure.Shape == "R1").Select(failure => failure.Detail).ToArray();
		details.Should().Contain("FirstFact");
		details.Should().Contain("SecondFact");
	}

	[Fact(DisplayName = "R1 through R4 with tests off fail the generated-test protocol")]
	public async Task TestsOff_OnR1ThroughR4_FailsGeneratedTests()
	{
		var scenario = new Case { TestsOff = "R3" };
		var result = await RunTemp(scenario);
		result.Failures.Should().Contain(failure => failure.Protocol == "generated-tests" && failure.Shape == "R3" && failure.Detail == "Tests == false");
		var tested = scenario.Calls.Where(call => call.Length > 1 && call[0] == "test").Select(call => call[1]);
		tested.Should().NotContain(path => path.EndsWith("R3.slnx", StringComparison.Ordinal));
		tested.Should().Contain(path => path.EndsWith("R1.slnx", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "A throwing provision fails generated tests for R1 through R4")]
	public async Task ProvisionThrows_FailsGeneratedTestsForR1ThroughR4()
	{
		var scenario = new Case { ProvisionThrows = true };
		var result = await RunTemp(scenario);
		result.Failures.Should().Contain(failure => failure.Protocol == "provision" && failure.Detail.Contains("pack failed", StringComparison.Ordinal));
		foreach (var shape in new[] { "R1", "R2", "R3", "R4" })
			result.Failures.Should().Contain(failure => failure.Protocol == "generated-tests" && failure.Shape == shape && failure.Detail == "not run");

		result.Failures.Should().Contain(failure => failure.Protocol == "T1-13" && failure.Detail == "not run");
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-04" && failure.Detail == "not run");
		result.Failures.Should().Contain(failure => failure.Protocol == "T1-18" && failure.Detail == "not run");
		var kinds = scenario.Calls.Select(Kind).ToArray();
		kinds.Should().Contain("runtime");
		kinds.Should().NotContain("architecture");
		kinds.Should().NotContain("guide");
	}

	private static async Task<Tier1Result> RunTemp(Case scenario)
	{
		var root = TempRoot();
		try
		{
			return await RunAsync(root, scenario);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string TempRoot()
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		return root;
	}

	private static async Task AssertSecretAsync(string root, string generatedConfiguration, string name)
	{
		var result = await RunAsync(root, new Case(), generatedConfiguration);
		result.ExitCode.Should().NotBe(0);
		result.Failures.Should().Contain(failure => failure.Protocol == "secrets" && failure.Detail == name);
		result.Failures.Should().OnlyContain(failure => failure.Protocol == "secrets");
		result.Report.Should().Contain("FAILED secrets " + name);
	}

	private static async Task<Tier1Result> RunAsync(string root, Case scenario, string? generatedConfiguration = null)
	{
		WriteConfigurations(root, scenario, generatedConfiguration);
		var runner = new ScriptRunner(scenario);
		var session = new Tier1Session
					  {
						  ProductRoot = root,
						  Runner = runner,
						  DockerAvailable = _ => Task.FromResult(scenario.Docker),
						  ConfigurationFiles = scenario.CaptureScannedShapes
												   ? (_, shapes) =>
													 {
														 scenario.ScannedShapeIds = shapes.Select(shape => shape.Id).ToArray();
														 return shapes.SelectMany(shape => Directory.Exists(shape.OutputDirectory)
																								? Directory.EnumerateFiles(shape.OutputDirectory, "appsettings*.json")
																								: []).ToArray();
													 }
												   : null,
						  Provision = (productRoot, invocation, _) =>
									  {
										  scenario.ProvisionCalls++;
										  scenario.ProvisionInvocation = invocation;
										  productRoot.Should().Be(root);
										  if (scenario.ProvisionThrows)
											  throw new InvalidOperationException("pack failed");

										  return Task.FromResult<Tier1Provision>(new MemoryProvision(scenario.Shapes, root));
									  }
					  };
		return await Tier1Run.ExecuteAsync(session, CancellationToken.None);
	}

	private static void WriteConfigurations(string root, Case scenario, string? generatedConfiguration)
	{
		foreach (var host in new[] { "Api", "Worker", "Common.ApiGateway" })
		{
			var directory = Path.Combine(root, "src", "Content", "Backend", "Solution", "Monaco.Template.Backend." + host);
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "appsettings.json"), ValidConfiguration);
			File.WriteAllText(Path.Combine(directory, "appsettings.Development.json"), ValidConfiguration);
		}

		string[] ids = ["R1", "R2", "R3", "R4", "R5", "R6", "R7", "E1", "E2", "E3", "E4"];
		scenario.Shapes = ids
						  .Select(id =>
								  {
									  var directory = Path.Combine(root, "shapes", id);
									  Directory.CreateDirectory(directory);
									  var configuration = generatedConfiguration is null || id != "R1" ? ValidConfiguration : generatedConfiguration;
									  File.WriteAllText(Path.Combine(directory, "appsettings.json"), configuration);
									  var tests = id is "R1" or "R2" or "R3" or "R4" or "E4";
									  if (id == scenario.TestsOff)
										  tests = false;

									  return new Tier1Shape(id, Path.Combine(directory, id + ".slnx"), directory, tests);
								  })
						  .ToArray();
	}

	private static string Kind(IReadOnlyList<string> args)
	{
		var project = args.Count > 1 ? args[1] : "";
		var joined = string.Join('\n', args);
		if (joined.Contains("ObservabilityGuideTemplateGenerationTests", StringComparison.Ordinal))
			return "guide";

		if (joined.Contains("HighFidelityEmission", StringComparison.Ordinal) &&
			joined.Contains("RuntimeOracleTests", StringComparison.Ordinal))
			return "runtime";

		if (joined.Contains("migrations.cs", StringComparison.Ordinal))
			return "migrations";

		if (args.Count > 0 && args[0] == "test" && project.EndsWith("Monaco.Template.Backend.slnx", StringComparison.Ordinal))
			return "solution";

		if (args.Count > 0 && args[0] == "test" && project.Contains("Monaco.Template.Backend.ArchitectureTests.csproj", StringComparison.Ordinal))
			return "architecture";

		if (args.Count > 0 && args[0] == "test" && project.EndsWith(".slnx", StringComparison.Ordinal))
			return "generated";

		return "other";
	}

	private static string TrxFor(string kind, Case scenario)
	{
		var rows = new List<TrxRow>();
		switch (kind)
		{
			case "solution":
				if (!scenario.OmitLegacy)
					rows.Add(Row("Monaco.Template.Backend.ArchitectureTests.LegacyTelemetryInventoryTests", "Inventory", "legacy", "Passed"));

				rows.Add(Row("Monaco.Template.Backend.Common.Observability.Tests.ObservabilityPackageGraphTests",
							 "Graph",
							 "package graph",
							 scenario.FailedSourceClass == "ObservabilityPackageGraphTests" ? "Failed" : "Passed"));
				rows.Add(Row("Monaco.Template.Backend.Common.Observability.Tests.ObservabilityConfigurationTests", "Config", "configuration", "Passed"));
				rows.Add(Row("Monaco.Template.Backend.Common.Observability.Tests.ObservabilityGuideTokenTests", "Guide", "guide tokens", "Passed"));
				if (!scenario.OmitProductCreate)
					rows.Add(Row("Monaco.Template.Backend.IntegrationTests.Tests.ProductsTests",
								 "CreateNewProductSucceeds",
								 "Create new Product succeeds",
								 scenario.FailProductCreate ? "Failed" : "Passed"));

				if (scenario.FailTimestamp)
					rows.Add(Row("Monaco.Template.Backend.Application.Tests.ClockTests", "Clock", "timestamp recorded", "Failed"));

				rows.Add(Row("Monaco.Template.Backend.Application.Tests.Persistence.AttributionCompositionBoundaryTests",
							 "Stamps",
							 "Stamp names stay off public surfaces",
							 "Passed"));
				if (scenario.SkipSourceTest)
					rows.Add(Row("Monaco.Template.Backend.IntegrationTests.Tests.CompaniesTests", "List", "Get Companies succeeds", "NotExecuted"));

				break;
			case "architecture":
				if (!scenario.OmitComposition)
					rows.Add(Row("Monaco.Template.Backend.ArchitectureTests.SupportedRuntimeShapeCompositionTests",
								 "Comp",
								 "COMP-01 R1 project/package graph",
								 "Passed"));

				if (scenario.FailComp03)
					rows.Add(Row("Monaco.Template.Backend.ArchitectureTests.SupportedRuntimeShapeCompositionTests",
								 "Comp03",
								 "COMP-03 R1 persistence attribution composition",
								 "Failed"));

				if (!scenario.OmitStaticEdge)
					rows.Add(Row("Monaco.Template.Backend.ArchitectureTests.StaticEdgeTruthfulnessTests",
								 "Edge",
								 "E2 omits the guide",
								 "Passed"));

				break;
			case "guide":
				rows.Add(Row("Monaco.Eng.GeneratedShapeHarness.ObservabilityGuideTemplateGenerationTests",
							 "Guide",
							 "guide generation",
							 scenario.FailGuide ? "Failed" : "Passed"));
				break;
			case "runtime":
				foreach (var name in new[] { "FullApiWorker", "GatewayFull", "LeanApi", "PublisherApi", "WorkerConsumer", "WorkerBaseline", "FullApiWorkerWithoutTests" })
				{
					if (name == scenario.OmitHighFidelityShape)
						continue;

					var failed = name == scenario.FailedHighFidelityShape;
					rows.Add(Row("Monaco.Eng.RuntimeShapeVerification.HighFidelityEmissionTests",
								 "HighFidelityEmission",
								 "HighFidelityEmission(id: " + name + ")",
								 failed ? "Failed" : "Passed",
								 failed ? HighFidelityOutput(name, scenario.FailedHighFidelityVerdict) : ""));
				}

				foreach (var profile in new[] { "FAULT-API", "FAULT-WORKER", "FAULT-GATEWAY" })
				{
					foreach (var protocol in new[] { "refuse", "timeout", "401", "403", "429", "malformed", "saturate", "shutdown", "recovery" })
					{
						var cell = profile + " " + protocol;
						if (cell == scenario.OmitFaultCell)
							continue;

						rows.Add(Row("Monaco.Eng.RuntimeShapeVerification.FaultIsolationTests",
									 "FaultIsolation",
									 cell,
									 cell == scenario.FailedFault ? "Failed" : "Passed"));
					}
				}

				foreach (var flow in new[] { "SAMP-HTTP", "SAMP-MSG" })
				{
					foreach (var samplingScenario in new[] { "SAMP-01", "SAMP-02", "SAMP-03", "SAMP-04" })
					{
						var cell = flow + " " + samplingScenario;
						if (cell == scenario.OmitSamplingCell)
							continue;

						rows.Add(Row("Monaco.Eng.RuntimeShapeVerification.SamplingPropagationTests",
									 "SamplingPropagation",
									 cell,
									 cell == scenario.FailedSampling ? "Failed" : "Passed"));
					}
				}

				goto case "oracle";
			case "oracle":
				foreach (var shape in new[] { "R1", "R2", "R3", "R4", "R5", "R6", "R7" })
				{
					foreach (var mode in new[] { "DA", "DU", "PO" })
					{
						var cell = shape + "-" + mode;
						if (cell == scenario.OmitOracleCell)
							continue;

						rows.Add(Row("Monaco.Eng.RuntimeShapeVerification.RuntimeOracleTests",
									 cell,
									 cell + " cell",
									 scenario.FailedCell == cell ? "Failed" : "Passed"));
					}
				}

				break;
			case "generated-fail":
				foreach (var name in scenario.GeneratedFailures)
					rows.Add(Row("Generated.Tests", name, name, "Failed"));

				break;
			default:
				rows.Add(Row("Generated.Tests", "Ok", "generated", "Passed"));
				break;
		}

		return Trx(rows);
	}

	private static string HighFidelityOutput(string shape, string? verdict)
	{
		if (verdict is null || verdict == "governance")
			return "";

		string Value(string key) => verdict == key ? "Fail" : "Match";
		return $"result shape={shape} HTTP={Value("HTTP")} dependency={Value("dependency")} messaging={Value("messaging")} applicationLog={Value("applicationLog")} authenticatedEntry={Value("authenticatedEntry")} companyCounter={Value("companyCounter")}";
	}

	private static TrxRow Row(string className, string method, string name, string outcome, string output = "") =>
		new(className, method, name, outcome, output);

	private static string Trx(IReadOnlyList<TrxRow> rows)
	{
		var document = new XDocument(new XElement("TestRun",
												  new XElement("Results",
															   rows.Select((row, index) =>
																		   {
																			   var result = new XElement("UnitTestResult",
																										 new XAttribute("testId", index.ToString()),
																										 new XAttribute("testName", row.Name),
																										 new XAttribute("outcome", row.Outcome));
																			   if (row.Output.Length > 0)
																				   result.Add(new XElement("Output", new XElement("StdOut", row.Output)));

																			   return result;
																		   })),
												  new XElement("TestDefinitions",
															   rows.Select((row, index) => new XElement("UnitTest",
																										 new XAttribute("id", index.ToString()),
																										 new XAttribute("name", row.Name),
																										 new XElement("TestMethod",
																													  new XAttribute("className", row.ClassName),
																													  new XAttribute("name", row.Method)))))));
		return document.ToString();
	}

	private sealed class Case
	{
		public bool Docker { get; init; } = true;
		public bool SkipSourceTest { get; init; }
		public bool ZeroExitDespiteFailures { get; init; }
		public string? FailedSourceClass { get; init; }
		public string? FailedCell { get; init; }
		public bool OmitLegacy { get; init; }
		public bool OmitProductCreate { get; init; }
		public bool FailProductCreate { get; init; }
		public bool FailTimestamp { get; init; }
		public bool FailComp03 { get; init; }
		public bool OmitComposition { get; init; }
		public bool OmitStaticEdge { get; init; }
		public bool UnusableSolution { get; init; }
		public bool FailGuide { get; init; }
		public int MigrationsExit { get; init; }
		public string? FailedHighFidelityShape { get; init; }
		public string? FailedHighFidelityVerdict { get; init; }
		public string? OmitHighFidelityShape { get; init; }
		public string? OmitOracleCell { get; init; }
		public string? FailedFault { get; init; }
		public string? OmitFaultCell { get; init; }
		public string? FailedSampling { get; init; }
		public string? OmitSamplingCell { get; init; }
		public string[] GeneratedFailures { get; init; } = [];
		public string? TestsOff { get; init; }
		public bool ProvisionThrows { get; init; }
		public bool CaptureScannedShapes { get; init; }
		public IReadOnlyList<string> ScannedShapeIds { get; set; } = [];
		public int ProvisionCalls { get; set; }
		public string? ProvisionInvocation { get; set; }
		public IReadOnlyList<Tier1Shape> Shapes { get; set; } = [];
		public List<string[]> Calls { get; } = [];
		public List<(string Kind, string Key, string Value)> Environments { get; } = [];
	}

	private sealed record TrxRow(string ClassName, string Method, string Name, string Outcome, string Output);

	private sealed class ScriptRunner : ITier1CommandRunner
	{
		private readonly Case _scenario;

		public ScriptRunner(Case scenario) =>
			_scenario = scenario;

		public Task<Tier1CommandResult> RunAsync(string fileName,
												 IReadOnlyList<string> arguments,
												 string workingDirectory,
												 CancellationToken cancellationToken,
												 IReadOnlyDictionary<string, string>? environment = null)
		{
			_scenario.Calls.Add(arguments.ToArray());
			var kind = Kind(arguments);
			if (kind is "migrations")
				return Task.FromResult(new Tier1CommandResult(_scenario.MigrationsExit, _scenario.MigrationsExit == 0 ? "OK" : "verify failed"));

			var results = arguments.SkipWhile(argument => argument != "--results-directory").Skip(1).FirstOrDefault();
			if (kind == "solution" && _scenario.UnusableSolution)
			{
				if (!string.IsNullOrEmpty(results))
				{
					Directory.CreateDirectory(results);
					File.WriteAllText(Path.Combine(results, "solution.trx"), "<TestRun />", Encoding.UTF8);
				}

				return Task.FromResult(new Tier1CommandResult(1, ""));
			}

			var project = arguments.Count > 1 ? arguments[1] : "";
			if (kind == "generated" && project.EndsWith("R1.slnx", StringComparison.Ordinal) && _scenario.GeneratedFailures.Length > 0)
				kind = "generated-fail";

			if (environment is not null)
			{
				foreach (var pair in environment)
					_scenario.Environments.Add((kind, pair.Key, pair.Value));
			}

			var xml = TrxFor(kind, _scenario);
			if (!string.IsNullOrEmpty(results))
			{
				Directory.CreateDirectory(results);
				File.WriteAllText(Path.Combine(results, kind + ".trx"), xml, Encoding.UTF8);
			}

			var failed = xml.Contains("outcome=\"Failed\"", StringComparison.Ordinal) || xml.Contains("outcome=\"NotExecuted\"", StringComparison.Ordinal);
			var exit = failed && !_scenario.ZeroExitDespiteFailures ? 1 : 0;
			return Task.FromResult(new Tier1CommandResult(exit, ""));
		}
	}
}
