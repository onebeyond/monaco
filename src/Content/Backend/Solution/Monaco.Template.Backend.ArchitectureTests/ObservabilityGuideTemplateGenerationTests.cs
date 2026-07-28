using System.Diagnostics;

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

		output.AssertGuidePresent(expectStaticBoundary: false);
	}

	[Fact(DisplayName = "No-host output omits the guide, the Solution Item, and the manual instructions")]
	public void NoHostOutputOmitsTheGuideContract()
	{
		var output = _hive.Generate("GuideContractNoHost", "--apiService", "false", "--workerService", "false", "--apiGateway", "false");

		Assert.False(File.Exists(output.GuidePath), $"No-host output must not contain OBSERVABILITY.md: {output.GuidePath}");
		Assert.DoesNotContain("<File Path=\"OBSERVABILITY.md\" />", output.Solution, StringComparison.Ordinal);
		Assert.DoesNotContain(GuidePointer, output.Cli, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "E1 output keeps the guide and renders only the static non-turnkey boundary")]
	public void E1OutputKeepsTheGuideWithStaticBoundary()
	{
		var output = _hive.Generate("GuideContractE1", "--commonLibraries", "false");

		output.AssertGuidePresent(expectStaticBoundary: true);
	}

	[Fact(DisplayName = "Gateway-only output contains the guide, the Solution Item, and the manual instructions")]
	public void GatewayOnlyOutputContainsTheGuideContract()
	{
		var output = _hive.Generate("GuideContractGatewayOnly", "--apiService", "false", "--workerService", "false", "--apiGateway", "true");

		output.AssertGuidePresent(expectStaticBoundary: false);
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

		public void AssertGuidePresent(bool expectStaticBoundary)
		{
			Assert.True(File.Exists(GuidePath), $"Expected OBSERVABILITY.md in {OutputDirectory}.");
			var guide = File.ReadAllText(GuidePath);

			Assert.Contains("<File Path=\"OBSERVABILITY.md\" />", Solution, StringComparison.Ordinal);
			Assert.Contains(GuidePointer, Cli, StringComparison.Ordinal);
			Assert.DoesNotContain("<!--#if", guide, StringComparison.Ordinal);

			if (expectStaticBoundary)
				Assert.Contains("Static non-turnkey boundary", guide, StringComparison.Ordinal);
			else
				Assert.DoesNotContain("Static non-turnkey boundary", guide, StringComparison.Ordinal);
		}
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
								CreateNoWindow = true
							};
			startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
			startInfo.Environment["DOTNET_NOLOGO"] = "1";
			startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

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