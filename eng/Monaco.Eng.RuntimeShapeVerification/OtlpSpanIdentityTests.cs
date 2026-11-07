using AwesomeAssertions;
using Google.Protobuf;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
public sealed class OtlpSpanIdentityTests
{
	[Fact]
	public void OtlpSpanIdentity()
	{
		var traceId = new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };
		var rootSpanId = new byte[] { 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x01, 0x02 };
		var childSpanId = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
		var payload = Traces(ResourceSpans("FullApiWorker.Api",
										   "System.Net.Http",
										   Span(traceId, rootSpanId, parentSpanId: null, "GET"),
										   Span(traceId, childSpanId, rootSpanId, "SQL")));

		var spans = OtlpProtobuf.ReadSpans(payload);

		spans.Should().HaveCount(2);
		spans[0].ServiceName.Should().Be("FullApiWorker.Api");
		spans[0].ScopeName.Should().Be("System.Net.Http");
		spans[0].Name.Should().Be("GET");
		spans[0].TraceId.Should().Be(Convert.ToHexString(traceId).ToLowerInvariant());
		spans[0].SpanId.Should().Be(Convert.ToHexString(rootSpanId).ToLowerInvariant());
		spans[0].ParentSpanId.Should().BeEmpty();
		spans[1].TraceId.Should().Be(spans[0].TraceId);
		spans[1].SpanId.Should().Be(Convert.ToHexString(childSpanId).ToLowerInvariant());
		spans[1].ParentSpanId.Should().Be(spans[0].SpanId);
		spans[1].Name.Should().Be("SQL");
	}

	private static byte[] Traces(byte[] resourceSpans) =>
		Message(output => WriteEmbedded(output, 1, resourceSpans));

	private static byte[] ResourceSpans(string serviceName, string scopeName, params byte[][] spans) =>
		Message(output =>
				{
					WriteEmbedded(output, 1, Resource(serviceName));
					WriteEmbedded(output, 2, ScopeSpans(scopeName, spans));
				});

	private static byte[] Resource(string serviceName) =>
		Message(output => WriteEmbedded(output, 1, Attribute("service.name", serviceName)));

	private static byte[] Attribute(string key, string value) =>
		Message(output =>
				{
					output.WriteTag(1, WireFormat.WireType.LengthDelimited);
					output.WriteString(key);
					WriteEmbedded(output, 2, Message(nested =>
													 {
														 nested.WriteTag(1, WireFormat.WireType.LengthDelimited);
														 nested.WriteString(value);
													 }));
				});

	private static byte[] ScopeSpans(string scopeName, byte[][] spans) =>
		Message(output =>
				{
					WriteEmbedded(output, 1, Message(scope =>
													 {
														 scope.WriteTag(1, WireFormat.WireType.LengthDelimited);
														 scope.WriteString(scopeName);
													 }));
					foreach (var span in spans)
						WriteEmbedded(output, 2, span);
				});

	private static byte[] Span(byte[] traceId, byte[] spanId, byte[]? parentSpanId, string name) =>
		Message(output =>
				{
					output.WriteTag(1, WireFormat.WireType.LengthDelimited);
					output.WriteBytes(ByteString.CopyFrom(traceId));
					output.WriteTag(2, WireFormat.WireType.LengthDelimited);
					output.WriteBytes(ByteString.CopyFrom(spanId));
					output.WriteTag(3, WireFormat.WireType.LengthDelimited);
					output.WriteString("vendor=state");
					if (parentSpanId is not null)
					{
						output.WriteTag(4, WireFormat.WireType.LengthDelimited);
						output.WriteBytes(ByteString.CopyFrom(parentSpanId));
					}

					output.WriteTag(5, WireFormat.WireType.LengthDelimited);
					output.WriteString(name);
				});

	private static void WriteEmbedded(CodedOutputStream output, int field, byte[] message)
	{
		output.WriteTag(field, WireFormat.WireType.LengthDelimited);
		output.WriteBytes(ByteString.CopyFrom(message));
	}

	private static byte[] Message(Action<CodedOutputStream> write)
	{
		using var stream = new MemoryStream();
		var output = new CodedOutputStream(stream);
		write(output);
		output.Flush();
		return stream.ToArray();
	}
}
