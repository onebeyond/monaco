using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Monaco.Template.Backend.IntegrationTests;

[ExcludeFromCodeCoverage]
internal sealed class OtlpLoopback : IAsyncDisposable
{
	private readonly WebApplication _application;
	private readonly ConcurrentQueue<OtlpEntry> _entries;

	private OtlpLoopback(WebApplication application, ConcurrentQueue<OtlpEntry> entries)
	{
		_application = application;
		_entries = entries;
	}

	internal string Endpoint => _application.Urls.Single();
	internal IReadOnlyCollection<OtlpEntry> Entries => _entries;

	internal static Dictionary<string, string?> ExporterConfiguration(string endpoint, string? tracesEndpoint = null)
	{
		var values = new Dictionary<string, string?>
					 {
						 ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
						 ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
						 ["OTEL_BSP_SCHEDULE_DELAY"] = "1",
						 ["OTEL_BLRP_SCHEDULE_DELAY"] = "1",
						 ["OTEL_METRIC_EXPORT_INTERVAL"] = "1"
					 };
		if (tracesEndpoint is not null)
		{
			var tracesUri = tracesEndpoint.TrimEnd('/');
			if (!tracesUri.EndsWith("/v1/traces", StringComparison.OrdinalIgnoreCase))
				tracesUri += "/v1/traces";
			values["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = tracesUri;
		}

		return values;
	}

	internal static async Task<OtlpLoopback> StartAsync()
	{
		var captured = new ConcurrentQueue<OtlpEntry>();
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		var webApplication = builder.Build();
		webApplication.MapPost("/{**path}", async context =>
											{
												await using var body = new MemoryStream();
												await context.Request.Body.CopyToAsync(body);
												captured.Enqueue(new OtlpEntry(context.Request.Path, body.ToArray()));
												context.Response.StatusCode = StatusCodes.Status200OK;
											});
		await webApplication.StartAsync();
		return new OtlpLoopback(webApplication, captured);
	}

	internal bool HasPath(string path) =>
		_entries.Any(entry => entry.Path == path);

	internal async Task WaitForSignalsAsync() =>
		await WaitUntilAsync(() => _entries.Select(entry => entry.Path).Distinct().Count() >= 3);

	internal async Task WaitForTraceResourcesAsync() =>
		await WaitUntilAsync(() =>
							 {
								 try
								 {
									 return GetTraceResourceServiceNames().Distinct(StringComparer.Ordinal).Count() >= 2;
								 }
								 catch (InvalidProtocolBufferException)
								 {
									 return false;
								 }
								 catch (InvalidOperationException)
								 {
									 return false;
								 }
							 });

	internal async Task WaitUntilAsync(Func<bool> predicate, int attempts = 100)
	{
		for (var attempt = 0; attempt < attempts && !predicate(); attempt++)
			await Task.Delay(100);

		if (!predicate())
			throw new TimeoutException("OTLP loopback did not observe the expected payload in time.");
	}

	internal IReadOnlyCollection<string> GetTraceResourceServiceNames() =>
	[
		.. DecodeTraces().Select(span => span.ServiceName)
						 .Where(serviceName => serviceName.Length > 0)
						 .Distinct(StringComparer.Ordinal)
	];

	internal IReadOnlyList<DecodedSpan> GetSpans() =>
	[
		.. _entries.Where(entry => entry.Path == "/v1/traces")
				   .SelectMany(entry => OtlpProtobuf.ReadSpans(entry.Payload))
	];

	internal IReadOnlyList<DecodedLogRecord> GetLogs() =>
	[
		.. _entries.Where(entry => entry.Path == "/v1/logs")
				   .SelectMany(entry => OtlpProtobuf.ReadLogs(entry.Payload))
	];

	internal IReadOnlyList<DecodedMetricsResource> GetMetrics() =>
	[
		.. _entries.Where(entry => entry.Path == "/v1/metrics")
				   .SelectMany(entry => OtlpProtobuf.ReadMetrics(entry.Payload))
	];

	internal bool HasHttpClientSpanTargetingEndpoint() =>
		HasHttpClientSpanTargeting(Endpoint);

	internal static bool TargetsEndpoint(DecodedSpan span, string endpoint)
	{
		if (!span.ScopeName.Contains("Http", StringComparison.Ordinal) && span.ScopeName != "System.Net.Http")
			return false;

		var target = new Uri(endpoint);
		var requestUrl = span.GetAttribute("url.full") ?? span.GetAttribute("http.url");
		if (Uri.TryCreate(requestUrl, UriKind.Absolute, out var requestUri))
			return requestUri.Scheme == target.Scheme && requestUri.Host == target.Host && requestUri.Port == target.Port;

		var address = span.GetAttribute("server.address") ?? span.GetAttribute("net.peer.name");
		var port = span.GetAttribute("server.port") ?? span.GetAttribute("net.peer.port");
		return address == target.Host && (port is null || port == target.Port.ToString());
	}

	internal bool HasHttpClientSpanTargeting(string endpoint) =>
		GetSpans().Any(span => TargetsEndpoint(span, endpoint));

	internal async Task WaitUntilSettledWithoutSelfTrafficAsync(params string[] extraEndpoints)
	{
		string[] endpoints = extraEndpoints.Length == 0 ? [Endpoint] : [Endpoint, .. extraEndpoints];
		for (var attempt = 0; attempt < 20; attempt++)
		{
			if (endpoints.Any(HasHttpClientSpanTargeting))
				throw new InvalidOperationException("OTLP loopback observed HttpClient traffic targeting the collector.");

			await Task.Delay(100);
		}
	}

	private IReadOnlyList<DecodedSpan> DecodeTraces() => GetSpans();

	public ValueTask DisposeAsync() => _application.DisposeAsync();
}

[ExcludeFromCodeCoverage]
internal sealed record OtlpEntry(string Path, byte[] Payload);

[ExcludeFromCodeCoverage]
internal sealed record DecodedSpan(string ScopeName,
								   string Name,
								   string TraceId,
								   string SpanId,
								   string ServiceName,
								   IReadOnlyList<KeyValuePair<string, string>> Attributes,
								   IReadOnlyList<DecodedSpanEvent> Events)
{
	internal string? GetAttribute(string key) =>
		Attributes.Where(attribute => attribute.Key == key).Select(attribute => attribute.Value).FirstOrDefault();
}

[ExcludeFromCodeCoverage]
internal sealed record DecodedSpanEvent(string Name, IReadOnlyList<KeyValuePair<string, string>> Attributes)
{
	internal string? GetAttribute(string key) =>
		Attributes.Where(attribute => attribute.Key == key).Select(attribute => attribute.Value).FirstOrDefault();
}

[ExcludeFromCodeCoverage]
internal sealed record DecodedLogRecord(
	string TraceId,
	string SpanId,
	string Body,
	IReadOnlyList<KeyValuePair<string, string>> Attributes)
{
	internal string? GetAttribute(string key) =>
		Attributes.Where(attribute => attribute.Key == key).Select(attribute => attribute.Value).FirstOrDefault();
}

[ExcludeFromCodeCoverage]
internal sealed record DecodedMetricsResource(string ServiceName);

[ExcludeFromCodeCoverage]
internal static class OtlpProtobuf
{
	internal static IReadOnlyList<DecodedSpan> ReadSpans(byte[] payload)
	{
		var spans = new List<DecodedSpan>();
		foreach (var resourceSpans in ReadRepeatedMessages(payload, fieldNumber: 1))
		{
			var serviceName = "";
			var scopeSpans = new List<byte[]>();
			foreach (var (field, message) in ReadFields(resourceSpans))
				switch (field)
				{
					case 1:
						serviceName = ReadServiceName(message);
						break;
					case 2:
						scopeSpans.Add(message);
						break;
				}

			foreach (var scopePayload in scopeSpans)
			{
				var scopeName = "";
				var spanPayloads = new List<byte[]>();
				foreach (var (scopeField, scopeMessage) in ReadFields(scopePayload))
					switch (scopeField)
					{
						case 1:
							scopeName = ReadInstrumentationScopeName(scopeMessage);
							break;
						case 2:
							spanPayloads.Add(scopeMessage);
							break;
					}

				foreach (var spanPayload in spanPayloads)
					spans.Add(ReadSpan(spanPayload, scopeName, serviceName));
			}
		}

		return spans;
	}

	internal static IReadOnlyList<DecodedLogRecord> ReadLogs(byte[] payload)
	{
		var logs = new List<DecodedLogRecord>();
		foreach (var resourceLogs in ReadRepeatedMessages(payload, fieldNumber: 1))
			foreach (var (field, message) in ReadFields(resourceLogs))
				if (field == 2)
					foreach (var (scopeField, scopeMessage) in ReadFields(message))
						if (scopeField == 2)
							logs.Add(ReadLogRecord(scopeMessage));

		return logs;
	}

	internal static IReadOnlyList<DecodedMetricsResource> ReadMetrics(byte[] payload)
	{
		var resources = new List<DecodedMetricsResource>();
		foreach (var resourceMetrics in ReadRepeatedMessages(payload, fieldNumber: 1))
		{
			var serviceName = "";
			foreach (var (field, message) in ReadFields(resourceMetrics))
				if (field == 1)
					serviceName = ReadServiceName(message);

			resources.Add(new DecodedMetricsResource(serviceName));
		}

		return resources;
	}

	private static DecodedSpan ReadSpan(byte[] payload, string scopeName, string serviceName)
	{
		var name = "";
		var traceId = "";
		var spanId = "";
		var attributes = new List<KeyValuePair<string, string>>();
		var events = new List<DecodedSpanEvent>();
		foreach (var (field, message) in ReadFields(payload))
			switch (field)
			{
				case 1:
					traceId = ToId(message);
					break;
				case 2:
					spanId = ToId(message);
					break;
				case 5:
					name = Encoding.UTF8.GetString(message);
					break;
				case 9:
					attributes.Add(ReadKeyValue(message));
					break;
				case 11:
					events.Add(ReadEvent(message));
					break;
			}

		return new DecodedSpan(scopeName, name, traceId, spanId, serviceName, attributes, events);
	}

	private static DecodedSpanEvent ReadEvent(byte[] payload)
	{
		var name = "";
		var attributes = new List<KeyValuePair<string, string>>();
		foreach (var (field, message) in ReadFields(payload))
			switch (field)
			{
				case 2:
					name = Encoding.UTF8.GetString(message);
					break;
				case 3:
					attributes.Add(ReadKeyValue(message));
					break;
			}

		return new DecodedSpanEvent(name, attributes);
	}

	private static DecodedLogRecord ReadLogRecord(byte[] payload)
	{
		var body = "";
		var traceId = "";
		var spanId = "";
		var attributes = new List<KeyValuePair<string, string>>();
		foreach (var (field, message) in ReadFields(payload))
			switch (field)
			{
				case 5:
					body = ReadAnyValue(message);
					break;
				case 6:
					attributes.Add(ReadKeyValue(message));
					break;
				case 9:
					traceId = ToId(message);
					break;
				case 10:
					spanId = ToId(message);
					break;
			}

		return new DecodedLogRecord(traceId, spanId, body, attributes);
	}

	private static string ReadServiceName(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			if (field == 1)
			{
				var attribute = ReadKeyValue(message);
				if (attribute.Key == "service.name")
					return attribute.Value;
			}

		return "";
	}

	private static string ReadInstrumentationScopeName(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			if (field == 1)
				return Encoding.UTF8.GetString(message);

		return "";
	}

	private static KeyValuePair<string, string> ReadKeyValue(byte[] payload)
	{
		var key = "";
		var value = "";
		foreach (var (field, message) in ReadFields(payload))
			switch (field)
			{
				case 1:
					key = Encoding.UTF8.GetString(message);
					break;
				case 2:
					value = ReadAnyValue(message);
					break;
			}

		return new KeyValuePair<string, string>(key, value);
	}

	private static string ReadAnyValue(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			return field switch
				   {
					   1 => Encoding.UTF8.GetString(message),
					   2 => ReadBool(message) ? bool.TrueString : bool.FalseString,
					   3 => ReadVarint(message).ToString(),
					   4 => BitConverter.ToDouble(message, 0).ToString(CultureInfo.InvariantCulture),
					   5 => string.Join("\n", ReadRepeatedMessages(message, fieldNumber: 1).Select(ReadAnyValue)),
					   6 => string.Join("\n", ReadRepeatedMessages(message, fieldNumber: 1).Select(item =>
																								   {
																									   var pair = ReadKeyValue(item);
																									   return $"{pair.Key}={pair.Value}";
																								   })),
					   7 => Convert.ToHexString(message),
					   _ => Convert.ToHexString(message)
				   };

		return "";
	}

	private static IEnumerable<byte[]> ReadRepeatedMessages(byte[] payload, int fieldNumber) =>
		ReadFields(payload).Where(field => field.Number == fieldNumber).Select(field => field.Message);

	private static IEnumerable<(int Number, byte[] Message)> ReadFields(byte[] payload)
	{
		var input = new CodedInputStream(payload);
		while (!input.IsAtEnd)
		{
			var tag = input.ReadTag();
			if (tag == 0)
				yield break;

			var number = WireFormat.GetTagFieldNumber(tag);
			yield return (number, ReadFieldValue(input, tag));
		}
	}

	private static byte[] ReadFieldValue(CodedInputStream input, uint tag) =>
		WireFormat.GetTagWireType(tag) switch
		{
			WireFormat.WireType.Varint => EncodeVarint(input.ReadUInt64()),
			WireFormat.WireType.Fixed64 => BitConverter.GetBytes(input.ReadFixed64()),
			WireFormat.WireType.LengthDelimited => input.ReadBytes().ToByteArray(),
			WireFormat.WireType.Fixed32 => BitConverter.GetBytes(input.ReadFixed32()),
			_ => throw new InvalidOperationException($"Unsupported OTLP wire type: {WireFormat.GetTagWireType(tag)}")
		};

	private static bool ReadBool(byte[] message) =>
		message.Length > 0 && message[0] != 0;

	private static ulong ReadVarint(byte[] message)
	{
		var input = new CodedInputStream(message);
		return input.ReadUInt64();
	}

	private static byte[] EncodeVarint(ulong value)
	{
		using var stream = new MemoryStream(10);
		var output = new CodedOutputStream(stream);
		output.WriteUInt64(value);
		output.Flush();
		return stream.ToArray();
	}

	private static string ToId(byte[] bytes) =>
		Convert.ToHexString(bytes).ToLowerInvariant();
}