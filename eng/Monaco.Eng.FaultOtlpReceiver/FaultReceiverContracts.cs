using System.Text.Json;

namespace Monaco.Eng.FaultOtlpReceiver;

public static class FaultCanaries
{
	public const string Credential = "otlp-fault-canary-credential-4b91c0e2";

	public const string Prose = "otlp-fault-canary-prose-export-denied-by-receiver-policy";
}

public sealed record FaultReceiverStatus(string Mode, int Accepted, int Completed, int InFlight, int Retained);

public sealed record FaultPayload(string Path, byte[] Body);

public sealed record FaultDataResponse(int Status, byte[] Body);

public static class FaultReceiverJson
{
	public static JsonSerializerOptions Options { get; } = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};
}
