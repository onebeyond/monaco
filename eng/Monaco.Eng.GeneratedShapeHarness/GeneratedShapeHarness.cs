using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Monaco.Eng.GeneratedShapeHarness;

public sealed class GeneratedShape
{
	internal GeneratedShape(string id,
							string name,
							string outputDirectory,
							ShapeOptions options,
							IReadOnlyList<string> arguments,
							string cliOutput)
	{
		Id = id;
		Name = name;
		OutputDirectory = outputDirectory;
		Options = options;
		Arguments = arguments;
		CliOutput = cliOutput;
		SolutionPath = Path.Combine(outputDirectory, $"{name}.slnx");
	}

	public string Id { get; }
	public string Name { get; }
	public string OutputDirectory { get; }
	public string SolutionPath { get; }
	public ShapeOptions Options { get; }
	public IReadOnlyList<string> Arguments { get; }
	public string CliOutput { get; }
}

public sealed class GeneratedShapeHarness : IAsyncDisposable
{
	private const int CommandTimeoutMilliseconds = 600_000;
	private const string PackageIdentity = "Monaco.Template";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	private readonly Lock _gate = new();
	private readonly List<GeneratedShape> _shapes = [];
	private readonly List<Process> _processes = [];
	private GeneratedShape[] _provisioned = [];
	private readonly string _templateEngineBaseline;
	private bool _disposed;
	private int _packCount;
	private bool _templateEngineUnchanged = true;

	private GeneratedShapeHarness(string productRoot, string tempRoot, string protocolInvocation)
	{
		ProductRoot = productRoot;
		TempRoot = tempRoot;
		ProtocolInvocation = protocolInvocation;
		HivePath = Path.Combine(tempRoot, "hive");
		EvidenceDirectory = Path.Combine(tempRoot, "evidence");
		ToolsDirectory = Path.Combine(tempRoot, "tools");
		FeedDirectory = Path.Combine(tempRoot, "feed");
		_templateEngineBaseline = TemplateEngineFingerprint();
		Build = new BuildAdapter(this);
		Start = new StartAdapter(this);
		StaticInspection = new StaticInspectionAdapter(this);
		Runtime = new RuntimeAdapter(this);
		Migration = new MigrationAdapter(this);
		Evidence = new EvidenceAdapter(this);
		Receiver = new ToolSlot(this, "receiver");
		Load = new ToolSlot(this, "load");
		Fault = new ToolSlot(this, "fault");
	}

	public string ProductRoot { get; }
	public string TempRoot { get; }
	public string HivePath { get; }
	public string EvidenceDirectory { get; }
	public string ToolsDirectory { get; }
	public string FeedDirectory { get; }
	public string PackagesDirectory => Path.Combine(TempRoot, "packages");
	public string ProtocolInvocation { get; }
	public string SourceSha { get; private set; } = "";
	public string PackageId { get; private set; } = "";
	public string PackageVersion { get; private set; } = "";
	public string PackageSha256 { get; private set; } = "";
	public string PackagePath { get; private set; } = "";
	public int PackCount => _packCount;
	public bool UserTemplateEngineUnchanged => _templateEngineUnchanged;
	public IReadOnlyList<GeneratedShape> Shapes => _shapes;
	public IReadOnlyList<GeneratedShape> ProvisionedShapes => _provisioned;
	public BuildAdapter Build { get; }
	public StartAdapter Start { get; }
	public StaticInspectionAdapter StaticInspection { get; }
	public RuntimeAdapter Runtime { get; }
	public MigrationAdapter Migration { get; }
	public EvidenceAdapter Evidence { get; }
	public ToolSlot Receiver { get; }
	public ToolSlot Load { get; }
	public ToolSlot Fault { get; }

	public static Task<GeneratedShapeHarness> ProvisionAsync(string productRoot,
															 string protocolInvocation,
															 CancellationToken cancellationToken) =>
		ProvisionAsync(productRoot, protocolInvocation, afterEachShape: null, cancellationToken);

	public GeneratedShape Generate(string name, params string[] parameters) =>
		GenerateAsync(name, name, parameters, CancellationToken.None).GetAwaiter().GetResult();

	public async ValueTask DisposeAsync()
	{
		lock (_gate)
		{
			if (_disposed)
				return;

			_disposed = true;
			KillRegisteredProcesses();
			DeleteTree(TempRoot);
		}

		await Task.CompletedTask;
	}

	internal static Task<GeneratedShapeHarness> ProvisionAsync(string productRoot,
															   string protocolInvocation,
															   Func<GeneratedShapeHarness, GeneratedShape, int, Task>? afterEachShape,
															   CancellationToken cancellationToken) =>
		ProvisionCoreAsync(productRoot, protocolInvocation, afterEachShape, cancellationToken);

	internal static string FindProductRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName, "src", "Monaco.Template.nuspec");
			if (File.Exists(candidate))
				return directory.FullName;
		}

		throw new DirectoryNotFoundException("Could not locate the product root containing src/Monaco.Template.nuspec.");
	}

	internal static bool IsInside(string path, string root)
	{
		var full = Path.GetFullPath(path);
		var parent = Path.GetFullPath(root);
		return full.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
			   full.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	internal async Task<GeneratedShape> GenerateIntoAsync(string shapeId,
														  string solutionName,
														  IReadOnlyList<string> arguments,
														  CancellationToken cancellationToken) =>
		await GenerateAsync(shapeId, solutionName, arguments, cancellationToken);

	internal async Task PackCommonProjectsAsync(GeneratedShape source,
												IReadOnlyList<string> projectSuffixes,
												CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(FeedDirectory);
		foreach (var suffix in projectSuffixes)
		{
			var project = Path.Combine(source.OutputDirectory, $"{source.Name}.{suffix}", $"{source.Name}.{suffix}.csproj");
			if (!File.Exists(project))
				throw new FileNotFoundException($"Common project to pack was not generated: {project}");

			// GeneratePackageOnBuild packs before the assembly exists and fails with NU5026.
			await RunAsync("dotnet",
						   [
							   "pack",
							   project,
							   "-c",
							   "Release",
							   "-o",
							   FeedDirectory,
							   "--nologo",
							   "-p:Version=0.0.1-alpha1",
							   "-p:GeneratePackageOnBuild=false"
						   ],
						   TempRoot,
						   cancellationToken,
						   requireSuccess: true);
		}
	}

	internal string WriteFeedConfig()
	{
		Directory.CreateDirectory(FeedDirectory);
		Directory.CreateDirectory(PackagesDirectory);
		var path = Path.Combine(TempRoot, "nuget.feed.config");
		if (IsInsideGeneratedTree(path))
			throw new InvalidOperationException("Feed config must stay outside every generated tree.");

		var feed = Path.GetFullPath(FeedDirectory);
		var packages = Path.GetFullPath(PackagesDirectory);
		var document = new XDocument(new XElement("configuration",
												  new XElement("config",
															   new XElement("add",
																			new XAttribute("key", "globalPackagesFolder"),
																			new XAttribute("value", packages))),
												  new XElement("packageSources",
															   new XElement("clear"),
															   new XElement("add",
																			new XAttribute("key", "nuget.org"),
																			new XAttribute("value", "https://api.nuget.org/v3/index.json"),
																			new XAttribute("protocolVersion", "3")),
															   new XElement("add",
																			new XAttribute("key", "generated-shape-feed"),
																			new XAttribute("value", feed)))));
		document.Save(path);
		return path;
	}

	internal string WriteConnectionString(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("A connection string is required.", nameof(connectionString));

		var directory = Path.Combine(TempRoot, "secrets");
		Directory.CreateDirectory(directory);
		var path = Path.Combine(directory, $"connection-{Guid.NewGuid():N}.txt");
		File.WriteAllText(path, connectionString);
		if (!IsInside(path, TempRoot) || IsInsideGeneratedTree(path))
			throw new InvalidOperationException("Connection string file must stay under the temp root and outside generated trees.");

		return path;
	}

	internal int StartRegisteredSleep(GeneratedShape shape)
	{
		Directory.CreateDirectory(ToolsDirectory);
		RefuseWorkingDirectory(ToolsDirectory);
		var (fileName, arguments) = SleepCommand();
		var process = StartProcess(fileName, arguments, ToolsDirectory, redirect: false);
		Register(process);
		_ = shape;
		return process.Id;
	}

	internal async Task<EvidenceEnvelope> RunToolAsync(string kind,
													   GeneratedShape shape,
													   string? workingDirectory,
													   CancellationToken cancellationToken)
	{
		var cwd = Path.GetFullPath(workingDirectory ?? ToolsDirectory);
		RefuseWorkingDirectory(cwd);
		Directory.CreateDirectory(cwd);
		var (fileName, arguments) = PrintWorkingDirectoryCommand();
		var result = await RunAsync(fileName, arguments, cwd, cancellationToken, requireSuccess: false);
		return WriteEvidence(shape, kind, result.ExitCode, cwd, result.Output);
	}

	internal async Task<EvidenceEnvelope> RunBuildAsync(GeneratedShape shape,
														string? configFile,
														CancellationToken cancellationToken)
	{
		List<string> arguments =
		[
			"build",
			shape.SolutionPath,
			"-c",
			"Release",
			"--nologo",
			"-v",
			"q"
		];
		IReadOnlyDictionary<string, string>? environment = null;
		if (configFile is not null)
		{
			arguments.Add("--configfile");
			arguments.Add(configFile);
			// NUGET_PACKAGES outranks globalPackagesFolder, so the build must set both.
			Directory.CreateDirectory(PackagesDirectory);
			environment = new Dictionary<string, string>
			{
				["NUGET_PACKAGES"] = Path.GetFullPath(PackagesDirectory)
			};
		}

		var result = await RunAsync("dotnet", arguments, TempRoot, cancellationToken, requireSuccess: false, environment);
		return WriteEvidence(shape, "build", result.ExitCode, TempRoot, result.Output);
	}

	internal async Task<EvidenceEnvelope> RunStartAsync(GeneratedShape shape, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(ToolsDirectory);
		RefuseWorkingDirectory(ToolsDirectory);
		var result = await RunAsync("dotnet", ["--version"], ToolsDirectory, cancellationToken, requireSuccess: false);
		return WriteEvidence(shape, "start", result.ExitCode, ToolsDirectory, result.Output);
	}

	internal EvidenceEnvelope Inspect(GeneratedShape shape) =>
		WriteEvidence(shape, "static-inspection", exitCode: null, workingDirectory: null, output: null);

	internal EvidenceEnvelope ReadEvidence(GeneratedShape shape)
	{
		var path = EvidencePath(shape.Id, "shape");
		if (!File.Exists(path))
			return WriteEvidence(shape, "evidence", exitCode: null, workingDirectory: null, output: null);

		var stored = JsonSerializer.Deserialize<EvidenceEnvelope>(File.ReadAllText(path), JsonOptions)
					 ?? throw new InvalidOperationException($"Evidence for '{shape.Id}' could not be read.");
		return stored with { Kind = "evidence" };
	}

	internal async Task<EvidenceEnvelope> RunMigrationAsync(GeneratedShape shape, CancellationToken cancellationToken)
	{
		var result = await RunAsync("dotnet",
								   ["run", "--file", "eng/migrations.cs", "--", "verify"],
								   ProductRoot,
								   cancellationToken,
								   requireSuccess: false);
		return WriteEvidence(shape, "migration", result.ExitCode, ProductRoot, result.Output);
	}

	internal bool IsInsideGeneratedTree(string path) =>
		_shapes.Any(shape => IsInside(path, shape.OutputDirectory));

	private static async Task<GeneratedShapeHarness> ProvisionCoreAsync(string productRoot,
																		string protocolInvocation,
																		Func<GeneratedShapeHarness, GeneratedShape, int, Task>? afterEachShape,
																		CancellationToken cancellationToken)
	{
		var harness = Create(productRoot, protocolInvocation);
		var transferred = false;
		try
		{
			await harness.PackAndInstallAsync(cancellationToken);
			var index = 0;
			foreach (var definition in ShapeCatalog.All)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var shape = await harness.GenerateAsync(definition.Id,
														definition.Id,
														definition.Arguments,
														definition.Options,
														cancellationToken);
				if (afterEachShape is not null)
					await afterEachShape(harness, shape, index);

				index++;
			}

			harness._provisioned = harness._shapes.ToArray();
			harness.NoteTemplateEngine();
			transferred = true;
			return harness;
		}
		finally
		{
			if (!transferred)
				await harness.DisposeAsync();
		}
	}

	private static GeneratedShapeHarness Create(string productRoot, string protocolInvocation)
	{
		var fullProduct = Path.GetFullPath(productRoot);
		var nuspec = Path.Combine(fullProduct, "src", "Monaco.Template.nuspec");
		if (!File.Exists(nuspec))
			throw new DirectoryNotFoundException($"Template nuspec not found at {nuspec}.");

		var tempRoot = CreateTempRoot(fullProduct);
		return new GeneratedShapeHarness(fullProduct, tempRoot, protocolInvocation);
	}

	private static string CreateTempRoot(string productRoot)
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-generated-shape-" + Guid.NewGuid().ToString("N"));
		var full = Path.GetFullPath(root);
		if (IsInside(full, productRoot))
			throw new InvalidOperationException($"Temporary root {full} is inside the product root.");

		Directory.CreateDirectory(full);
		return full;
	}

	private async Task PackAndInstallAsync(CancellationToken cancellationToken)
	{
		var nuspec = Path.Combine(ProductRoot, "src", "Monaco.Template.nuspec");
		var document = XDocument.Load(nuspec);
		PackageId = RequiredElement(document, "id");
		PackageVersion = RequiredElement(document, "version");
		if (!string.Equals(PackageId, PackageIdentity, StringComparison.Ordinal))
			throw new InvalidOperationException($"Expected package id {PackageIdentity}, found {PackageId}.");

		var sha = await RunAsync("git", ["rev-parse", "HEAD"], ProductRoot, cancellationToken, requireSuccess: true);
		SourceSha = sha.Output.Trim();
		Console.WriteLine($"[GeneratedShapeHarness] Packing {PackageId} {PackageVersion} from {SourceSha}.");
		await RunAsync("nuget",
					   ["pack", "src/Monaco.Template.nuspec", "-OutputDirectory", TempRoot, "-NonInteractive"],
					   ProductRoot,
					   cancellationToken,
					   requireSuccess: true);
		_packCount++;
		var packages = Directory.GetFiles(TempRoot, $"{PackageIdentity}.*.nupkg");
		if (packages.Length != 1)
			throw new InvalidOperationException($"Expected one {PackageIdentity} nupkg under {TempRoot}, found {packages.Length}.");

		PackagePath = packages[0];
		PackageSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(PackagePath, cancellationToken))).ToLowerInvariant();
		Directory.CreateDirectory(HivePath);
		Console.WriteLine($"[GeneratedShapeHarness] Installing {Path.GetFileName(PackagePath)} into {HivePath}.");
		await RunAsync("dotnet",
					   ["new", "install", PackagePath, "--debug:custom-hive", HivePath],
					   TempRoot,
					   cancellationToken,
					   requireSuccess: true);
		NoteTemplateEngine();
	}

	private Task<GeneratedShape> GenerateAsync(string shapeId,
											   string solutionName,
											   IReadOnlyList<string> arguments,
											   CancellationToken cancellationToken) =>
		GenerateAsync(shapeId, solutionName, arguments, ShapeCatalog.Apply(arguments), cancellationToken);

	private async Task<GeneratedShape> GenerateAsync(string shapeId,
													 string solutionName,
													 IReadOnlyList<string> arguments,
													 ShapeOptions options,
													 CancellationToken cancellationToken)
	{
		if (_shapes.Any(shape => string.Equals(shape.Id, shapeId, StringComparison.Ordinal)))
			throw new InvalidOperationException($"Shape '{shapeId}' already exists.");

		var outputDirectory = Path.Combine(TempRoot, "shapes", shapeId);
		Directory.CreateDirectory(Path.Combine(TempRoot, "shapes"));
		Console.WriteLine($"[GeneratedShapeHarness] Generating {shapeId} ({solutionName}).");
		List<string> args =
		[
			"new",
			"monaco-backend-solution",
			"--name",
			solutionName,
			"--output",
			outputDirectory,
			"--debug:custom-hive",
			HivePath
		];
		args.AddRange(arguments);
		// Exit 0 is the only provision gate, including E1 and E3.
		var result = await RunAsync("dotnet", args, TempRoot, cancellationToken, requireSuccess: true);
		NoteTemplateEngine();
		var shape = new GeneratedShape(shapeId, solutionName, outputDirectory, options, arguments, result.Output);
		_shapes.Add(shape);
		WriteEvidence(shape, "shape", result.ExitCode, workingDirectory: null, output: null);
		return shape;
	}

	private EvidenceEnvelope WriteEvidence(GeneratedShape shape,
										   string kind,
										   int? exitCode,
										   string? workingDirectory,
										   string? output)
	{
		var envelope = new EvidenceEnvelope(SourceSha,
											PackageId,
											PackageVersion,
											PackageSha256,
											shape.Id,
											shape.Options,
											ProtocolInvocation,
											exitCode,
											workingDirectory,
											kind,
											output);
		Directory.CreateDirectory(EvidenceDirectory);
		var path = EvidencePath(shape.Id, kind);
		var stored = envelope with { Output = null };
		File.WriteAllText(path, JsonSerializer.Serialize(stored, JsonOptions));
		return envelope;
	}

	private string EvidencePath(string shapeId, string kind)
	{
		var name = string.Equals(kind, "shape", StringComparison.Ordinal)
					   ? $"{shapeId}.json"
					   : $"{kind}-{shapeId}-{Guid.NewGuid():N}.json";
		return Path.Combine(EvidenceDirectory, name);
	}

	private void RefuseWorkingDirectory(string workingDirectory)
	{
		if (IsInsideGeneratedTree(workingDirectory))
			throw new InvalidOperationException($"Working directory '{workingDirectory}' is inside a generated tree.");
	}

	private void NoteTemplateEngine()
	{
		if (!string.Equals(_templateEngineBaseline, TemplateEngineFingerprint(), StringComparison.Ordinal))
			_templateEngineUnchanged = false;
	}

	private static string TemplateEngineFingerprint()
	{
		var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".templateengine");
		if (!Directory.Exists(root))
			return "";

		return string.Join('\n',
						   Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
									.Select(path =>
											{
												var info = new FileInfo(path);
												return $"{Path.GetRelativePath(root, path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
											})
									.OrderBy(line => line, StringComparer.Ordinal));
	}

	private async Task<CommandResult> RunAsync(string fileName,
											   IReadOnlyList<string> arguments,
											   string workingDirectory,
											   CancellationToken cancellationToken,
											   bool requireSuccess,
											   IReadOnlyDictionary<string, string>? environment = null)
	{
		var process = StartProcess(fileName, arguments, workingDirectory, redirect: true, environment);
		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(CommandTimeoutMilliseconds);
		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			Kill(process);
			process.Dispose();
			throw new TimeoutException($"'{fileName}' did not finish within {CommandTimeoutMilliseconds / 1000} seconds.");
		}
		catch (OperationCanceledException)
		{
			Kill(process);
			process.Dispose();
			throw;
		}

		if (!Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(30)))
		{
			process.Dispose();
			throw new TimeoutException($"'{fileName}' exited but stdout/stderr drain timed out.");
		}

		var output = stdoutTask.Result + stderrTask.Result;
		var exitCode = process.ExitCode;
		process.Dispose();
		if (requireSuccess && exitCode != 0)
			throw new InvalidOperationException($"'{fileName} {string.Join(' ', arguments)}' failed with exit code {exitCode}:{Environment.NewLine}{output}");

		return new CommandResult(exitCode, output);
	}

	private Process StartProcess(string fileName,
								 IReadOnlyList<string> arguments,
								 string workingDirectory,
								 bool redirect,
								 IReadOnlyDictionary<string, string>? environment = null)
	{
		var startInfo = new ProcessStartInfo(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = redirect,
			RedirectStandardError = redirect,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		foreach (var argument in arguments)
			startInfo.ArgumentList.Add(argument);

		// Persistent MSBuild nodes keep redirected pipes open and hang WaitForExit.
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

		var process = new Process { StartInfo = startInfo };
		if (!process.Start())
			throw new InvalidOperationException($"Failed to start '{fileName}'.");

		return process;
	}

	private void Register(Process process)
	{
		lock (_gate)
			_processes.Add(process);
	}

	private void KillRegisteredProcesses()
	{
		foreach (var process in _processes)
		{
			Kill(process);
			process.Dispose();
		}

		_processes.Clear();
	}

	private static void Kill(Process process)
	{
		try
		{
			if (process.HasExited)
				return;

			process.Kill(entireProcessTree: true);
			process.WaitForExit(10_000);
		}
		catch (InvalidOperationException)
		{
		}
		catch (NotSupportedException)
		{
		}
	}

	private static void DeleteTree(string path)
	{
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				if (!Directory.Exists(path))
					return;

				foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
					File.SetAttributes(file, FileAttributes.Normal);

				Directory.Delete(path, recursive: true);
				return;
			}
			catch (Exception) when (attempt < 10)
			{
				Thread.Sleep(1000);
			}
		}
	}

	private static string RequiredElement(XDocument document, string localName) =>
		document.Descendants().First(element => element.Name.LocalName == localName).Value.Trim();

	private static (string FileName, string[] Arguments) PrintWorkingDirectoryCommand() =>
		OperatingSystem.IsWindows()
			? ("cmd.exe", ["/c", "cd"])
			: ("/bin/pwd", []);

	private static (string FileName, string[] Arguments) SleepCommand() =>
		OperatingSystem.IsWindows()
			? ("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 300"])
			: ("sleep", ["300"]);

	private readonly record struct CommandResult(int ExitCode, string Output);
}
