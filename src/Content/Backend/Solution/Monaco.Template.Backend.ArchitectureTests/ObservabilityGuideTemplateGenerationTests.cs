using System.Diagnostics;
using AwesomeAssertions;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class ObservabilityGuideTemplateGenerationTests : IClassFixture<ObservabilityGuideTemplateGenerationTests.TemplateHiveFixture>
{
	private const string GuidePointer = "Review OBSERVABILITY.md for the generated local-observability guidance.";

	private readonly TemplateHiveFixture _hive;

	public ObservabilityGuideTemplateGenerationTests(TemplateHiveFixture hive) => _hive = hive;

	[Fact(DisplayName = "Default Runtime Host output contains the guide, the Solution Item, and the manual instructions")]
	public void DefaultHostOutputContainsTheGuideContract()
	{
		var output = _hive.Generate("GuideContractDefaultHost");

		output.AssertGuidePresent();
		output.AssertSuccessfulCompanyCounterPresent();
		output.AssertApplicationDiagnosticsPresent();
		output.AssertHttpExceptionBoundaryGuidePresent();
		output.AssertProductionRoutingGuidePresent();
		output.AssertCanonicalLocalFirstRunTask("GuideContractDefaultHost.Api", "GuideContractDefaultHost.Worker");
		output.AssertGuideDoesNotContain("GuideContractDefaultHost.Common.ApiGateway");
	}

	[Fact(DisplayName = "No-host output omits the guide, the Solution Item, and the manual instructions")]
	public void NoHostOutputOmitsTheGuideContract()
	{
		var output = _hive.Generate("GuideContractNoHost", "--apiService", "false", "--workerService", "false", "--apiGateway", "false");

		Assert.False(File.Exists(output.GuidePath), $"No-host output must not contain OBSERVABILITY.md: {output.GuidePath}");
		Assert.DoesNotContain("<File Path=\"OBSERVABILITY.md\" />", output.Solution, StringComparison.Ordinal);
		Assert.DoesNotContain(GuidePointer, output.Cli, StringComparison.Ordinal);
		output.AssertApplicationDiagnosticsPresent();
	}

	[Fact(DisplayName = "Package delivery output keeps the complete observability guide")]
	public void PackageDeliveryOutputKeepsTheGuideContract()
	{
		var output = _hive.Generate("GuideContractPackages", "--commonLibraries", "false");

		output.AssertGuidePresent();
		output.AssertHttpExceptionBoundaryGuidePresent();
		output.AssertProductionRoutingGuidePresent();
	}

	[Fact(DisplayName = "Gateway-only output contains the guide, the Solution Item, and the manual instructions")]
	public void GatewayOnlyOutputContainsTheGuideContract()
	{
		var output = _hive.Generate("GuideContractGatewayOnly", "--apiService", "false", "--workerService", "false", "--apiGateway", "true");

		output.AssertGuidePresent();
		output.AssertSuccessfulCompanyCounterAbsent();
		Assert.DoesNotContain("IntegrationTests", output.Solution, StringComparison.Ordinal);
		output.AssertApplicationDiagnosticsPresent();
		output.AssertHttpExceptionBoundaryGuidePresent();
		output.AssertProductionRoutingGuidePresent();
	}

	[Fact(DisplayName = "Worker-only output omits the successful-company counter guidance")]
	public void WorkerOnlyOutputOmitsSuccessfulCompanyCounterGuidance()
	{
		var output = _hive.Generate("GuideContractWorkerOnly", "--apiService", "false", "--workerService", "true", "--apiGateway", "false");

		output.AssertGuidePresent();
		output.AssertSuccessfulCompanyCounterAbsent();
		output.AssertApplicationDiagnosticsPresent();
		output.AssertHttpExceptionBoundaryGuideAbsent();
		output.AssertProductionRoutingGuidePresent();
	}

	[Fact(DisplayName = "Full output with Gateway contains the local observability walkthrough")]
	public void FullOutputWithGatewayContainsCanonicalLocalFirstRunTask()
	{
		var output = _hive.Generate("GuideContractFullGateway", "--apiGateway", "true");

		output.AssertCanonicalLocalFirstRunTask("GuideContractFullGateway.Api", "GuideContractFullGateway.Worker", "GuideContractFullGateway.Common.ApiGateway");
	}

	[Fact(DisplayName = "Full output without tests contains the local observability walkthrough")]
	public void FullOutputWithoutTestsContainsCanonicalLocalFirstRunTask()
	{
		var output = _hive.Generate("GuideContractFullWithoutTests", "--tests", "false");

		output.AssertCanonicalLocalFirstRunTask("GuideContractFullWithoutTests.Api", "GuideContractFullWithoutTests.Worker");
		output.AssertGuideDoesNotContain("GuideContractFullWithoutTests.Common.ApiGateway");
	}

	[Fact(DisplayName = "Worker-only output states the local walkthrough boundary")]
	public void WorkerOnlyOutputStatesLocalWalkthroughBoundary()
	{
		var r5Output = _hive.Generate("GuideContractWorkerR5",
			"--apiService", "false",
			"--workerService", "true",
			"--apiGateway", "false",
			"--massTransitIntegration", "true",
			"--filesSupport", "true",
			"--tests", "false");
		var r6Output = _hive.Generate("GuideContractWorkerR6",
			"--apiService", "false",
			"--workerService", "true",
			"--apiGateway", "false",
			"--massTransitIntegration", "false",
			"--filesSupport", "false",
			"--tests", "false");

		r5Output.AssertWorkerOnlyFirstRunBoundary();
		r6Output.AssertWorkerOnlyFirstRunBoundary();
	}

	public sealed class GeneratedShape
	{
		public GeneratedShape(string name, string outputDirectory, string cli)
		{
			OutputDirectory = outputDirectory;
			Cli = cli;
			GuidePath = Path.Combine(outputDirectory, "OBSERVABILITY.md");
			Solution = File.ReadAllText(Path.Combine(outputDirectory, $"{name}.slnx"));
		}

		public string OutputDirectory { get; }

		public string Cli { get; }

		public string GuidePath { get; }

		public string Solution { get; }

		public string ApplicationDiagnosticsPath => Path.Combine(OutputDirectory,
																			 $"{Path.GetFileName(OutputDirectory)}.Application",
																			 "Diagnostics",
																			 "ApplicationDiagnostics.cs");

		public void AssertGuidePresent()
		{
			Assert.True(File.Exists(GuidePath), $"Expected OBSERVABILITY.md in {OutputDirectory}.");
			var guide = File.ReadAllText(GuidePath);

			Assert.Contains("<File Path=\"OBSERVABILITY.md\" />", Solution, StringComparison.Ordinal);
			Assert.Contains(GuidePointer, Cli, StringComparison.Ordinal);
			Assert.DoesNotContain("<!--#if", guide, StringComparison.Ordinal);
			Assert.DoesNotContain("Static non-turnkey boundary", guide, StringComparison.Ordinal);
		}

		public void AssertSuccessfulCompanyCounterPresent()
		{
			var guide = File.ReadAllText(GuidePath);

			Assert.Contains("## Successful company creations", guide, StringComparison.Ordinal);
			Assert.Contains("company.successful_creations", guide, StringComparison.Ordinal);
		}

		public void AssertSuccessfulCompanyCounterAbsent()
		{
			var guide = File.ReadAllText(GuidePath);

			Assert.DoesNotContain("## Successful company creations", guide, StringComparison.Ordinal);
			Assert.DoesNotContain("company.successful_creations", guide, StringComparison.Ordinal);
		}

		public void AssertHttpExceptionBoundaryGuidePresent()
		{
			var guide = File.ReadAllText(GuidePath);

			guide.Should().Contain("## HTTP exception boundaries");
			guide.Should().Contain("exactly one authoritative Operational Log");
			guide.Should().Contain("opaque third-party exception prose");
		}

		public void AssertHttpExceptionBoundaryGuideAbsent() =>
			File.ReadAllText(GuidePath).Should().NotContain("## HTTP exception boundaries");

		public void AssertProductionRoutingGuidePresent()
		{
			var guide = File.ReadAllText(GuidePath);

			guide.Should().Contain("## Production signal routing");
			guide.Should().Contain("configuration-only");
			guide.Should().Contain("OpenTelemetry Collector or compatible OTLP receiver");
			guide.Should().Contain("wins over its common counterpart");
			guide.Should().Contain("does not deploy or configure a production Collector");
			guide.Should().Contain("encrypted, authenticated transport or a secured local Collector hop");
			guide.Should().NotContain("OBSERVABILITY: 2.4 PRODUCTION-ROUTING");
		}

		public void AssertCanonicalLocalFirstRunTask(params string[] expectedServiceNames)
		{
			var guide = File.ReadAllText(GuidePath);
			var taskStart = guide.IndexOf("## Create a Company and inspect local observability", StringComparison.Ordinal);
			Assert.True(taskStart >= 0, "Expected the canonical local first-run task in the generated guide.");
			var nextSection = guide.IndexOf("\n## ", taskStart + 1, StringComparison.Ordinal);
			var task = guide[taskStart..(nextSection >= 0 ? nextSection : guide.Length)];

			Assert.Contains("prerequisites selected for this solution", task, StringComparison.Ordinal);
			Assert.Contains("generated `Init` migration", task, StringComparison.Ordinal);
			Assert.Contains("participating Runtime Host", task, StringComparison.Ordinal);
			Assert.Contains("Authenticate in Scalar", task, StringComparison.Ordinal);
			Assert.Contains("create a Company", task, StringComparison.Ordinal);
			Assert.Contains("Dashboard's Logs", task, StringComparison.Ordinal);
			Assert.Contains("correlated Trace", task, StringComparison.Ordinal);
			Assert.Contains("applicable automatic Metric", task, StringComparison.Ordinal);
			Assert.Contains("company.successful_creations", task, StringComparison.Ordinal);
			Assert.Contains("exactly one", task, StringComparison.Ordinal);
			Assert.Contains("no attributes", task, StringComparison.Ordinal);
			Assert.Contains("validation or persistence failure", task, StringComparison.Ordinal);
			foreach (var serviceName in expectedServiceNames)
				Assert.Contains(serviceName, task, StringComparison.Ordinal);
		}

		public void AssertWorkerOnlyFirstRunBoundary()
		{
			var guide = File.ReadAllText(GuidePath);

			Assert.Contains("## Worker-only solutions", guide, StringComparison.Ordinal);
			Assert.Contains("no interactive API/Scalar Company walkthrough", guide, StringComparison.Ordinal);
			Assert.Contains("no generated sample fixture or walkthrough task", guide, StringComparison.Ordinal);
			Assert.DoesNotContain("LOCAL-FULL-UJ1", guide, StringComparison.Ordinal);
			Assert.DoesNotContain("REPO-R", guide, StringComparison.Ordinal);
			Assert.DoesNotContain("## Successful company creations", guide, StringComparison.Ordinal);
			Assert.DoesNotContain("company.successful_creations", guide, StringComparison.Ordinal);
		}

		public void AssertGuideDoesNotContain(string text) =>
			Assert.DoesNotContain(text, File.ReadAllText(GuidePath), StringComparison.Ordinal);

		public void AssertApplicationDiagnosticsPresent() =>
			Assert.True(File.Exists(ApplicationDiagnosticsPath), $"Expected ApplicationDiagnostics.cs in {ApplicationDiagnosticsPath}.");
	}

	public sealed class TemplateHiveFixture : IDisposable
	{
		private const int CommandTimeoutMilliseconds = 180_000;

		private readonly string _root = Path.Combine(Path.GetTempPath(), $"monaco-guide-template-tests-{Guid.NewGuid():N}");
		private readonly string _hivePath;

		public TemplateHiveFixture()
		{
			_hivePath = Path.Combine(_root, "hive");
			Directory.CreateDirectory(_hivePath);
			RunDotnet($"new install \"{FindTemplateSourceDirectory()}\" --debug:custom-hive \"{_hivePath}\"");
		}

		public GeneratedShape Generate(string name, params string[] parameters)
		{
			var outputDirectory = Path.Combine(_root, name);
			var arguments = $"new monaco-backend-solution --name \"{name}\" --output \"{outputDirectory}\" --debug:custom-hive \"{_hivePath}\"";
			if (parameters.Length > 0)
				arguments += $" {string.Join(" ", parameters)}";

			return new GeneratedShape(name, outputDirectory, RunDotnet(arguments));
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(_root, recursive: true);
			}
			catch (IOException)
			{
				// Best effort: locked files from the CLI are cleaned up with the temp directory later.
			}
			catch (UnauthorizedAccessException)
			{
				// Best effort: locked files from the CLI are cleaned up with the temp directory later.
			}
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
				process.Kill(entireProcessTree: true);
				throw new TimeoutException($"'dotnet {arguments}' did not finish within {CommandTimeoutMilliseconds / 1000} seconds.");
			}

			var combined = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
			Assert.True(process.ExitCode == 0, $"'dotnet {arguments}' failed with exit code {process.ExitCode}:{Environment.NewLine}{combined}");
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