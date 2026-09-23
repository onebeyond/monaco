#:property PublishAot=false
#:property Nullable=enable
#:property ImplicitUsings=enable

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const string ToolName = "dotnet-ef";
const string DesignPackageId = "Microsoft.EntityFrameworkCore.Design";
const string RelativeManifest = ".config/dotnet-tools.json";
const string RelativeProps = "src/Content/Backend/Solution/Directory.Packages.props";

if (args is not ["check"])
{
	Console.Error.WriteLine("Usage: dotnet run --file eng/migrations.cs -- check");
	return 1;
}

try
{
	var productRoot = ResolveProductRoot();
	var manifestPath = Path.GetFullPath(Path.Combine(productRoot, RelativeManifest));
	var propsPath = Path.GetFullPath(Path.Combine(productRoot, RelativeProps));

	if (!File.Exists(manifestPath))
	{
		Fail($"Missing local tool manifest at {manifestPath}.");
		return 1;
	}

	if (!File.Exists(propsPath))
	{
		Fail($"Missing Directory.Packages.props at {propsPath}.");
		return 1;
	}

	var expectedVersion = ReadDesignPackageVersion(propsPath);
	if (expectedVersion is null)
	{
		Fail($"Could not read {DesignPackageId} version from {propsPath}.");
		return 1;
	}

	string manifestVersion;
	try
	{
		manifestVersion = ReadSoleDotnetEfVersion(manifestPath);
	}
	catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException)
	{
		Fail($"Local tool manifest pin check failed: {ex.Message}");
		return 1;
	}

	if (!string.Equals(manifestVersion, expectedVersion, StringComparison.Ordinal))
	{
		Fail($"Manifest pins {ToolName} {manifestVersion}, but {DesignPackageId} is {expectedVersion}.");
		return 1;
	}

	if (!TryRestoreLocalTools(productRoot, manifestPath, out var restoreError))
	{
		Fail(restoreError);
		return 1;
	}

	if (!TryReadEffectiveToolVersion(manifestPath, out var effectiveVersion, out var probeError))
	{
		Fail(probeError);
		return 1;
	}

	if (!string.Equals(effectiveVersion, expectedVersion, StringComparison.Ordinal))
	{
		Fail($"Effective {ToolName} version token is '{effectiveVersion}', expected '{expectedVersion}'.");
		return 1;
	}

	Console.WriteLine($"OK: repository-local {ToolName} {expectedVersion} matches {DesignPackageId}.");
	return 0;
}
catch (Exception ex)
{
	Fail(ex.Message);
	return 1;
}

static string ResolveProductRoot()
{
	var scriptDir = Path.GetDirectoryName(Path.GetFullPath(Environment.GetCommandLineArgs()[0])) ??
					Directory.GetCurrentDirectory();

	foreach (var start in new[] { scriptDir, Directory.GetCurrentDirectory() })
	{
		for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
		{
			if (LooksLikeProductRoot(dir.FullName))
				return dir.FullName;

			var nested = Path.Combine(dir.FullName, "monaco");
			if (LooksLikeProductRoot(nested))
				return nested;
		}
	}

	throw new InvalidOperationException("Could not resolve the Monaco product root (expected .config/dotnet-tools.json and Directory.Packages.props).");
}

static bool LooksLikeProductRoot(string path) =>
	File.Exists(Path.Combine(path, RelativeManifest)) &&
	File.Exists(Path.Combine(path, RelativeProps));

static string? ReadDesignPackageVersion(string propsPath)
{
	var document = XDocument.Load(propsPath);
	foreach (var packageVersion in document.Descendants("PackageVersion"))
	{
		var include = packageVersion.Attribute("Include")?.Value;
		if (!string.Equals(include, DesignPackageId, StringComparison.Ordinal))
			continue;

		var version = packageVersion.Attribute("Version")?.Value;
		return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
	}

	return null;
}

static string ReadSoleDotnetEfVersion(string manifestPath)
{
	using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
	if (!document.RootElement.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Object)
		throw new InvalidDataException("Manifest has no tools object.");

	string? version = null;
	foreach (var tool in tools.EnumerateObject())
	{
		if (!string.Equals(tool.Name, ToolName, StringComparison.Ordinal))
			throw new InvalidDataException($"Manifest declares unexpected tool '{tool.Name}'.");

		if (!tool.Value.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String)
			throw new InvalidDataException($"Manifest tool '{ToolName}' has no string version.");

		version = versionElement.GetString();
	}

	if (version is null)
		throw new InvalidDataException($"Manifest does not declare '{ToolName}'.");

	if (string.IsNullOrWhiteSpace(version))
		throw new InvalidDataException($"Manifest tool '{ToolName}' has an empty version.");

	return version.Trim();
}

static bool TryRestoreLocalTools(string productRoot, string manifestPath, out string error)
{
	var startInfo = new ProcessStartInfo("dotnet")
	{
		WorkingDirectory = productRoot,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false
	};
	startInfo.ArgumentList.Add("tool");
	startInfo.ArgumentList.Add("restore");
	startInfo.ArgumentList.Add("--tool-manifest");
	startInfo.ArgumentList.Add(manifestPath);

	using var process = Process.Start(startInfo);
	if (process is null)
	{
		error = "Failed to start 'dotnet tool restore'.";
		return false;
	}

	var stdout = process.StandardOutput.ReadToEnd();
	var stderr = process.StandardError.ReadToEnd();
	process.WaitForExit();

	if (process.ExitCode != 0)
	{
		error = $"dotnet tool restore failed (exit {process.ExitCode}).{Environment.NewLine}{stdout}{stderr}";
		return false;
	}

	error = string.Empty;
	return true;
}

static bool TryReadEffectiveToolVersion(string manifestPath, out string version, out string error)
{
	version = string.Empty;
	var startInfo = new ProcessStartInfo("dotnet")
	{
		// `dotnet tool run` resolves only a local manifest by walking from the working directory.
		WorkingDirectory = DirectoryOwningManifest(manifestPath),
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false
	};
	startInfo.ArgumentList.Add("tool");
	startInfo.ArgumentList.Add("run");
	startInfo.ArgumentList.Add(ToolName);
	startInfo.ArgumentList.Add("--");
	startInfo.ArgumentList.Add("--version");

	using var process = Process.Start(startInfo);
	if (process is null)
	{
		error = $"Failed to start 'dotnet tool run {ToolName}'.";
		return false;
	}

	var stdout = process.StandardOutput.ReadToEnd();
	var stderr = process.StandardError.ReadToEnd();
	process.WaitForExit();

	if (process.ExitCode != 0)
	{
		error = $"dotnet tool run {ToolName} --version failed (exit {process.ExitCode}).{Environment.NewLine}{stdout}{stderr}";
		return false;
	}

	var match = Regex.Match(stdout + Environment.NewLine + stderr, @"^\s*(\d+\.\d+\.\d+(?:[-+][\w.]+)?)\s*$",
		RegexOptions.Multiline);
	if (!match.Success)
	{
		error = $"Could not parse an exact version token from {ToolName} --version output.{Environment.NewLine}{stdout}{stderr}";
		return false;
	}

	version = match.Groups[1].Value;
	error = string.Empty;
	return true;
}

static string DirectoryOwningManifest(string manifestPath)
{
	var full = Path.GetFullPath(manifestPath);
	var parent = Path.GetDirectoryName(full);
	if (parent is not null && string.Equals(Path.GetFileName(parent), ".config", StringComparison.OrdinalIgnoreCase))
	{
		var owner = Path.GetDirectoryName(parent);
		if (!string.IsNullOrEmpty(owner))
			return owner;
	}

	return parent ?? Directory.GetCurrentDirectory();
}

static void Fail(string message) => Console.Error.WriteLine($"migrations check failed: {message}");