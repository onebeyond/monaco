namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class LegacyTelemetryInventoryTests
{
	private static readonly string[] ForbiddenInventory =
	[
		"Serilog",
		"ApplicationInsights",
		"AuditEntry",
		"AuditLog",
		"INonAuditable",
		"AuditEntries",
		"AuditTo",
		"OperationIdEnricher",
		"operationId",
		"parentId",
		"AddSerilog",
		"UseSerilog"
	];

	[Fact(DisplayName = "Template source does not contain retired telemetry or dedicated audit inventory")]
	public void TemplateSourceDoesNotContainRetiredTelemetryOrDedicatedAuditInventory()
	{
		var solutionDirectory = FindSolutionDirectory();
		var sourceFiles = Directory.EnumerateFiles(solutionDirectory, "*", SearchOption.AllDirectories)
							   .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
							   .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
							   .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
							   .Where(path => !path.EndsWith(nameof(LegacyTelemetryInventoryTests) + ".cs", StringComparison.Ordinal));

		var matches = sourceFiles.SelectMany(path => ForbiddenInventory
			.Where(forbidden => File.ReadAllText(path).Contains(forbidden, StringComparison.OrdinalIgnoreCase))
			.Select(forbidden => $"{Path.GetRelativePath(solutionDirectory, path)}: {forbidden}"));

		Assert.Empty(matches);
	}

	private static string FindSolutionDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;
		}

		throw new DirectoryNotFoundException("Could not locate Monaco.Template.Backend.slnx from the test output directory.");
	}
}