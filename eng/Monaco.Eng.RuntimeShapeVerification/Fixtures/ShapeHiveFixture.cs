using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification.Fixtures;

[ExcludeFromCodeCoverage]
public sealed class ShapeHiveFixture : IAsyncLifetime
{
	private const int CommandTimeoutMilliseconds = 600_000;

	private readonly string _root = Path.Combine(Path.GetTempPath(), $"monaco-runtime-shape-verify-{Guid.NewGuid():N}");
	private readonly string _hivePath;

	public ShapeHiveFixture()
	{
		_hivePath = Path.Combine(_root, "hive");
		Directory.CreateDirectory(_hivePath);
		RunDotnet($"new install \"{FindTemplateSourceDirectory()}\" --debug:custom-hive \"{_hivePath}\"");

		FullApiWorker = Generate(RuntimeShape.FullApiWorker,
								 new(true,
									 true,
									 false,
									 true,
									 true,
									 true,
									 true,
									 true,
									 true));
		GatewayFull = Generate(RuntimeShape.GatewayFull,
							   new(true,
								   true,
								   true,
								   true,
								   true,
								   true,
								   true,
								   true,
								   true),
							   "--apiGateway",
							   "true");
		LeanApi = Generate(RuntimeShape.LeanApi,
						   new(true,
							   false,
							   false,
							   false,
							   false,
							   false,
							   true,
							   true,
							   false),
						   "--workerService",
						   "false",
						   "--auth",
						   "false",
						   "--massTransitIntegration",
						   "false",
						   "--filesSupport",
						   "false",
						   "--keycloakConfig",
						   "false");
		PublisherApi = Generate(RuntimeShape.PublisherApi,
								new(true,
									false,
									false,
									false,
									true,
									true,
									true,
									true,
									false),
								"--workerService",
								"false",
								"--auth",
								"false",
								"--keycloakConfig",
								"false");
		WorkerConsumer = Generate(RuntimeShape.WorkerConsumer,
								  new(false,
									  true,
									  false,
									  false,
									  true,
									  true,
									  false,
									  true,
									  false),
								  "--apiService",
								  "false",
								  "--auth",
								  "false",
								  "--tests",
								  "false",
								  "--keycloakConfig",
								  "false");
		WorkerBaseline = Generate(RuntimeShape.WorkerBaseline,
								  new(false,
									  true,
									  false,
									  false,
									  false,
									  false,
									  false,
									  true,
									  false),
								  "--apiService",
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
		FullApiWorkerWithoutTests = Generate(RuntimeShape.FullApiWorkerWithoutTests,
											 new(true,
												 true,
												 false,
												 true,
												 true,
												 true,
												 false,
												 true,
												 true),
											 "--tests",
											 "false");
	}

	public GeneratedShape FullApiWorker { get; }
	public GeneratedShape GatewayFull { get; }
	public GeneratedShape LeanApi { get; }
	public GeneratedShape PublisherApi { get; }
	public GeneratedShape WorkerConsumer { get; }
	public GeneratedShape WorkerBaseline { get; }
	public GeneratedShape FullApiWorkerWithoutTests { get; }

	public GeneratedShape Get(RuntimeShape shape) =>
		shape switch
		{
			RuntimeShape.FullApiWorker => FullApiWorker,
			RuntimeShape.GatewayFull => GatewayFull,
			RuntimeShape.LeanApi => LeanApi,
			RuntimeShape.PublisherApi => PublisherApi,
			RuntimeShape.WorkerConsumer => WorkerConsumer,
			RuntimeShape.WorkerBaseline => WorkerBaseline,
			RuntimeShape.FullApiWorkerWithoutTests => FullApiWorkerWithoutTests,
			_ => throw new ArgumentOutOfRangeException(nameof(shape),
													   shape,
													   "Unknown runtime shape.")
		};

	public Task InitializeAsync()
	{
		foreach (var shape in new[]
							  {
								  FullApiWorker,
								  GatewayFull,
								  LeanApi,
								  PublisherApi,
								  WorkerConsumer,
								  WorkerBaseline,
								  FullApiWorkerWithoutTests
							  })
		{
			Console.WriteLine($"[RuntimeShapeVerification] Building {shape.Id} ({shape.Name})...");
			RunDotnet($"build \"{shape.SolutionPath}\" -c Debug --nologo -v q");
			Console.WriteLine($"[RuntimeShapeVerification] Built {shape.Id}.");
		}

		return Task.CompletedTask;
	}

	public Task DisposeAsync()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}

		return Task.CompletedTask;
	}

	private GeneratedShape Generate(RuntimeShape shape,
									ShapeOptions options,
									params string[] parameters)
	{
		var name = shape.ToString();
		var outputDirectory = Path.Combine(_root, name);
		Console.WriteLine($"[RuntimeShapeVerification] Generating {name} -> {outputDirectory}");
		var arguments =
			$"new monaco-backend-solution --name \"{name}\" --output \"{outputDirectory}\" --debug:custom-hive \"{_hivePath}\"";
		if (parameters.Length > 0)
			arguments += $" {string.Join(" ", parameters)}";

		RunDotnet(arguments);
		Console.WriteLine($"[RuntimeShapeVerification] Generated {name}.");
		return new(shape,
				   outputDirectory,
				   options);
	}

	private static string RunDotnet(string arguments)
	{
		Console.WriteLine($"[RuntimeShapeVerification] dotnet {arguments}");
		Console.Out.Flush();
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
								["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
								// Persistent MSBuild nodes keep redirected pipes open and hang WaitForExit.
								["MSBUILDDISABLENODEREUSE"] = "1",
								["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
							}
						};

		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start 'dotnet {arguments}'.");
		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();

		if (!process.WaitForExit(CommandTimeoutMilliseconds))
		{
			process.Kill(entireProcessTree: true);
			_ = process.WaitForExit(10_000);
			throw new TimeoutException($"'dotnet {arguments}' did not finish within {CommandTimeoutMilliseconds / 1000} seconds.");
		}

		if (!Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(30)))
			throw new TimeoutException($"'dotnet {arguments}' exited but stdout/stderr drain timed out.");

		var combined = stdoutTask.Result + stderrTask.Result;
		Assert.True(process.ExitCode == 0,
					$"'dotnet {arguments}' failed with exit code {process.ExitCode}:{Environment.NewLine}{combined}");
		return combined;
	}

	private static string FindTemplateSourceDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName,
										 "src",
										 "Content",
										 "Backend",
										 "Solution");
			if (File.Exists(Path.Combine(candidate,
										 ".template.config",
										 "template.json")))
				return candidate;

			if (File.Exists(Path.Combine(directory.FullName,
										 ".template.config",
										 "template.json")))
				return directory.FullName;
		}

		throw new DirectoryNotFoundException("Could not locate the template source directory containing .template.config/template.json.");
	}
}