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
		spans[0].Attributes.Should().BeEmpty();
		spans[0].Events.Should().BeEmpty();
		spans[1].Attributes.Should().BeEmpty();
		spans[1].Events.Should().BeEmpty();
	}

	[Fact]
	public void OtlpSpanIdentityDecodesAttributesEventsAndLogExceptionFields()
	{
		var traceId = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10 };
		var spanId = new byte[] { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28 };
		var payload = Traces(ResourceSpans("FullApiWorker.Api",
										   "Microsoft.AspNetCore",
										   Span(traceId,
												spanId,
												parentSpanId: null,
												"POST",
												SpanAttribute("http.request.method", "POST"),
												SpanInt64Attribute("http.response.status_code", 201),
												SpanAttribute("user.id", "subject-1"),
												Event("user.claim",
													  SpanAttribute("user.claim.type", "sub"),
													  SpanAttribute("user.claim.value", "subject-1")))));
		var logs = Logs(ResourceLogs("FullApiWorker.Api",
									 traceId,
									 "Company creation completed.",
									 SpanAttribute("{OriginalFormat}", "Company creation completed."),
									 SpanAttribute("user.claims", """[{"type":"sub"}]"""),
									 SpanAttribute("exception.type", "InvalidOperationException"),
									 SpanAttribute("exception.message", "native exception detail"),
									 SpanAttribute("exception.stacktrace", "at Handler.Handle()")));

		var spans = OtlpProtobuf.ReadSpans(payload);
		var records = OtlpProtobuf.ReadLogs(logs);

		spans.Should().ContainSingle();
		spans[0].Attributes.Should().Equal(new DecodedAttribute("http.request.method", "POST"),
										   new DecodedAttribute("http.response.status_code", "201"),
										   new DecodedAttribute("user.id", "subject-1"));
		spans[0].Events.Should().ContainSingle();
		spans[0].Events[0].Name.Should().Be("user.claim");
		spans[0].Events[0].Attributes.Should().Equal(new DecodedAttribute("user.claim.type", "sub"),
													 new DecodedAttribute("user.claim.value", "subject-1"));
		records.Should().ContainSingle();
		records[0].Body.Should().Be("Company creation completed.");
		records[0].TraceId.Should().Be(Convert.ToHexString(traceId).ToLowerInvariant());
		records[0].Attributes.Should().Equal(new DecodedAttribute("{OriginalFormat}", "Company creation completed."),
											 new DecodedAttribute("user.claims", """[{"type":"sub"}]"""),
											 new DecodedAttribute("exception.type", "InvalidOperationException"),
											 new DecodedAttribute("exception.message", "native exception detail"),
											 new DecodedAttribute("exception.stacktrace", "at Handler.Handle()"));
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

	private static byte[] Span(byte[] traceId, byte[] spanId, byte[]? parentSpanId, string name, params byte[][] fields)
	{
		var span = Message(output =>
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
		foreach (var field in fields)
			span = Concat(span, field);

		return span;
	}

	private static byte[] SpanAttribute(string key, string value) =>
		Embedded(9, Attribute(key, value));

	private static byte[] SpanInt64Attribute(string key, long value) =>
		Embedded(9,
				 Message(output =>
						 {
							 output.WriteTag(1, WireFormat.WireType.LengthDelimited);
							 output.WriteString(key);
							 WriteEmbedded(output,
										   2,
										   Message(nested =>
												   {
													   nested.WriteTag(3, WireFormat.WireType.Varint);
													   nested.WriteInt64(value);
												   }));
						 }));

	private static byte[] Event(string name, params byte[][] attributes)
	{
		var body = Message(output =>
						   {
							   output.WriteTag(2, WireFormat.WireType.LengthDelimited);
							   output.WriteString(name);
						   });
		foreach (var attribute in attributes)
		{
			var copy = attribute.ToArray();
			copy[0] = (byte)(3 << 3 | (int)WireFormat.WireType.LengthDelimited);
			body = Concat(body, copy);
		}

		return Embedded(11, body);
	}

	private static byte[] Logs(byte[] resourceLogs) =>
		Message(output => WriteEmbedded(output, 1, resourceLogs));

	private static byte[] ResourceLogs(string serviceName, byte[] traceId, string body, params byte[][] attributes)
	{
		var record = Message(output =>
							 {
								 WriteEmbedded(output,
											   5,
											   Message(any =>
													   {
														   any.WriteTag(1, WireFormat.WireType.LengthDelimited);
														   any.WriteString(body);
													   }));
								 output.WriteTag(9, WireFormat.WireType.LengthDelimited);
								 output.WriteBytes(ByteString.CopyFrom(traceId));
							 });
		foreach (var attribute in attributes)
			record = Concat(record, LogAttribute(attribute));

		return Message(output =>
					   {
						   WriteEmbedded(output, 1, Resource(serviceName));
						   WriteEmbedded(output,
										 2,
										 Message(scopeLogs =>
												 {
													 WriteEmbedded(scopeLogs,
																   1,
																   Message(scope =>
																		   {
																			   scope.WriteTag(1, WireFormat.WireType.LengthDelimited);
																			   scope.WriteString("Application");
																		   }));
													 WriteEmbedded(scopeLogs, 2, record);
												 }));
					   });
	}

	private static byte[] Concat(byte[] left, byte[] right)
	{
		var combined = new byte[left.Length + right.Length];
		left.CopyTo(combined, 0);
		right.CopyTo(combined, left.Length);
		return combined;
	}

	private static byte[] LogAttribute(byte[] spanAttribute)
	{
		// Span attributes are field 9. Log records store the same KeyValue on field 6.
		spanAttribute[0] = (byte)(6 << 3 | (int)WireFormat.WireType.LengthDelimited);
		return spanAttribute;
	}

	private static byte[] Embedded(int field, byte[] message) =>
		Message(output => WriteEmbedded(output, field, message));

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
