namespace Monaco.Eng.GeneratedShapeHarness;

public sealed record EvidenceEnvelope(string SourceSha,
									  string PackageId,
									  string PackageVersion,
									  string PackageSha256,
									  string ShapeId,
									  ShapeOptions Options,
									  string ProtocolInvocation,
									  int? ExitCode,
									  string? WorkingDirectory,
									  string Kind,
									  string? Output);

public sealed class BuildAdapter
{
	private readonly GeneratedShapeHarness _harness;

	internal BuildAdapter(GeneratedShapeHarness harness) => _harness = harness;

	public Task<EvidenceEnvelope> RunAsync(GeneratedShape shape, CancellationToken cancellationToken) =>
		RunAsync(shape, configFile: null, cancellationToken);

	internal Task<EvidenceEnvelope> RunAsync(GeneratedShape shape, string? configFile, CancellationToken cancellationToken) =>
		_harness.RunBuildAsync(shape, configFile, cancellationToken);
}

public sealed class StartAdapter
{
	private readonly GeneratedShapeHarness _harness;

	internal StartAdapter(GeneratedShapeHarness harness) => _harness = harness;

	public Task<EvidenceEnvelope> RunAsync(GeneratedShape shape, CancellationToken cancellationToken) =>
		_harness.RunStartAsync(shape, cancellationToken);
}

public sealed class StaticInspectionAdapter
{
	private readonly GeneratedShapeHarness _harness;

	internal StaticInspectionAdapter(GeneratedShapeHarness harness) => _harness = harness;

	public EvidenceEnvelope Run(GeneratedShape shape) =>
		_harness.Inspect(shape);
}

public sealed class RuntimeAdapter
{
	private readonly GeneratedShapeHarness _harness;

	internal RuntimeAdapter(GeneratedShapeHarness harness) => _harness = harness;

	public EvidenceEnvelope Read(GeneratedShape shape) =>
		_harness.ReadEvidence(shape) with { Kind = "runtime" };

	public string WriteConnectionString(string connectionString) =>
		_harness.WriteConnectionString(connectionString);
}

public sealed class MigrationAdapter
{
	private readonly GeneratedShapeHarness _harness;

	internal MigrationAdapter(GeneratedShapeHarness harness) => _harness = harness;

	public Task<EvidenceEnvelope> RunAsync(GeneratedShape shape, CancellationToken cancellationToken) =>
		_harness.RunMigrationAsync(shape, cancellationToken);
}

public sealed class EvidenceAdapter
{
	private readonly GeneratedShapeHarness _harness;

	internal EvidenceAdapter(GeneratedShapeHarness harness) => _harness = harness;

	public EvidenceEnvelope Read(GeneratedShape shape) =>
		_harness.ReadEvidence(shape);
}

public sealed class ToolSlot
{
	private readonly GeneratedShapeHarness _harness;
	private readonly string _kind;

	internal ToolSlot(GeneratedShapeHarness harness, string kind)
	{
		_harness = harness;
		_kind = kind;
	}

	public Task<EvidenceEnvelope> RunAsync(GeneratedShape shape, CancellationToken cancellationToken) =>
		RunAsync(shape, workingDirectory: null, cancellationToken);

	public Task<EvidenceEnvelope> RunAsync(GeneratedShape shape, string? workingDirectory, CancellationToken cancellationToken) =>
		_harness.RunToolAsync(_kind, shape, workingDirectory, cancellationToken);
}
