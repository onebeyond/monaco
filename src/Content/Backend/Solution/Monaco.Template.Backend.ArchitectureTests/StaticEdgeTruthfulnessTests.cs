using System.Diagnostics;
using AwesomeAssertions;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class StaticEdgeTruthfulnessTests : IClassFixture<StaticEdgeTruthfulnessTests.StaticEdgeHiveFixture>
{
	private const string GuideShellHeading = "# Local observability";
	private const string StaticShapeAnchor = "<!-- OBSERVABILITY: 5.4 STATIC-SHAPE-BOUNDARIES -->";
	private const string GuidePostActionText = "Review OBSERVABILITY.md for the generated local-observability guidance.";
	private const string CoreInitFileName = "20260915223105_Init.cs";
	private const string CoreInitDesignerFileName = "20260915223105_Init.Designer.cs";
	private const string CoreSnapshotFileName = "AppDbContextModelSnapshot.cs";
	private const string FullInitFileName = "20260328183658_Init.cs";

	private readonly StaticEdgeHiveFixture _hive;

	public StaticEdgeTruthfulnessTests(StaticEdgeHiveFixture hive) =>
		_hive = hive;

	[Fact(DisplayName = "E1 keeps the external Observability package handoff and the guide shell")]
	public void E1_KeepsExternalObservabilityPackageHandoffAndTheGuideShell()
	{
		var shape = _hive.E1;

		shape.ProjectDirectoryExists("Common.Observability")
			 .Should()
			 .BeFalse(shape.Id);
		shape.Solution
			 .Should()
			 .NotContain("Common.Observability.csproj", shape.Id);
		shape.PackageVersions
			 .Should()
			 .Contain(id => id.Contains("Common.Observability", StringComparison.Ordinal), shape.Id);
		shape.PackageVersions
			 .Should()
			 .NotContain(id => id.StartsWith("OpenTelemetry.", StringComparison.Ordinal), shape.Id);

		foreach (var host in new[] { "Api", "Worker" })
		{
			var csproj = shape.ReadText(host, $"{shape.Name}.{host}.csproj");
			csproj.Should()
				  .Contain($"PackageReference Include=\"{shape.Name}.Common.Observability\"", shape.Id);
			csproj.Should()
				  .NotContain("Common.Observability.csproj", shape.Id);
			csproj.Should()
				  .NotContain("OpenTelemetry", shape.Id);
			shape.ReadText(host, "appsettings.json")
				 .Should()
				 .Contain("\"Observability\"", shape.Id);
			shape.ReadText(host, "appsettings.Development.json")
				 .Should()
				 .Contain("OTEL_EXPORTER_OTLP_ENDPOINT", shape.Id);
		}

		var apiProgram = shape.ReadText("Api", "Program.cs");
		CSharpCompositionSyntax.Invokes(apiProgram, "AddApiObservability")
							   .Should()
							   .BeTrue(shape.Id);
		CSharpCompositionSyntax.ContainsIdentifier(apiProgram, "BoundaryExceptionHandler")
							   .Should()
							   .BeTrue(shape.Id);
		CSharpCompositionSyntax.Invokes(apiProgram, "UseIdentityEnrichment")
							   .Should()
							   .BeTrue(shape.Id);
		var workerProgram = shape.ReadText("Worker", "Program.cs");
		CSharpCompositionSyntax.Invokes(workerProgram, "AddWorkerObservability")
							   .Should()
							   .BeTrue(shape.Id);
		CSharpCompositionSyntax.ContainsIdentifier(workerProgram, "BoundaryExceptionHandler")
							   .Should()
							   .BeFalse(shape.Id);
		CSharpCompositionSyntax.Invokes(workerProgram, "UseIdentityEnrichment")
							   .Should()
							   .BeFalse(shape.Id);

		var apiCsproj = shape.ReadText("Api", $"{shape.Name}.Api.csproj");
		apiCsproj.Should()
				 .Contain($"{shape.Name}.Common.Api\"", shape.Id);
		apiCsproj.Should()
				 .Contain($"PackageReference Include=\"{shape.Name}.Common.Api.Application\"", shape.Id);
		var applicationCsproj = shape.ReadText("Application", $"{shape.Name}.Application.csproj");
		applicationCsproj.Should()
						 .Contain($"PackageReference Include=\"{shape.Name}.Common.Application\"", shape.Id);
		applicationCsproj.Should()
						 .Contain($"PackageReference Include=\"{shape.Name}.Common.Infrastructure\"", shape.Id);
		applicationCsproj.Should()
						 .Contain($"PackageReference Include=\"{shape.Name}.Common.BlobStorage\"", shape.Id);
		AssertStampRegistrationPresent(shape);
		shape.PackageVersions
			 .Should()
			 .Contain($"{shape.Name}.Common.Api", shape.Id);
		shape.PackageVersions
			 .Should()
			 .Contain($"{shape.Name}.Common.Infrastructure", shape.Id);

		AssertGuideShell(shape, _hive.GenerationLog(shape));
	}

	[Fact(DisplayName = "E2 omits no-host observability claims and keeps Core Init")]
	public void E2_OmitsNoHostClaimsAndKeepsCoreInit()
	{
		var shape = _hive.E2;

		File.Exists(shape.GuidePath)
			.Should()
			.BeFalse(shape.Id);
		shape.Solution
			 .Should()
			 .NotContain("OBSERVABILITY.md", shape.Id);
		_hive.GenerationLog(shape)
			 .Should()
			 .NotContain(GuidePostActionText, shape.Id);

		shape.ProjectDirectoryExists("Api")
			 .Should()
			 .BeFalse(shape.Id);
		shape.ProjectDirectoryExists("Worker")
			 .Should()
			 .BeFalse(shape.Id);
		shape.ProjectDirectoryExists("Common.ApiGateway")
			 .Should()
			 .BeFalse(shape.Id);
		Directory.EnumerateFiles(shape.OutputDirectory, "launchSettings.json", SearchOption.AllDirectories)
				 .Should()
				 .BeEmpty(shape.Id);
		File.Exists(Path.Combine(shape.OutputDirectory, $"{shape.Name}.Api.http"))
			.Should()
			.BeFalse(shape.Id);

		var generated = GeneratedText(shape);
		generated.Should()
				 .NotContain("Common.Observability", shape.Id);
		generated.Should()
				 .NotContain("AddApiObservability", shape.Id);
		generated.Should()
				 .NotContain("AddWorkerObservability", shape.Id);
		generated.Should()
				 .NotContain("AddGatewayObservability", shape.Id);
		generated.Should()
				 .NotContain("OTEL_", shape.Id);
		generated.Should()
				 .NotContain("OpenTelemetry", shape.Id);
		generated.Should()
				 .NotContain("\"Observability\"", shape.Id);

		AssertStampRegistrationAbsent(shape);
		shape.ProjectDirectoryExists("Application")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Domain")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Common.Infrastructure")
			 .Should()
			 .BeTrue(shape.Id);
		shape.FileExists("Application", "Persistence", "Migrations", CoreInitFileName)
			 .Should()
			 .BeTrue(shape.Id);
		shape.FileExists("Application", "Persistence", "Migrations", CoreInitDesignerFileName)
			 .Should()
			 .BeTrue(shape.Id);
		shape.FileExists("Application", "Persistence", "Migrations", CoreSnapshotFileName)
			 .Should()
			 .BeTrue(shape.Id);
		shape.FileExists("Application", "Persistence", "Migrations", FullInitFileName)
			 .Should()
			 .BeFalse(shape.Id);
	}

	[Fact(DisplayName = "E3 keeps the guide shell without stamp registration")]
	public void E3_KeepsGuideShellWithoutStampRegistration()
	{
		var shape = _hive.E3;

		AssertGuideShell(shape, _hive.GenerationLog(shape));
		AssertStampRegistrationAbsent(shape);
		shape.ProjectDirectoryExists("Api")
			 .Should()
			 .BeFalse(shape.Id);
		shape.ProjectDirectoryExists("Worker")
			 .Should()
			 .BeFalse(shape.Id);
	}

	[Fact(DisplayName = "E4 keeps Worker observability without the API")]
	public void E4_KeepsWorkerObservabilityWithoutTheApi()
	{
		var shape = _hive.E4;

		AssertGuideShell(shape, _hive.GenerationLog(shape));
		AssertStampRegistrationPresent(shape);
		shape.ProjectDirectoryExists("Api")
			 .Should()
			 .BeFalse(shape.Id);
		shape.ProjectDirectoryExists("Worker")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Common.Observability")
			 .Should()
			 .BeTrue(shape.Id);
		shape.Solution
			 .Should()
			 .Contain("Common.Observability", shape.Id);
		shape.Solution
			 .Should()
			 .NotContain($"{shape.Name}.Api/", shape.Id);

		var workerProgram = shape.ReadText("Worker", "Program.cs");
		CSharpCompositionSyntax.Invokes(workerProgram, "AddWorkerObservability")
							   .Should()
							   .BeTrue(shape.Id);
		shape.ReadText("Worker", "appsettings.json")
			 .Should()
			 .Contain("\"Observability\"", shape.Id);
		shape.ReadText("Worker", "appsettings.Development.json")
			 .Should()
			 .Contain("OTEL_EXPORTER_OTLP_ENDPOINT", shape.Id);
		var workerCsproj = shape.ReadText("Worker", $"{shape.Name}.Worker.csproj");
		workerCsproj.Should()
					.Contain("Common.Observability.csproj", shape.Id);
		workerCsproj.Should()
					.NotContain($"PackageReference Include=\"{shape.Name}.Common.Observability\"", shape.Id);
		shape.FileExists("ArchitectureTests", "StaticEdgeTruthfulnessTests.cs")
			 .Should()
			 .BeFalse(shape.Id);
	}

	private static void AssertGuideShell(SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape shape, string generationLog)
	{
		File.Exists(shape.GuidePath)
			.Should()
			.BeTrue(shape.Id);
		shape.Solution
			 .Should()
			 .Contain("<File Path=\"OBSERVABILITY.md\" />", shape.Id);
		generationLog.Should()
					 .Contain(GuidePostActionText, shape.Id);
		shape.Guide
			 .Should()
			 .Contain(GuideShellHeading, shape.Id);
		shape.Guide
			 .Should()
			 .Contain(StaticShapeAnchor, shape.Id);
	}

	private static void AssertStampRegistrationAbsent(SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape shape)
	{
		var applicationDi = shape.ReadText("Application", "DependencyInjection", "ServiceCollectionExtensions.cs");
		CSharpCompositionSyntax.CountGenericInvocations(applicationDi, "AddScoped", "AuditableSaveChangesInterceptor")
							   .Should()
							   .Be(0, shape.Id);
		CSharpCompositionSyntax.Invokes(applicationDi, "AddInterceptors")
							   .Should()
							   .BeFalse(shape.Id);
		CSharpCompositionSyntax.ContainsIdentifier(applicationDi, "AuditableSaveChangesInterceptor")
							   .Should()
							   .BeFalse(shape.Id);
		CSharpCompositionSyntax.ContainsIdentifier(applicationDi, "PersistenceActorFallbackDiagnostics")
							   .Should()
							   .BeFalse(shape.Id);
		CSharpCompositionSyntax.Invokes(applicationDi, "AddDbContext")
							   .Should()
							   .BeTrue(shape.Id);
	}

	private static void AssertStampRegistrationPresent(SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape shape)
	{
		var applicationDi = shape.ReadText("Application", "DependencyInjection", "ServiceCollectionExtensions.cs");
		CSharpCompositionSyntax.CountGenericInvocations(applicationDi, "AddScoped", "AuditableSaveChangesInterceptor")
							   .Should()
							   .Be(1, shape.Id);
		CSharpCompositionSyntax.Invokes(applicationDi, "AddInterceptors")
							   .Should()
							   .BeTrue(shape.Id);
		CSharpCompositionSyntax.CountGenericInvocations(applicationDi, "AddSingleton", "PersistenceActorFallbackDiagnostics")
							   .Should()
							   .Be(1, shape.Id);
	}

	private static string GeneratedText(SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape shape)
	{
		string[] extensions = [".cs", ".csproj", ".json", ".props", ".targets", ".slnx", ".md", ".http", ".config", ".xml"];
		return string.Concat(Directory.EnumerateFiles(shape.OutputDirectory, "*", SearchOption.AllDirectories)
									  .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
													 !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
													 extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
									  .Select(File.ReadAllText));
	}

	public sealed class StaticEdgeHiveFixture : IDisposable
	{
		private const int CommandTimeoutMilliseconds = 180_000;

		private readonly string _root = Path.Combine(Path.GetTempPath(), $"monaco-static-edge-tests-{Guid.NewGuid():N}");
		private readonly Dictionary<string, string> _logs = new(StringComparer.Ordinal);
		private readonly string _hivePath;

		public StaticEdgeHiveFixture()
		{
			_hivePath = Path.Combine(_root, "hive");
			try
			{
				Directory.CreateDirectory(_hivePath);
				RunDotnet($"new install \"{FindTemplateSourceDirectory()}\" --debug:custom-hive \"{_hivePath}\"");

				E1 = Generate("E1",
							  new SupportedRuntimeShapeCompositionTests.ShapeOptions(true, true, false, true, true, true, false, false, false),
							  "--tests",
							  "false",
							  "--commonLibraries",
							  "false",
							  "--keycloakConfig",
							  "false");
				E2 = Generate("E2",
							  new SupportedRuntimeShapeCompositionTests.ShapeOptions(false, false, false, false, false, false, false, true, false),
							  "--apiService",
							  "false",
							  "--workerService",
							  "false",
							  "--auth",
							  "false",
							  "--massTransitIntegration",
							  "false",
							  "--filesSupport",
							  "false",
							  "--tests",
							  "false",
							  "--keycloakConfig",
							  "false");
				E3 = Generate("E3",
							  new SupportedRuntimeShapeCompositionTests.ShapeOptions(false, false, true, false, false, false, false, true, false),
							  "--apiService",
							  "false",
							  "--workerService",
							  "false",
							  "--apiGateway",
							  "true",
							  "--auth",
							  "false",
							  "--massTransitIntegration",
							  "false",
							  "--filesSupport",
							  "false",
							  "--tests",
							  "false",
							  "--keycloakConfig",
							  "false");
				E4 = Generate("E4",
							  new SupportedRuntimeShapeCompositionTests.ShapeOptions(false, true, false, false, true, true, true, true, false),
							  "--apiService",
							  "false",
							  "--auth",
							  "false",
							  "--keycloakConfig",
							  "false");
			}
			catch
			{
				DeleteRoot();
				throw;
			}
		}

		public SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape E1 { get; }
		public SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape E2 { get; }
		public SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape E3 { get; }
		public SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape E4 { get; }

		public string GenerationLog(SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape shape) =>
			_logs[shape.Id];

		public void Dispose() =>
			DeleteRoot();

		private void DeleteRoot()
		{
			try
			{
				Directory.Delete(_root, recursive: true);
			}
			catch (IOException)
			{ }
			catch (UnauthorizedAccessException)
			{ }
		}

		private SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape Generate(string id,
																					 SupportedRuntimeShapeCompositionTests.ShapeOptions options,
																					 params string[] parameters)
		{
			var name = $"Edge{id}";
			var outputDirectory = Path.Combine(_root, name);
			var arguments = $"new monaco-backend-solution --name \"{name}\" --output \"{outputDirectory}\" --debug:custom-hive \"{_hivePath}\"";
			if (parameters.Length > 0)
				arguments += $" {string.Join(" ", parameters)}";

			_logs[id] = RunDotnet(arguments);
			return new SupportedRuntimeShapeCompositionTests.GeneratedRuntimeShape(id, name, outputDirectory, options);
		}

		private static string RunDotnet(string arguments)
		{
			var startInfo = new ProcessStartInfo("dotnet", arguments)
							{
								RedirectStandardOutput = true,
								RedirectStandardError = true,
								UseShellExecute = false,
								CreateNoWindow = true,
								Environment =
								{
									["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
									["DOTNET_NOLOGO"] = "1",
									["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
								}
							};

			using var process = Process.Start(startInfo)!;
			var standardOutput = process.StandardOutput.ReadToEndAsync();
			var standardError = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(CommandTimeoutMilliseconds))
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch (InvalidOperationException)
				{ }

				throw new TimeoutException($"'dotnet {arguments}' did not finish within {CommandTimeoutMilliseconds / 1000} seconds.");
			}

			var combined = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
			Assert.True(process.ExitCode == 0,
						$"'dotnet {arguments}' failed with exit code {process.ExitCode}:{Environment.NewLine}{combined}");
			return combined;
		}

		private static string FindTemplateSourceDirectory()
		{
			for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
				if (File.Exists(Path.Combine(directory.FullName, ".template.config", "template.json")))
					return directory.FullName;

			throw new DirectoryNotFoundException("Could not locate the template source directory containing .template.config/template.json.");
		}
	}
}
