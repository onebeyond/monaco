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

	internal IReadOnlyCollection<string> GetMeterNames(string? serviceName = null) =>
	[
		.. GetMetrics()
		   .Where(resource => serviceName is null || string.Equals(resource.ServiceName, serviceName, StringComparison.Ordinal))
		   .SelectMany(resource => resource.MeterNames)
		   .Where(static name => name.Length > 0)
		   .Distinct(StringComparer.Ordinal)
	];

	internal IReadOnlyCollection<string> GetMeterNamesByServiceSuffix(string suffix) =>
	[
		.. GetMetrics()
		   .Where(resource => resource.ServiceName.EndsWith(suffix, StringComparison.Ordinal))
		   .SelectMany(resource => resource.MeterNames)
		   .Where(static name => name.Length > 0)
		   .Distinct(StringComparer.Ordinal)
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
								   string ParentSpanId,
								   string ServiceName,
								   int StatusCode,
								   IReadOnlyList<KeyValuePair<string, string>> Attributes,
								   IReadOnlyList<DecodedSpanEvent> Events,
								   IReadOnlyList<DecodedSpanLink> Links)
{
	internal bool IsError => StatusCode == 2;

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
internal sealed record DecodedSpanLink(string TraceId, string SpanId);

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
internal sealed record DecodedMetricsResource(string ServiceName, IReadOnlyList<string> MeterNames);

[ExcludeFromCodeCoverage]
internal static class HostMetricNames
{
	internal const string Runtime = "System.Runtime";
	internal const string HttpClient = "System.Net.Http";
	internal const string HttpClientInstrumentation = "OpenTelemetry.Instrumentation.Http";
	internal const string Application = "Monaco.Template.Backend.Application";
	internal const string MassTransit = "MassTransit";
	internal const string Authentication = "Microsoft.AspNetCore.Authentication";
	internal const string Authorization = "Microsoft.AspNetCore.Authorization";

	internal static bool IsAspNetCore(string name) =>
		name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal);

	internal static bool IsHttpClient(string name) =>
		name is (HttpClient) or HttpClientInstrumentation;

	internal static bool IsSqlClient(string name) =>
		name.Contains("SqlClient", StringComparison.Ordinal);

	internal static bool IsBlobOrYarp(string name) =>
		name.Contains("Azure", StringComparison.Ordinal) ||
		name.Contains("Blob", StringComparison.OrdinalIgnoreCase) ||
		name.Contains("Yarp", StringComparison.OrdinalIgnoreCase);
}

[ExcludeFromCodeCoverage]
internal static class OtlpProtobuf
{
	internal static IReadOnlyList<DecodedSpan> ReadSpans(byte[] payload)
	{
		var spans = new List<DecodedSpan>();
		foreach (var resourceSpans in ReadRepeatedMessages(payload, fieldNumber: 1))
		{
			var serviceName = string.Empty;
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
				var scopeName = string.Empty;
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

				spans.AddRange(spanPayloads.Select(spanPayload => ReadSpan(spanPayload, scopeName, serviceName)));
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
			var serviceName = string.Empty;
			var meterNames = new List<string>();
			foreach (var (field, message) in ReadFields(resourceMetrics))
				switch (field)
				{
					case 1:
						serviceName = ReadServiceName(message);
						break;
					case 2:
						var meterName = ReadScopeMetricsName(message);
						if (meterName.Length > 0)
							meterNames.Add(meterName);
						break;
				}

			resources.Add(new DecodedMetricsResource(serviceName, meterNames));
		}

		return resources;
	}

	private static string ReadScopeMetricsName(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			if (field == 1)
				return ReadInstrumentationScopeName(message);

		return string.Empty;
	}

	private static DecodedSpan ReadSpan(byte[] payload, string scopeName, string serviceName)
	{
		var name = string.Empty;
		var traceId = string.Empty;
		var spanId = string.Empty;
		var parentSpanId = string.Empty;
		var statusCode = 0;
		var attributes = new List<KeyValuePair<string, string>>();
		var events = new List<DecodedSpanEvent>();
		var links = new List<DecodedSpanLink>();
		foreach (var (field, message) in ReadFields(payload))
			switch (field)
			{
				case 1:
					traceId = ToId(message);
					break;
				case 2:
					spanId = ToId(message);
					break;
				case 4:
					parentSpanId = ToId(message);
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
				case 13:
					links.Add(ReadLink(message));
					break;
				case 15:
					statusCode = ReadStatusCode(message);
					break;
			}

		return new DecodedSpan(scopeName, name, traceId, spanId, parentSpanId, serviceName, statusCode, attributes, events, links);
	}

	private static int ReadStatusCode(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			if (field == 3)
				return (int)ReadVarint(message);

		return 0;
	}

	private static DecodedSpanLink ReadLink(byte[] payload)
	{
		var traceId = string.Empty;
		var spanId = string.Empty;
		foreach (var (field, message) in ReadFields(payload))
			switch (field)
			{
				case 1:
					traceId = ToId(message);
					break;
				case 2:
					spanId = ToId(message);
					break;
			}

		return new DecodedSpanLink(traceId, spanId);
	}

	private static DecodedSpanEvent ReadEvent(byte[] payload)
	{
		var name = string.Empty;
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
		var body = string.Empty;
		var traceId = string.Empty;
		var spanId = string.Empty;
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

		return string.Empty;
	}

	private static string ReadInstrumentationScopeName(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			if (field == 1)
				return Encoding.UTF8.GetString(message);

		return string.Empty;
	}

	private static KeyValuePair<string, string> ReadKeyValue(byte[] payload)
	{
		var key = string.Empty;
		var value = string.Empty;
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
					   _ => Convert.ToHexString(message)
				   };

		return string.Empty;
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