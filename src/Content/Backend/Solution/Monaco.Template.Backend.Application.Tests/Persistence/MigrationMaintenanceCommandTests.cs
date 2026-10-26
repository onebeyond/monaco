using AwesomeAssertions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Persistence;

[ExcludeFromCodeCoverage]
[Trait("Application Persistence", "Migration Variants")]
[Collection("Migration maintenance commands")]
public sealed class MigrationMaintenanceCommandTests
{
	private static readonly string[] LiveIds =
	[
		"20260915220138",
		"20260915223105",
		"20260915223146",
		"20260915223219"
	];

	[Fact(DisplayName = "verify rejects a dotnet-ef pin other than 10.0.2 before Docker")]
	public void VerifyRejectsPinBeforeDocker()
	{
		var productRoot = RequireProductRoot();
		var containers = ContainerNames();
		var root = NewTempRoot("monaco-pin");
		try
		{
			WriteProductSkeleton(root, productRoot);
			File.WriteAllText(Path.Combine(root, ".config", "dotnet-tools.json"),
							  """
							  {
							    "version": 1,
							    "isRoot": true,
							    "tools": {
							      "dotnet-ef": {
							        "version": "9.0.0",
							        "commands": [ "dotnet-ef" ]
							      }
							    }
							  }
							  """);

			var result = RunMigrations(productRoot, root, "verify", timeoutMilliseconds: 180_000);
			result.ExitCode.Should().Be(1);
			result.Output.Should().Contain("expected 10.0.2");
			result.Output.Should().NotContain("pulling SQL Server image");
			ContainerNames().Should().BeEquivalentTo(containers);
		}
		finally
		{
			DeleteTree(root);
		}
	}

	[Fact(DisplayName = "verify rejects a drifted SQL image lock before pull")]
	public void VerifyRejectsLockBeforePull()
	{
		var productRoot = RequireProductRoot();
		var containers = ContainerNames();
		var root = NewTempRoot("monaco-lock");
		try
		{
			WriteProductSkeleton(root, productRoot);
			File.Copy(Path.Combine(productRoot, ".config", "dotnet-tools.json"),
					  Path.Combine(root, ".config", "dotnet-tools.json"));
			File.WriteAllText(Path.Combine(root, "eng", "mssql-server-image.lock.json"),
							  """
							  {
							    "repository": "mcr.microsoft.com/mssql/server",
							    "provenanceTag": "2022-CU26-ubuntu-22.04",
							    "manifestDigest": "sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89",
							    "extra": "no"
							  }
							  """);

			var result = RunMigrations(productRoot, root, "verify", timeoutMilliseconds: 180_000);
			result.ExitCode.Should().Be(1);
			result.Output.Should().Contain("unexpected property");
			result.Output.Should().NotContain("pulling SQL Server image");
			ContainerNames().Should().BeEquivalentTo(containers);
		}
		finally
		{
			DeleteTree(root);
		}
	}

	[Fact(DisplayName = "verify fails when Docker is unreachable and leaves no container")]
	public void VerifyFailsWhenDockerIsUnreachable()
	{
		var productRoot = RequireProductRoot();
		var containers = ContainerNames();
		var result = RunMigrations(productRoot,
								   productRoot,
								   "verify",
								   timeoutMilliseconds: 180_000,
								   environment: new Dictionary<string, string>(StringComparer.Ordinal)
								   {
									   ["DOCKER_HOST"] = "tcp://127.0.0.1:1"
								   });

		result.ExitCode.Should().Be(1);
		result.Output.Should().Contain("docker pull");
		ContainerNames().Should().BeEquivalentTo(containers);
	}

	[Fact(DisplayName = "verify rejects pending model changes and leaves the migration bytes")]
	public void VerifyRejectsPendingModelChanges()
	{
		var productRoot = RequireProductRoot();
		var containers = ContainerNames();
		var copy = CopyProduct(productRoot, "monaco-schema");
		try
		{
			ShortenActorMaxLength(copy);
			var before = HashDestinations(copy);
			var result = RunMigrations(productRoot, copy, "verify", timeoutMilliseconds: 1_200_000);
			result.ExitCode.Should().Be(1);
			result.Output.Should().Contain("has-pending-model-changes exited");
			HashDestinations(copy).Should().Equal(before);
			ContainerNames().Should().BeEquivalentTo(containers);
			LiveIdsStillShipped(productRoot);
		}
		finally
		{
			DeleteTree(copy);
		}
	}

	[Fact(DisplayName = "verify succeeds without leaking the password or changing the product")]
	public void VerifySucceeds()
	{
		var productRoot = RequireProductRoot();
		var containers = ContainerNames();
		var before = HashDestinations(productRoot);
		var result = RunMigrations(productRoot, productRoot, "verify", timeoutMilliseconds: 2_700_000);

		result.ExitCode.Should().Be(0, result.Output);
		result.Output.Should().Contain("OK: verified 4 API fingerprints and 2 worker-only probes.");
		result.Output.Should().Contain("generating Worker Core");
		result.Output.Should().Contain("generating Worker Full");
		result.Output.Should().NotContain("Aa1!");
		result.Output.Should().NotContain("Password=");
		result.Output.Should().NotContain("Server=127.0.0.1");
		HashDestinations(productRoot).Should().Equal(before);
		ContainerNames().Should().BeEquivalentTo(containers);
		LiveIdsStillShipped(productRoot);
	}

	[Fact(DisplayName = "regenerate restores destinations when a fingerprint fails validation")]
	public void RegenerateLeavesDestinationsUnchangedWhenValidationFails()
	{
		var productRoot = RequireProductRoot();
		var copy = CopyProduct(productRoot, "monaco-regen-fail");
		try
		{
			ShortenActorMaxLength(copy);
			var before = HashDestinations(copy);
			var result = RunMigrations(productRoot, copy, "regenerate", timeoutMilliseconds: 1_200_000);
			result.ExitCode.Should().Be(1);
			result.Output.Should().Contain("does not match the stamp contract");
			HashDestinations(copy).Should().Equal(before);
			LiveIdsStillShipped(productRoot);
		}
		finally
		{
			DeleteTree(copy);
		}
	}

	[Fact(DisplayName = "regenerate replaces all four trios on a product copy")]
	public void RegenerateReplacesAllFourTriosOnACopy()
	{
		var productRoot = RequireProductRoot();
		var copy = CopyProduct(productRoot, "monaco-regen-ok");
		try
		{
			var result = RunMigrations(productRoot, copy, "regenerate", timeoutMilliseconds: 2_700_000);
			result.ExitCode.Should().Be(0, result.Output);
			var ids = DestinationIds(copy);
			ids.Should().HaveCount(4);
			ids.Should().OnlyHaveUniqueItems();
			ids.Should().NotIntersectWith(LiveIds);
			LiveIdsStillShipped(productRoot);
		}
		finally
		{
			DeleteTree(copy);
		}
	}

	private static string RequireProductRoot()
	{
		var root = FindProductRoot();
		root.Should().NotBeNull();
		return root!;
	}

	private static void LiveIdsStillShipped(string productRoot) =>
		DestinationIds(productRoot).Should().Equal(LiveIds);

	private static void ShortenActorMaxLength(string productRoot)
	{
		var path = Path.Combine(productRoot,
								"src",
								"Content",
								"Backend",
								"Solution",
								"Monaco.Template.Backend.Common.Domain",
								"Model",
								"Contracts",
								"IAuditable.cs");
		var text = File.ReadAllText(path);
		text.Should().Contain("const int ActorMaxLength = 512;");
		File.WriteAllText(path, text.Replace("const int ActorMaxLength = 512;", "const int ActorMaxLength = 256;", StringComparison.Ordinal));
	}

	private static string[] DestinationIds(string productRoot) =>
	[
		.. Destinations(productRoot).Select(ReadInitId)
	];

	private static Dictionary<string, string> HashDestinations(string productRoot)
	{
		var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var directory in Destinations(productRoot))
		{
			var builder = new StringBuilder();
			foreach (var file in Directory.GetFiles(directory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
			{
				var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
				builder.Append(Path.GetFileName(file)).Append(':').Append(hash).Append(';');
			}

			hashes[directory] = builder.ToString();
		}

		return hashes;
	}

	private static IEnumerable<string> Destinations(string productRoot)
	{
		var solution = Path.Combine(productRoot, "src", "Content", "Backend", "Solution");
		yield return Path.Combine(solution, "Monaco.Template.Backend.Application", "Persistence", "Migrations");
		yield return Path.Combine(solution, "_TemplateArtifacts", "MigrationVariants", "Core");
		yield return Path.Combine(solution, "_TemplateArtifacts", "MigrationVariants", "FilesOnly");
		yield return Path.Combine(solution, "_TemplateArtifacts", "MigrationVariants", "MessagingOnly");
	}

	private static string ReadInitId(string directory)
	{
		var init = Directory.GetFiles(directory, "*_Init.cs")
							.Single(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
		var name = Path.GetFileName(init)!;
		return name[..^"_Init.cs".Length];
	}

	private static void WriteProductSkeleton(string root, string productRoot)
	{
		Directory.CreateDirectory(Path.Combine(root, ".config"));
		var propsDirectory = Path.Combine(root, "src", "Content", "Backend", "Solution");
		Directory.CreateDirectory(propsDirectory);
		Directory.CreateDirectory(Path.Combine(root, "eng"));
		File.Copy(Path.Combine(productRoot, "src", "Content", "Backend", "Solution", "Directory.Packages.props"),
				  Path.Combine(propsDirectory, "Directory.Packages.props"));
	}

	private static string CopyProduct(string productRoot, string prefix)
	{
		var destination = NewTempRoot(prefix);
		var arguments = $"\"{productRoot}\" \"{destination}\" /E /XD .git bin obj .vs /NFL /NDL /NJH /NJS /NC /NS";
		var result = RunProcess("robocopy", arguments, productRoot, environment: null, timeoutMilliseconds: 600_000);
		result.ExitCode.Should().BeLessThan(8, result.Output);
		return destination;
	}

	private static HashSet<string> ContainerNames()
	{
		var result = RunProcess("docker",
								"ps -a --filter name=monaco-mig --format {{.Names}}",
								Directory.GetCurrentDirectory(),
								environment: null,
								timeoutMilliseconds: 30_000);
		result.ExitCode.Should().Be(0, result.Output);
		return result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
					 .ToHashSet(StringComparer.Ordinal);
	}

	private static (int ExitCode, string Output) RunMigrations(string productRoot,
															   string workingDirectory,
															   string command,
															   int timeoutMilliseconds,
															   IReadOnlyDictionary<string, string>? environment = null)
	{
		var script = Path.Combine(productRoot, "eng", "migrations.cs");
		return RunProcess("dotnet",
						  $"run --file \"{script}\" -- {command}",
						  workingDirectory,
						  environment,
						  timeoutMilliseconds);
	}

	private static (int ExitCode, string Output) RunProcess(string fileName,
															string arguments,
															string workingDirectory,
															IReadOnlyDictionary<string, string>? environment,
															int timeoutMilliseconds)
	{
		var startInfo = new ProcessStartInfo(fileName, arguments)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		if (environment is not null)
		{
			foreach (var pair in environment)
				startInfo.Environment[pair.Key] = pair.Value;
		}

		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
		var stdout = process.StandardOutput.ReadToEndAsync();
		var stderr = process.StandardError.ReadToEndAsync();
		if (!process.WaitForExit(timeoutMilliseconds))
		{
			process.Kill(entireProcessTree: true);
			process.WaitForExit(10_000);
			throw new TimeoutException($"{fileName} {arguments} did not finish within {timeoutMilliseconds / 1000} seconds.");
		}

		if (!Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(30)))
			throw new TimeoutException($"{fileName} exited but stdout/stderr drain timed out.");

		return (process.ExitCode, stdout.Result + stderr.Result);
	}

	private static string NewTempRoot(string prefix) =>
		Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"))).FullName;

	private static void DeleteTree(string path)
	{
		if (!Directory.Exists(path))
			return;

		for (var attempt = 1; ; attempt++)
		{
			try
			{
				foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
					File.SetAttributes(file, FileAttributes.Normal);
				Directory.Delete(path, recursive: true);
				return;
			}
			catch (Exception) when (attempt < 5)
			{
				Thread.Sleep(500);
			}
		}
	}

	private static string? FindProductRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, ".config", "dotnet-tools.json")) &&
				File.Exists(Path.Combine(directory.FullName, "eng", "migrations.cs")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}