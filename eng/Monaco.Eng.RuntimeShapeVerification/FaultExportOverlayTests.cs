using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
public sealed class FaultExportOverlayTests
{
	[Fact(DisplayName = "Fault overlay drops inherited per-signal OTLP endpoints")]
	public void OverlayDropsInheritedSignalEndpoints()
	{
		var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
						  {
							  ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
							  ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc",
							  ["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = "http://inherited/traces",
							  ["OTEL_EXPORTER_OTLP_TRACES_PROTOCOL"] = "grpc",
							  ["OTEL_EXPORTER_OTLP_METRICS_ENDPOINT"] = "http://inherited/metrics",
							  ["OTEL_EXPORTER_OTLP_METRICS_PROTOCOL"] = "grpc",
							  ["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] = "http://inherited/logs",
							  ["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"] = "grpc"
						  };
		var overlay = new Dictionary<string, string?>(StringComparer.Ordinal)
					  {
						  ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:9",
						  ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
					  };

		ProcessHost.ApplyExportOverlay(environment, overlay);

		environment.Should().NotContainKey("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
		environment.Should().NotContainKey("OTEL_EXPORTER_OTLP_TRACES_PROTOCOL");
		environment.Should().NotContainKey("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
		environment.Should().NotContainKey("OTEL_EXPORTER_OTLP_METRICS_PROTOCOL");
		environment.Should().NotContainKey("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT");
		environment.Should().NotContainKey("OTEL_EXPORTER_OTLP_LOGS_PROTOCOL");
		environment["OTEL_EXPORTER_OTLP_ENDPOINT"].Should().Be("http://127.0.0.1:9");
		environment["OTEL_EXPORTER_OTLP_PROTOCOL"].Should().Be("http/protobuf");
	}
}
