using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
internal static class FaultCharacterization
{
	private static readonly Lock Gate = new();

	// Samples describe the run. Assertions do not read this file.
	internal static string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "monaco-fault-characterization.jsonl");

	internal static void Append(FaultCharacterizationSample sample)
	{
		var line = JsonSerializer.Serialize(sample, FaultReceiverJsonOptions.Options) + Environment.NewLine;
		lock (Gate)
			File.AppendAllText(Path, line);
	}
}

internal sealed record FaultCharacterizationSample(string Profile,
												   string Protocol,
												   long[] SoakRssSamples,
												   long HealthyRss,
												   long AdditionalOutageRss,
												   int TelemetryLossCount,
												   long BusinessCallLatencyMilliseconds,
												   long? RecoveryDurationMilliseconds);
