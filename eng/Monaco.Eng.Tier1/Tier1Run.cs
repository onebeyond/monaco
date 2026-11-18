using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ShapeHarness = Monaco.Eng.GeneratedShapeHarness.GeneratedShapeHarness;

namespace Monaco.Eng.Tier1;

public static class Tier1Run
{
	private static readonly string[] RegistryShapes = ["R1", "R2", "R3", "R4", "R5", "R6", "R7"];
	private static readonly string[] CellModes = ["DA", "DU", "PO"];
	private static readonly string[] GeneratedTestShapes = ["R1", "R2", "R3", "R4"];
	private static readonly string[] StaticEdgeShapes = ["E1", "E2", "E3", "E4"];
	private static readonly string[] HighFidelityProtocols = ["T1-06", "T1-07", "T1-08", "T1-09", "T1-10", "T1-17"];
	private static readonly string[] SolutionProtocols = ["T1-01", "T1-02", "T1-03", "T1-04", "T1-05", "T1-11", "T1-18"];
	private static readonly string[] GuideProtocols = ["T1-04", "T1-18"];
	private static readonly string[] RuntimeProtocols = ["T1-06", "T1-07", "T1-08", "T1-09", "T1-10", "T1-14", "T1-15", "T1-16", "T1-17"];
	private const string RuntimeProject = "eng/Monaco.Eng.RuntimeShapeVerification/Monaco.Eng.RuntimeShapeVerification.csproj";
	private const string RuntimeFilter = "FullyQualifiedName~HighFidelityEmission|FullyQualifiedName~RuntimeOracleTests|FullyQualifiedName~FaultIsolation|FullyQualifiedName~SamplingPropagation";
	private const string SolutionProject = "src/Content/Backend/Solution/Monaco.Template.Backend.slnx";
	private const string SolutionFilter = "FullyQualifiedName!~MigrationMaintenanceCommandTests&FullyQualifiedName!~SupportedRuntimeShapeCompositionTests&FullyQualifiedName!~StaticEdgeTruthfulnessTests";
	private const string ArchitectureProject = "src/Content/Backend/Solution/Monaco.Template.Backend.ArchitectureTests/Monaco.Template.Backend.ArchitectureTests.csproj";
	private const string ArchitectureFilter = "FullyQualifiedName~SupportedRuntimeShapeCompositionTests|FullyQualifiedName~StaticEdgeTruthfulnessTests";
	private const string GuideProject = "eng/Monaco.Eng.GeneratedShapeHarness/Monaco.Eng.GeneratedShapeHarness.csproj";
	private const string GuideFilter = "FullyQualifiedName~ObservabilityGuideTemplateGenerationTests";

	private static readonly string[] ApplicationInsightsTokens = ["ApplicationInsights", "InstrumentationKey", "APPINSIGHTS"];
	private static readonly string[] AuditTokens = ["AuditEntry", "AuditLog", "AuditEntries", "INonAuditable", "AuditTo"];

	private static readonly (string Profile, string Shape)[] FaultProfiles =
	[
		("FAULT-GATEWAY", "R2"),
		("FAULT-WORKER", "R5"),
		("FAULT-API", "R1")
	];

	private static readonly string[] FaultProtocols =
	[
		"refuse",
		"timeout",
		"401",
		"403",
		"429",
		"malformed",
		"saturate",
		"shutdown",
		"recovery"
	];

	private static readonly (string Flow, string Shape)[] SamplingFlows =
	[
		("SAMP-HTTP", "R2"),
		("SAMP-MSG", "R1")
	];

	private static readonly string[] SamplingScenarios = ["SAMP-01", "SAMP-02", "SAMP-03", "SAMP-04"];

	private static readonly (string Key, string Protocol)[] HighFidelityVerdicts =
	[
		("HTTP", "T1-06"),
		("messaging", "T1-06"),
		("authenticatedEntry", "T1-07"),
		("dependency", "T1-08"),
		("companyCounter", "T1-10"),
		("applicationLog", "T1-17")
	];

	private static readonly Regex StampWord = new(@"\bstamp\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
	private static readonly Regex ResultShapeLine = new(@"result shape=(?<shape>\S+)(?<rest>[^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
	private static readonly Regex VerdictToken = new(@"\b(?<key>HTTP|dependency|messaging|applicationLog|authenticatedEntry|companyCounter)=(?<value>\S+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
	private static readonly Regex WordToken = new(@"\b(?<token>refuse|timeout|401|403|429|malformed|saturate|shutdown|recovery|SAMP-0[1-4])\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

	private static readonly (string EnumName, string Shape)[] ShapeNames =
	[
		("FullApiWorkerWithoutTests", "R7"),
		("FullApiWorker", "R1"),
		("GatewayFull", "R2"),
		("LeanApi", "R3"),
		("PublisherApi", "R4"),
		("WorkerConsumer", "R5"),
		("WorkerBaseline", "R6")
	];

	public static IReadOnlyList<string> DockerProtocols { get; } =
	[
		"T1-06",
		"T1-07",
		"T1-08",
		"T1-09",
		"T1-10",
		"T1-12",
		"T1-14",
		"T1-15",
		"T1-16",
		"T1-17"
	];

	public static async Task<int> Main()
	{
		var productRoot = FindProductRoot();
		var result = await ExecuteAsync(Tier1Session.Live(productRoot), CancellationToken.None);
		Console.WriteLine(result.Report);
		return result.ExitCode;
	}

	public static async Task<Tier1Result> ExecuteAsync(Tier1Session session, CancellationToken cancellationToken)
	{
		var failures = new List<Tier1Failure>();
		Tier1Provision? provision = null;
		try
		{
			await RunStepAsync(failures,
							   () => RunSolutionAsync(session, failures, cancellationToken),
							   ex => SolutionBlanket(ex.Message));
			var docker = false;
			try
			{
				docker = await session.DockerAvailable(cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				docker = false;
			}

			if (!docker)
			{
				foreach (var protocol in DockerProtocols)
					Add(failures, new(protocol, null, null, "Docker daemon unavailable; a skip is not a pass."));
			}
			else
			{
				await RunStepAsync(failures,
								   () => RunRuntimeAsync(session, failures, cancellationToken),
								   ex => Blanket(RuntimeProtocols, ex.Message));
				await RunStepAsync(failures,
								   () => RunMigrationsAsync(session, failures, cancellationToken),
								   ex => [new("T1-12", null, null, ex.Message)]);
			}

			try
			{
				provision = await session.Provision(session.ProductRoot, "tier1", cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Add(failures, new("provision", null, null, ex.Message));
			}

			if (provision is null)
			{
				Add(failures, new("T1-13", null, null, "not run"));
				AddRange(failures, Blanket(GuideProtocols, "not run"));
			}
			else
			{
				var captured = provision;
				await RunStepAsync(failures,
								   () => RunArchitectureAsync(session, failures, captured, cancellationToken),
								   ex => [new("T1-13", null, null, ex.Message)]);
				await RunStepAsync(failures,
								   () => RunFilteredAsync(session,
														  failures,
														  GuideProject,
														  GuideFilter,
														  GuideProtocols,
														  tests => AttributeGuide(tests, failures),
														  cancellationToken,
														  ProvisionEnvironment(captured)),
								   ex => Blanket(GuideProtocols, ex.Message));
			}

			if (!docker)
			{
				foreach (var shape in GeneratedTestShapes)
					Add(failures, new("generated-tests", shape, null, "Docker daemon unavailable; a skip is not a pass."));
			}
			else if (provision is null)
			{
				foreach (var shape in GeneratedTestShapes)
					Add(failures, new("generated-tests", shape, null, "not run"));
			}
			else
			{
				foreach (var id in GeneratedTestShapes)
				{
					var shape = provision.Shapes.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
					if (shape is null || !shape.Tests)
					{
						Add(failures, new("generated-tests", id, null, "Tests == false"));
						continue;
					}

					var captured = shape;
					await RunStepAsync(failures,
									   () => RunGeneratedAsync(session, failures, captured, cancellationToken),
									   ex => [new("generated-tests", captured.Id, null, ex.Message)]);
				}
			}

			try
			{
				var files = session.ConfigurationFiles is null
								? DiscoverConfiguration(session.ProductRoot, provision?.Shapes)
								: session.ConfigurationFiles(session.ProductRoot, provision?.Shapes ?? []);
				AddSecretFailures(files, failures);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Add(failures, new("secrets", null, null, ex.Message));
			}
		}
		finally
		{
			if (provision is not null)
			{
				try
				{
					await provision.DisposeAsync();
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					Add(failures, new("provision", null, null, ex.Message));
				}
			}
		}

		return Tier1Result.From(failures);
	}

	internal static string FindProductRoot()
	{
		foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
		{
			for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
			{
				var candidate = Path.Combine(directory.FullName, "src", "Monaco.Template.nuspec");
				if (File.Exists(candidate))
					return directory.FullName;
			}
		}

		throw new DirectoryNotFoundException("Could not locate the product root containing src/Monaco.Template.nuspec.");
	}

	private static async Task RunSolutionAsync(Tier1Session session, List<Tier1Failure> failures, CancellationToken cancellationToken)
	{
		var tests = await TestAsync(session, SolutionProject, SolutionFilter, cancellationToken);
		if (tests.Blanket)
		{
			AddRange(failures, Blanket(SolutionProtocols, tests.Detail));
			Add(failures, new("T1-13", null, null, tests.Detail));
			return;
		}

		foreach (var test in tests.Results.Where(test => !test.Passed))
			AddRange(failures, AttributeSolution(test));

		if (!tests.Results.Any(test => test.ClassName.Contains("LegacyTelemetryInventoryTests", StringComparison.Ordinal)))
			Add(failures, new("T1-01", null, null, "not run"));

		if (!tests.Results.Any(test => test.ClassName.Contains("ObservabilityPackageGraphTests", StringComparison.Ordinal)))
			Add(failures, new("T1-02", null, null, "did not run"));

		if (!tests.Results.Any(test => test.ClassName.Contains("ObservabilityConfigurationTests", StringComparison.Ordinal)))
			Add(failures, new("T1-03", null, null, "did not run"));

		if (!tests.Results.Any(test => test.ClassName.Contains("ObservabilityGuideTokenTests", StringComparison.Ordinal)))
			AddRange(failures, Blanket(GuideProtocols, "did not run"));

		if (!tests.Results.Any(IsProductCreateFact))
			Add(failures, new("T1-05", null, null, "not run"));

		if (!tests.Results.Any(IsStampFact))
			Add(failures, new("T1-11", null, null, "did not run"));
	}

	private static async Task RunArchitectureAsync(Tier1Session session,
												   List<Tier1Failure> failures,
												   Tier1Provision provision,
												   CancellationToken cancellationToken)
	{
		var tests = await TestAsync(session, ArchitectureProject, ArchitectureFilter, cancellationToken, ProvisionEnvironment(provision));
		if (tests.Blanket)
		{
			Add(failures, new("T1-13", null, null, tests.Detail));
			return;
		}

		foreach (var test in tests.Results.Where(test => !test.Passed))
			AddRange(failures, AttributeSolution(test));

		foreach (var className in new[] { "SupportedRuntimeShapeCompositionTests", "StaticEdgeTruthfulnessTests" })
		{
			if (tests.Results.Any(test => test.ClassName.Contains(className, StringComparison.Ordinal)))
				continue;

			Add(failures, new("T1-13", null, null, className + " not run"));
		}
	}

	private static IReadOnlyDictionary<string, string>? ProvisionEnvironment(Tier1Provision provision) =>
		string.IsNullOrWhiteSpace(provision.TempRoot)
			? null
			: new Dictionary<string, string> { [ShapeHarness.ProvisionRootVariable] = provision.TempRoot };

	private static async Task RunRuntimeAsync(Tier1Session session, List<Tier1Failure> failures, CancellationToken cancellationToken)
	{
		// One process shares the runtime hive. Separate filters each generate and build R1–R7 again.
		var tests = await TestAsync(session, RuntimeProject, RuntimeFilter, cancellationToken);
		if (tests.Blanket)
		{
			AddRange(failures, Blanket(RuntimeProtocols, tests.Detail));
			return;
		}

		AttributeHighFidelity(tests.Results.Where(test => test.ClassName.Contains("HighFidelityEmission", StringComparison.Ordinal)).ToArray(), failures);
		AttributeOracle(tests.Results.Where(test => test.ClassName.Contains("RuntimeOracleTests", StringComparison.Ordinal)).ToArray(), failures);
		var faults = tests.Results.Where(test => test.ClassName.Contains("FaultIsolation", StringComparison.Ordinal)).ToArray();
		AttributeFault(faults, failures);
		var sampling = tests.Results.Where(test => test.ClassName.Contains("SamplingPropagation", StringComparison.Ordinal)).ToArray();
		AttributeSampling(sampling, failures);
	}

	private static void AttributeHighFidelity(IReadOnlyList<ObservedTest> tests, List<Tier1Failure> failures)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var test in tests)
		{
			var shape = MapShape(test.TestName + " " + test.MethodName);
			var hasLine = TryReadResultLine(test.Output, out var lineShape, out var verdicts);
			if (lineShape.Length > 0)
				shape = lineShape;

			if (shape.Length > 0)
				seen.Add(shape);

			if (test.Passed)
				continue;

			var failedVerdicts = new List<string>();
			if (hasLine)
			{
				foreach (var (key, protocol) in HighFidelityVerdicts)
				{
					if (!verdicts.TryGetValue(key, out var value) || IsPassingVerdict(value))
						continue;

					failedVerdicts.Add(protocol);
				}
			}

			var reportedShape = shape.Length == 0 ? null : shape;
			foreach (var protocol in ResolveHighFidelity(test, hasLine, failedVerdicts))
				Add(failures, new(protocol, reportedShape, null, test.TestName));
		}

		foreach (var shape in RegistryShapes.Where(shape => !seen.Contains(shape)))
		{
			foreach (var protocol in HighFidelityProtocols)
				Add(failures, new(protocol, shape, null, "not run"));
		}
	}

	private static async Task RunFilteredAsync(Tier1Session session,
											   List<Tier1Failure> failures,
											   string project,
											   string filter,
											   IReadOnlyList<string> protocols,
											   Action<IReadOnlyList<ObservedTest>> attribute,
											   CancellationToken cancellationToken,
											   IReadOnlyDictionary<string, string>? environment = null)
	{
		var tests = await TestAsync(session, project, filter, cancellationToken, environment);
		if (tests.Blanket)
		{
			AddRange(failures, Blanket(protocols, tests.Detail));
			return;
		}

		attribute(tests.Results);
	}

	private static async Task RunMigrationsAsync(Tier1Session session, List<Tier1Failure> failures, CancellationToken cancellationToken)
	{
		var result = await session.Runner.RunAsync("dotnet",
												   ["run", "--file", "eng/migrations.cs", "--", "verify"],
												   session.ProductRoot,
												   cancellationToken);
		if (result.ExitCode != 0)
			Add(failures, new("T1-12", null, null, Trim(result.Output)));
	}

	private static async Task RunGeneratedAsync(Tier1Session session,
												List<Tier1Failure> failures,
												Tier1Shape shape,
												CancellationToken cancellationToken)
	{
		var tests = await TestAsync(session, shape.SolutionPath, filter: null, cancellationToken);
		if (tests.Blanket)
		{
			Add(failures, new("generated-tests", shape.Id, null, tests.Detail));
			return;
		}

		foreach (var test in tests.Results.Where(test => !test.Passed))
			Add(failures, new("generated-tests", shape.Id, null, test.TestName));
	}

	private static async Task<TestCommandOutcome> TestAsync(Tier1Session session,
															string project,
															string? filter,
															CancellationToken cancellationToken,
															IReadOnlyDictionary<string, string>? environment = null)
	{
		var resultsDirectory = Path.Combine(Path.GetTempPath(), "monaco-tier1-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(resultsDirectory);
		try
		{
			List<string> arguments = ["test", project, "--results-directory", resultsDirectory, "--logger", "trx", "--nologo"];
			if (!string.IsNullOrEmpty(filter))
			{
				arguments.Add("--filter");
				arguments.Add(filter);
			}

			var result = await session.Runner.RunAsync("dotnet", arguments, session.ProductRoot, cancellationToken, environment);
			var tests = ReadTrx(resultsDirectory);
			var failed = tests.Any(test => !test.Passed);
			if (tests.Count == 0 || (result.ExitCode != 0 && !failed))
				return new(true, [], Trim(result.Output.Length == 0 ? $"exit {result.ExitCode}" : result.Output));

			return new(false, tests, Trim(result.Output));
		}
		finally
		{
			try
			{
				Directory.Delete(resultsDirectory, recursive: true);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}

	private static IEnumerable<Tier1Failure> AttributeSolution(ObservedTest test)
	{
		if (test.ClassName.Contains("ObservabilityGuideTokenTests", StringComparison.Ordinal))
		{
			foreach (var protocol in GuideProtocols)
				yield return new(protocol, null, null, test.TestName);

			yield break;
		}

		if (test.ClassName.Contains("ObservabilityPackageGraphTests", StringComparison.Ordinal))
		{
			yield return new("T1-02", null, null, test.TestName);
			yield break;
		}

		if (test.ClassName.Contains("ObservabilityConfigurationTests", StringComparison.Ordinal))
		{
			yield return new("T1-03", null, null, test.TestName);
			yield break;
		}

		if (IsProductCreateFact(test))
		{
			yield return new("T1-05", null, null, test.TestName);
			yield break;
		}

		if (IsStampFact(test))
		{
			yield return new("T1-11", null, null, test.TestName);
			yield break;
		}

		if (IsStaticShapeFact(test))
		{
			var source = test.TestName + " " + test.MethodName;
			var shape = RegistryShapes.Concat(StaticEdgeShapes).FirstOrDefault(id => source.Contains(id, StringComparison.Ordinal));
			yield return new("T1-13", shape, null, test.TestName);
			yield break;
		}

		yield return new("T1-01", null, null, test.TestName);
	}

	private static void AttributeGuide(IReadOnlyList<ObservedTest> tests, List<Tier1Failure> failures)
	{
		foreach (var test in tests.Where(test => !test.Passed))
		{
			foreach (var protocol in GuideProtocols)
				Add(failures, new(protocol, null, null, test.TestName));
		}

		if (tests.Any(test => test.ClassName.Contains("ObservabilityGuideTemplateGenerationTests", StringComparison.Ordinal)))
			return;

		AddRange(failures, Blanket(GuideProtocols, "not run"));
	}

	private static void AttributeOracle(IReadOnlyList<ObservedTest> tests, List<Tier1Failure> failures)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var test in tests)
		{
			if (!TryOracleCell(test, out var shape, out var mode))
			{
				if (!test.Passed)
					Add(failures, new("T1-14", null, null, test.TestName));

				continue;
			}

			seen.Add(shape + "-" + mode);
			if (!test.Passed)
				Add(failures, new("T1-14", shape, mode, test.TestName));
		}

		foreach (var shape in RegistryShapes)
		{
			foreach (var mode in CellModes)
			{
				if (seen.Contains(shape + "-" + mode))
					continue;

				Add(failures, new("T1-14", shape, mode, "not run"));
			}
		}
	}

	private static void AttributeFault(IReadOnlyList<ObservedTest> tests, List<Tier1Failure> failures)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var test in tests)
		{
			var source = test.TestName + " " + test.MethodName;
			if (!TryFaultCell(source, out var shape, out var protocol))
			{
				if (!test.Passed)
					Add(failures, new("T1-15", null, null, test.TestName));

				continue;
			}

			seen.Add(shape + " " + protocol);
			if (!test.Passed)
				Add(failures, new("T1-15", shape, protocol, test.TestName));
		}

		foreach (var (_, shape) in FaultProfiles)
		{
			foreach (var protocol in FaultProtocols)
			{
				if (seen.Contains(shape + " " + protocol))
					continue;

				Add(failures, new("T1-15", shape, protocol, "not run"));
			}
		}
	}

	private static void AttributeSampling(IReadOnlyList<ObservedTest> tests, List<Tier1Failure> failures)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var test in tests)
		{
			var source = test.TestName + " " + test.MethodName;
			if (!TrySamplingCell(source, out var shape, out var scenario))
			{
				if (!test.Passed)
					Add(failures, new("T1-16", null, null, test.TestName));

				continue;
			}

			seen.Add(shape + " " + scenario);
			if (!test.Passed)
				Add(failures, new("T1-16", shape, scenario, test.TestName));
		}

		foreach (var (_, shape) in SamplingFlows)
		{
			foreach (var scenario in SamplingScenarios)
			{
				if (seen.Contains(shape + " " + scenario))
					continue;

				Add(failures, new("T1-16", shape, scenario, "not run"));
			}
		}
	}

	private static bool TryOracleCell(ObservedTest test, out string shape, out string mode)
	{
		var source = test.TestName + " " + test.MethodName;
		foreach (var id in RegistryShapes)
		{
			foreach (var cellMode in CellModes)
			{
				if (!source.Contains(id + "-" + cellMode, StringComparison.Ordinal))
					continue;

				shape = id;
				mode = cellMode;
				return true;
			}
		}

		shape = MapShape(source);
		mode = source.Contains("ReceiverUnavailable", StringComparison.Ordinal)
				   ? "DU"
				   : source.Contains("ProductionOverride", StringComparison.Ordinal)
					   ? "PO"
					   : source.Contains("ReceiverAvailable", StringComparison.Ordinal)
						   ? "DA"
						   : "";
		return shape.Length > 0 && mode.Length > 0;
	}

	private static bool IsStaticShapeFact(ObservedTest test) =>
		test.ClassName.Contains("SupportedRuntimeShapeCompositionTests", StringComparison.Ordinal) ||
		test.ClassName.Contains("StaticEdgeTruthfulnessTests", StringComparison.Ordinal);

	private static bool IsProductCreateFact(ObservedTest test) =>
		test.ClassName.Contains("ProductsTests", StringComparison.Ordinal) &&
		(test.MethodName.Contains("CreateNewProductSucceeds", StringComparison.Ordinal) ||
		 test.TestName.Contains("Create new Product succeeds", StringComparison.Ordinal));

	private static bool IsStampFact(ObservedTest test)
	{
		if (test.ClassName.Contains("AuditableStampLifecycleTests", StringComparison.Ordinal) ||
			test.ClassName.Contains("AttributionCompositionBoundaryTests", StringComparison.Ordinal))
			return true;

		var name = test.TestName + " " + test.MethodName;
		return name.Contains("COMP-03", StringComparison.Ordinal) ||
			   name.Contains("persistence attribution", StringComparison.OrdinalIgnoreCase) ||
			   StampWord.IsMatch(name);
	}

	private static bool TryReadResultLine(string output, out string shape, out Dictionary<string, string> verdicts)
	{
		shape = "";
		verdicts = new(StringComparer.OrdinalIgnoreCase);
		var match = ResultShapeLine.Match(output);
		if (!match.Success)
			return false;

		shape = MapShape(match.Groups["shape"].Value);
		foreach (Match token in VerdictToken.Matches(match.Groups["rest"].Value))
			verdicts[token.Groups["key"].Value] = token.Groups["value"].Value;

		return true;
	}

	private static bool IsPassingVerdict(string value) =>
		value.Equals("Match", StringComparison.OrdinalIgnoreCase) ||
		value.Equals("NotApplicable", StringComparison.OrdinalIgnoreCase);

	private static IReadOnlyList<string> ResolveHighFidelity(ObservedTest test, bool hasLine, List<string> failedVerdicts)
	{
		if (failedVerdicts.Count > 0)
			return failedVerdicts;

		if (!hasLine || IsGovernanceFailure(test.Output))
			return ["T1-09", "T1-17"];

		var signal = ClassifySignal(ResultShapeLine.Replace(test.Output, ""));
		return signal is null ? ["T1-09"] : [signal];
	}

	private static bool IsGovernanceFailure(string output)
	{
		var text = ResultShapeLine.Replace(output, "");
		return text.Contains("AssertGuide", StringComparison.Ordinal) ||
			   text.Contains("AssertGovernance", StringComparison.Ordinal) ||
			   text.Contains("compliance requirements", StringComparison.Ordinal) ||
			   text.Contains("does not certify", StringComparison.Ordinal) ||
			   text.Contains("consumer-owned Collector", StringComparison.Ordinal);
	}

	private static string? ClassifySignal(string text)
	{
		var spans = text.IndexOf("spans=", StringComparison.Ordinal);
		if (spans >= 0)
			text = text[..spans];

		if (text.Contains("user.claim", StringComparison.Ordinal) || text.Contains("user.id", StringComparison.Ordinal))
			return "T1-07";

		if (text.Contains("db.system.name", StringComparison.Ordinal) || text.Contains("SqlClient", StringComparison.Ordinal))
			return "T1-08";

		if (text.Contains("http.request.method", StringComparison.Ordinal) ||
			text.Contains("http.response.status_code", StringComparison.Ordinal) ||
			text.Contains("Microsoft.AspNetCore", StringComparison.Ordinal))
			return "T1-06";

		if (text.Contains("Company creation completed", StringComparison.Ordinal) ||
			text.Contains("{OriginalFormat}", StringComparison.Ordinal))
			return "T1-17";

		if (text.Contains("company.successful_creations", StringComparison.Ordinal))
			return "T1-10";

		return null;
	}

	private static bool TryFaultCell(string source, out string shape, out string protocol)
	{
		shape = "";
		protocol = "";
		foreach (var (profile, profileShape) in FaultProfiles)
		{
			if (!source.Contains(profile, StringComparison.Ordinal))
				continue;

			shape = profileShape;
			break;
		}

		if (shape.Length == 0)
			return false;

		foreach (Match match in WordToken.Matches(source))
		{
			var token = match.Groups["token"].Value;
			if (!FaultProtocols.Contains(token, StringComparer.Ordinal))
				continue;

			protocol = token;
			return true;
		}

		return false;
	}

	private static bool TrySamplingCell(string source, out string shape, out string scenario)
	{
		shape = "";
		scenario = "";
		foreach (var (flow, flowShape) in SamplingFlows)
		{
			if (!source.Contains(flow, StringComparison.Ordinal))
				continue;

			shape = flowShape;
			break;
		}

		if (shape.Length == 0)
			return false;

		foreach (Match match in WordToken.Matches(source))
		{
			var token = match.Groups["token"].Value;
			if (!SamplingScenarios.Contains(token, StringComparer.Ordinal))
				continue;

			scenario = token;
			return true;
		}

		return false;
	}

	private static string MapShape(string source)
	{
		foreach (var (enumName, shape) in ShapeNames)
		{
			if (source.Contains(enumName, StringComparison.Ordinal))
				return shape;
		}

		foreach (var shape in RegistryShapes)
		{
			if (source.Contains(shape, StringComparison.Ordinal))
				return shape;
		}

		return "";
	}

	private static void AddSecretFailures(IReadOnlyList<string> files, List<Tier1Failure> failures)
	{
		// Configuration files only. ProductionOverrideConfiguration keeps its header in process memory.
		var found = new HashSet<string>(StringComparer.Ordinal);
		foreach (var file in files)
		{
			if (!File.Exists(file))
				continue;

			var text = File.ReadAllText(file);
			if (text.Contains("OTEL_EXPORTER_OTLP_HEADERS", StringComparison.OrdinalIgnoreCase))
				found.Add("OTEL_EXPORTER_OTLP_HEADERS");

			if (HasAuthorizationCredential(text))
				found.Add("authorization");

			if (ContainsAny(text, ApplicationInsightsTokens))
				found.Add("ApplicationInsights");

			if (ContainsAny(text, AuditTokens))
				found.Add("audit");
		}

		foreach (var name in found)
			Add(failures, new("secrets", null, null, name));
	}

	private static bool HasAuthorizationCredential(string text) =>
		Regex.IsMatch(text, "\"authorization\"\\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
		Regex.IsMatch(text, "authorization\\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static bool ContainsAny(string text, IEnumerable<string> tokens) =>
		tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

	private static IReadOnlyList<string> DiscoverConfiguration(string productRoot, IReadOnlyList<Tier1Shape>? shapes)
	{
		var files = new List<string>();
		foreach (var host in new[] { "Api", "Worker", "Common.ApiGateway" })
		{
			var directory = Path.Combine(productRoot,
										 "src",
										 "Content",
										 "Backend",
										 "Solution",
										 "Monaco.Template.Backend." + host);
			AddAppSettings(files, directory);
		}

		if (shapes is null)
			return files;

		foreach (var shape in shapes)
			AddAppSettings(files, shape.OutputDirectory);

		return files;
	}

	private static void AddAppSettings(List<string> files, string directory)
	{
		if (!Directory.Exists(directory))
			return;

		foreach (var file in Directory.EnumerateFiles(directory, "appsettings*.json", SearchOption.AllDirectories))
		{
			if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
				file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
				continue;

			files.Add(file);
		}
	}

	private static List<ObservedTest> ReadTrx(string resultsDirectory)
	{
		var tests = new List<ObservedTest>();
		if (!Directory.Exists(resultsDirectory))
			return tests;

		foreach (var file in Directory.EnumerateFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories))
		{
			var document = XDocument.Load(file);
			var definitions = document.Descendants()
									  .Where(element => element.Name.LocalName == "UnitTest")
									  .Select(element =>
											  {
												  var method = element.Elements().FirstOrDefault(child => child.Name.LocalName == "TestMethod");
												  var className = method?.Attribute("className")?.Value ?? "";
												  var comma = className.IndexOf(',');
												  if (comma >= 0)
													  className = className[..comma];

												  return new
														 {
															 Id = element.Attribute("id")?.Value ?? "",
															 ClassName = className,
															 MethodName = method?.Attribute("name")?.Value ?? ""
														 };
											  })
									  .ToDictionary(item => item.Id, StringComparer.Ordinal);

			foreach (var result in document.Descendants().Where(element => element.Name.LocalName == "UnitTestResult"))
			{
				var id = result.Attribute("testId")?.Value ?? "";
				definitions.TryGetValue(id, out var definition);
				var stdout = result.Descendants().FirstOrDefault(element => element.Name.LocalName == "StdOut")?.Value ?? "";
				var message = result.Descendants().FirstOrDefault(element => element.Name.LocalName == "Message")?.Value ?? "";
				var stack = result.Descendants().FirstOrDefault(element => element.Name.LocalName == "StackTrace")?.Value ?? "";
				tests.Add(new(definition?.ClassName ?? "",
							  definition?.MethodName ?? "",
							  result.Attribute("testName")?.Value ?? "",
							  result.Attribute("outcome")?.Value ?? "",
							  stdout + "\n" + message + "\n" + stack));
			}
		}

		return tests;
	}

	private static async Task RunStepAsync(List<Tier1Failure> failures,
										   Func<Task> step,
										   Func<Exception, IEnumerable<Tier1Failure>> onError)
	{
		try
		{
			await step();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			AddRange(failures, onError(ex));
		}
	}

	private static IEnumerable<Tier1Failure> SolutionBlanket(string detail)
	{
		foreach (var failure in Blanket(SolutionProtocols, detail))
			yield return failure;

		yield return new("T1-13", null, null, detail);
	}

	private static IEnumerable<Tier1Failure> Blanket(IReadOnlyList<string> protocols, string detail)
	{
		foreach (var protocol in protocols)
			yield return new(protocol, null, null, detail);
	}

	private static void AddRange(List<Tier1Failure> failures, IEnumerable<Tier1Failure> additions)
	{
		foreach (var failure in additions)
			Add(failures, failure);
	}

	private static void Add(List<Tier1Failure> failures, Tier1Failure failure)
	{
		if (!failures.Contains(failure))
			failures.Add(failure);
	}

	private static string Trim(string text)
	{
		var compact = text.Replace("\r", "", StringComparison.Ordinal).Trim();
		return compact.Length <= 500 ? compact : compact[^500..];
	}
}

public sealed record Tier1Failure(string Protocol, string? Shape, string? Mode, string Detail);

public sealed record Tier1Result(int ExitCode, string Report, IReadOnlyList<Tier1Failure> Failures)
{
	public static Tier1Result From(IReadOnlyList<Tier1Failure> failures)
	{
		if (failures.Count == 0)
			return new(0, "Tier 1 passed.", failures);

		var report = string.Join(Environment.NewLine, failures.Select(Format));
		return new(1, report, failures);
	}

	private static string Format(Tier1Failure failure)
	{
		var line = "FAILED " + failure.Protocol;
		if (!string.IsNullOrEmpty(failure.Shape))
			line += " shape=" + failure.Shape;

		if (!string.IsNullOrEmpty(failure.Mode))
			line += " mode=" + failure.Mode;

		if (!string.IsNullOrEmpty(failure.Detail))
			line += " " + failure.Detail;

		return line;
	}
}

public sealed class Tier1Session
{
	public required string ProductRoot { get; init; }
	public required ITier1CommandRunner Runner { get; init; }
	public required Func<string, string, CancellationToken, Task<Tier1Provision>> Provision { get; init; }
	public required Func<CancellationToken, Task<bool>> DockerAvailable { get; init; }
	public Func<string, IReadOnlyList<Tier1Shape>, IReadOnlyList<string>>? ConfigurationFiles { get; init; }

	public static Tier1Session Live(string productRoot) =>
		new()
		{
			ProductRoot = productRoot,
			Runner = new LiveCommandRunner(),
			Provision = async (root, invocation, cancellationToken) =>
						{
							var harness = await ShapeHarness.ProvisionAsync(root, invocation, cancellationToken);
							return new HarnessProvision(harness);
						},
			DockerAvailable = ProbeDockerAsync
		};

	private static async Task<bool> ProbeDockerAsync(CancellationToken cancellationToken)
	{
		try
		{
			var result = await new LiveCommandRunner().RunAsync("docker", ["info"], Directory.GetCurrentDirectory(), cancellationToken);
			return result.ExitCode == 0;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return false;
		}
	}
}

public interface ITier1CommandRunner
{
	Task<Tier1CommandResult> RunAsync(string fileName,
									  IReadOnlyList<string> arguments,
									  string workingDirectory,
									  CancellationToken cancellationToken,
									  IReadOnlyDictionary<string, string>? environment = null);
}

public sealed record Tier1CommandResult(int ExitCode, string Output);

public sealed record Tier1Shape(string Id, string SolutionPath, string OutputDirectory, bool Tests);

public abstract class Tier1Provision : IAsyncDisposable
{
	public abstract IReadOnlyList<Tier1Shape> Shapes { get; }

	public virtual string? TempRoot => null;

	public virtual ValueTask DisposeAsync() =>
		ValueTask.CompletedTask;
}

public sealed class MemoryProvision : Tier1Provision
{
	public MemoryProvision(IReadOnlyList<Tier1Shape> shapes, string? tempRoot = null)
	{
		Shapes = shapes;
		TempRoot = tempRoot;
	}

	public override IReadOnlyList<Tier1Shape> Shapes { get; }

	public override string? TempRoot { get; }
}

internal sealed record ObservedTest(string ClassName, string MethodName, string TestName, string Outcome, string Output = "")
{
	public bool Passed =>
		string.Equals(Outcome, "Passed", StringComparison.OrdinalIgnoreCase);
}

internal readonly record struct TestCommandOutcome(bool Blanket, IReadOnlyList<ObservedTest> Results, string Detail);

internal sealed class HarnessProvision : Tier1Provision
{
	private readonly ShapeHarness _harness;

	public HarnessProvision(ShapeHarness harness) =>
		_harness = harness;

	public override IReadOnlyList<Tier1Shape> Shapes =>
		_harness.ProvisionedShapes
				.Select(shape => new Tier1Shape(shape.Id, shape.SolutionPath, shape.OutputDirectory, shape.Options.Tests))
				.ToArray();

	public override string? TempRoot => _harness.TempRoot;

	public override ValueTask DisposeAsync() =>
		_harness.DisposeAsync();
}

internal sealed class LiveCommandRunner : ITier1CommandRunner
{
	private const int CommandTimeoutMilliseconds = 7_200_000;

	public async Task<Tier1CommandResult> RunAsync(string fileName,
												   IReadOnlyList<string> arguments,
												   string workingDirectory,
												   CancellationToken cancellationToken,
												   IReadOnlyDictionary<string, string>? environment = null)
	{
		var startInfo = new ProcessStartInfo(fileName)
						{
							WorkingDirectory = workingDirectory,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true
						};
		foreach (var argument in arguments)
			startInfo.ArgumentList.Add(argument);

		startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
		startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
		startInfo.Environment["DOTNET_NOLOGO"] = "1";
		startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
		if (environment is not null)
		{
			foreach (var pair in environment)
				startInfo.Environment[pair.Key] = pair.Value;
		}

		var output = new StringBuilder();
		using var process = new Process { StartInfo = startInfo };
		process.OutputDataReceived += (_, eventArgs) => Append(output, eventArgs.Data);
		process.ErrorDataReceived += (_, eventArgs) => Append(output, eventArgs.Data);
		Console.WriteLine($"[tier1] {fileName} {string.Join(' ', arguments)}");
		if (!process.Start())
			throw new InvalidOperationException($"Failed to start '{fileName}'.");

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(CommandTimeoutMilliseconds);
		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			Kill(process);
			throw new TimeoutException($"'{fileName}' did not finish within {CommandTimeoutMilliseconds / 1000} seconds.");
		}
		catch (OperationCanceledException)
		{
			Kill(process);
			throw;
		}

		process.WaitForExit();
		return new(process.ExitCode, output.ToString());
	}

	private static void Append(StringBuilder output, string? line)
	{
		if (line is null)
			return;

		lock (output)
			output.AppendLine(line);

		Console.WriteLine(line);
	}

	private static void Kill(Process process)
	{
		try
		{
			if (process.HasExited)
				return;

			process.Kill(entireProcessTree: true);
		}
		catch (InvalidOperationException)
		{
		}
	}
}
