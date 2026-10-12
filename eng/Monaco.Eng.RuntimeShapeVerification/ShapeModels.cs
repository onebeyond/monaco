namespace Monaco.Eng.RuntimeShapeVerification;

public sealed record ShapeOptions(bool Api,
								  bool Worker,
								  bool Gateway,
								  bool Auth,
								  bool MassTransit,
								  bool Files,
								  bool Tests,
								  bool Common,
								  bool Keycloak);

public enum RuntimeShape
{
	/// <summary>API and Worker with auth, messaging, files, tests, and Keycloak. No Gateway. Registry cell R1.</summary>
	FullApiWorker,

	/// <summary>Full API and Worker plus the Gateway. Registry cell R2.</summary>
	GatewayFull,

	/// <summary>API and tests only. No auth, messaging, files, Worker, or Gateway. Registry cell R3.</summary>
	LeanApi,

	/// <summary>API that publishes messages and stores files. No Worker. Registry cell R4.</summary>
	PublisherApi,

	/// <summary>Worker that consumes ProductCreated. No API. Registry cell R5.</summary>
	WorkerConsumer,

	/// <summary>Worker with no messaging. Registry cell R6.</summary>
	WorkerBaseline,

	/// <summary>Full API and Worker with tests turned off. Registry cell R7.</summary>
	FullApiWorkerWithoutTests
}

public enum DestinationMode
{
	/// <summary>Development OTLP defaults with the receiver listening on :4317.</summary>
	ReceiverAvailable,

	/// <summary>Development OTLP defaults with nothing listening.</summary>
	ReceiverUnavailable,

	/// <summary>Same binaries; production OTLP overrides via OTEL_EXPORTER_OTLP_*.</summary>
	ProductionOverride
}

public sealed class GeneratedShape
{
	public GeneratedShape(RuntimeShape shape,
						  string outputDirectory,
						  ShapeOptions options)
	{
		Shape = shape;
		Name = shape.ToString();
		OutputDirectory = outputDirectory;
		Options = options;
		SolutionPath = Path.Combine(outputDirectory, $"{Name}.slnx");
	}

	public RuntimeShape Shape { get; }

	public string Id =>
		Name;

	public string Name { get; }
	public string OutputDirectory { get; }
	public ShapeOptions Options { get; }
	public string SolutionPath { get; }

	public string ProjectDir(string suffix) =>
		Path.Combine(OutputDirectory, $"{Name}.{suffix}");

	public string BuiltDll(string suffix) =>
		Path.Combine(ProjectDir(suffix),
					 "bin",
					 "Debug",
					 "net10.0",
					 $"{Name}.{suffix}.dll");

	public string ServiceName(string suffix) =>
		suffix switch
		{
			"Api" => $"{Name}.Api",
			"Worker" => $"{Name}.Worker",
			"Common.ApiGateway" => $"{Name}.Common.ApiGateway",
			_ => $"{Name}.{suffix}"
		};

	public string ApplicationMeterName =>
		$"{Name}.Application";

	public string ClusterApiAddress
	{
		get
		{
			var proxyPath = Path.Combine(ProjectDir("Common.ApiGateway"), "reverseProxy.json");
			if (!File.Exists(proxyPath))
				return "http://localhost:5400";

			var text = File.ReadAllText(proxyPath);
			const string marker = "\"Address\": \"";
			var start = text.IndexOf(marker, StringComparison.Ordinal);
			if (start < 0)
				return "http://localhost:5400";

			start += marker.Length;
			var end = text.IndexOf('"', start);
			return end < 0 ? "http://localhost:5400" : text[start..end];
		}
	}
}