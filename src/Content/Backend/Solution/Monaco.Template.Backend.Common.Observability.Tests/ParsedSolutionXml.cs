using System.Xml.Linq;

namespace Monaco.Template.Backend.Common.Observability.Tests;

internal static class ParsedSolutionXml
{
	internal static readonly string[] OpenTelemetryPackages =
	[
		"OpenTelemetry.Extensions.Hosting",
		"OpenTelemetry.Exporter.OpenTelemetryProtocol",
		"OpenTelemetry.Instrumentation.Runtime",
		"OpenTelemetry.Instrumentation.Http",
		"OpenTelemetry.Instrumentation.AspNetCore",
		"OpenTelemetry.Instrumentation.SqlClient"
	];

	internal static string SolutionDirectory { get; } = FindSolutionDirectory();

	internal static XDocument Load(string relativePath) =>
		LoadFromPath(Path.Combine(SolutionDirectory, relativePath));

	internal static IReadOnlyList<(string Path, XDocument Document)> CsprojDocuments() =>
	[
		.. Directory.EnumerateFiles(SolutionDirectory, "*.csproj", SearchOption.AllDirectories)
					.Where(path => !IsBuildArtifact(path))
					.Select(path => (path, LoadFromPath(path)))
	];

	private static XDocument LoadFromPath(string path)
	{
		using var reader = System.Xml.XmlReader.Create(path, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit });
		return XDocument.Load(reader);
	}

	internal static IReadOnlyList<string> PackageVersions(XDocument document, string? prefix = null) =>
		Elements(document, "PackageVersion")
			.Select(element => (string?)element.Attribute("Include"))
			.Where(include => include is not null && (prefix is null || include.StartsWith(prefix, StringComparison.Ordinal)))
			.Cast<string>()
			.ToArray();

	internal static string? PackageVersion(XDocument document, string include) =>
		Elements(document, "PackageVersion").FirstOrDefault(element => (string?)element.Attribute("Include") == include)?
											.Attribute("Version")?
											.Value;

	internal static IReadOnlyList<string> PackageReferences(XDocument document) =>
		AttributeValues(document, "PackageReference", "Include");

	internal static IReadOnlyList<string> ProjectReferences(XDocument document) =>
		AttributeValues(document, "ProjectReference", "Include");

	internal static bool HasProjectReference(XDocument document, string projectName) =>
		ProjectReferences(document)
			.Any(include => include.EndsWith($"{projectName}\\{projectName}.csproj", StringComparison.Ordinal) ||
							include.EndsWith($"{projectName}/{projectName}.csproj", StringComparison.Ordinal));

	internal static bool HasPackageReference(XDocument document, string packageId) =>
		PackageReferences(document)
			.Contains(packageId, StringComparer.Ordinal);

	internal static bool HasPrivateAssets(XDocument document) =>
		Elements(document, "PrivateAssets").Any() ||
		Elements(document, "PackageReference").Any(element => element.Attribute("PrivateAssets") is not null);

	internal static IReadOnlyList<string> FrameworkReferences(XDocument document) =>
		AttributeValues(document, "FrameworkReference", "Include");

	internal static string? Sdk(XDocument document) =>
		document.Root?.Attribute("Sdk")
				?.Value;

	internal static IEnumerable<string> FileNames() =>
		Directory.EnumerateFiles(SolutionDirectory, "*", SearchOption.AllDirectories)
				 .Where(path => !IsBuildArtifact(path))
				 .Select(Path.GetFileName)
				 .Where(name => name is not null)
				 .Cast<string>();

	private static IReadOnlyList<string> AttributeValues(XDocument document, string elementName, string attributeName) =>
	[
		.. Elements(document, elementName)
		   .Select(element => (string?)element.Attribute(attributeName))
		   .Where(value => value is not null)
		   .Cast<string>()
	];

	private static IEnumerable<XElement> Elements(XDocument document, string name) =>
		document.Descendants()
				.Where(element => element.Name.LocalName == name);

	private static bool IsBuildArtifact(string path) =>
		path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
		path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

	private static string FindSolutionDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;

		throw new DirectoryNotFoundException("Could not locate Monaco.Template.Backend.slnx from the test output directory.");
	}
}