using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Monaco.Eng.RuntimeShapeVerification.Infrastructure;

[ExcludeFromCodeCoverage]
public sealed class OtlpSignalReceiver : IAsyncDisposable
{
	private readonly WebApplication _application;
	private readonly ConcurrentQueue<OtlpEntry> _entries;
	private readonly IContainer? _grpcBridge;
	private readonly string? _bridgeConfigPath;

	private OtlpSignalReceiver(WebApplication application,
							   ConcurrentQueue<OtlpEntry> entries,
							   IContainer? grpcBridge,
							   string? bridgeConfigPath)
	{
		_application = application;
		_entries = entries;
		_grpcBridge = grpcBridge;
		_bridgeConfigPath = bridgeConfigPath;
		HttpEndpoint = application.Urls.Single();
	}

	public string HttpEndpoint { get; }

	public static async Task<OtlpSignalReceiver> StartDevelopmentAsync()
	{
		await FreeHostPortAsync();
		var http = await StartHttpCoreAsync();
		var hostPort = new Uri(http.HttpEndpoint).Port;
		var config = $$"""
					   receivers:
					     otlp:
					       protocols:
					         grpc:
					           endpoint: 0.0.0.0:4317
					   exporters:
					     otlphttp:
					       endpoint: http://host.docker.internal:{{hostPort}}
					       compression: none
					       tls:
					         insecure: true
					   service:
					     pipelines:
					       traces:
					         receivers: [otlp]
					         exporters: [otlphttp]
					       metrics:
					         receivers: [otlp]
					         exporters: [otlphttp]
					       logs:
					         receivers: [otlp]
					         exporters: [otlphttp]
					   """;
		var configPath = Path.Combine(Path.GetTempPath(), $"otel-bridge-{Guid.NewGuid():N}.yaml");
		await File.WriteAllTextAsync(configPath, config);
		var bridge = new ContainerBuilder("otel/opentelemetry-collector-contrib:0.120.0").WithPortBinding(4317, 4317)
																						 .WithBindMount(configPath, "/etc/otelcol-contrib/config.yaml")
																						 .WithCommand("--config=/etc/otelcol-contrib/config.yaml")
																						 .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Everything is ready"))
																						 .Build();
		await bridge.StartAsync();
		return new(http._application, http._entries, bridge, configPath);
	}

	private static async Task FreeHostPortAsync()
	{
		// Prior aborted runs leave otelcol on :4317; reuse would fail DA binding.
		try
		{
			var list = new System.Diagnostics.ProcessStartInfo("docker", "ps -q --filter ancestor=otel/opentelemetry-collector-contrib:0.120.0")
						{
							RedirectStandardOutput = true,
							UseShellExecute = false,
							CreateNoWindow = true
						};
			using var listProcess = System.Diagnostics.Process.Start(list);
			if (listProcess is not null)
			{
				var ids = await listProcess.StandardOutput.ReadToEndAsync();
				await listProcess.WaitForExitAsync();
				foreach (var id in ids.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
				{
					using var rm = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", $"rm -f {id}")
																	{
																		RedirectStandardOutput = true,
																		RedirectStandardError = true,
																		UseShellExecute = false,
																		CreateNoWindow = true
																	});
					if (rm is not null)
						await rm.WaitForExitAsync();
				}
			}
		}
		catch
		{
			// Best-effort cleanup only.
		}
	}

	public static Task<OtlpSignalReceiver> StartProductionAsync() =>
		StartHttpCoreAsync();

	private static async Task<OtlpSignalReceiver> StartHttpCoreAsync()
	{
		var entries = new ConcurrentQueue<OtlpEntry>();
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		var app = builder.Build();
		app.MapPost("/{**path}", async context =>
								 {
									 await using var body = new MemoryStream();
									 await context.Request.Body.CopyToAsync(body);
									 entries.Enqueue(new(context.Request.Path, body.ToArray()));
									 context.Response.StatusCode = StatusCodes.Status200OK;
								 });
		await app.StartAsync();
		return new(app, entries, null, null);
	}

	public IReadOnlyDictionary<string, string?> ProductionOverrideConfiguration(string? headers = null)
	{
		// PO may only retarget via OTEL_EXPORTER_OTLP_*; no other OTEL_* developer overrides.
		var values = new Dictionary<string, string?>
					 {
						 ["OTEL_EXPORTER_OTLP_ENDPOINT"] = HttpEndpoint,
						 ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
					 };
		if (!string.IsNullOrEmpty(headers))
			values["OTEL_EXPORTER_OTLP_HEADERS"] = headers;

		return values;
	}

	public void Clear() =>
		_entries.Clear();

	public async Task WaitUntilAsync(Func<bool> predicate, int attempts = 150)
	{
		for (var attempt = 0; attempt < attempts && !predicate(); attempt++)
			await Task.Delay(100);

		if (!predicate())
			throw new TimeoutException("OTLP receiver did not observe the expected payload in time.");
	}

	public IReadOnlyCollection<string> GetServiceNames() =>
	[
		.. GetSpans().Select(span => span.ServiceName)
					 .Concat(GetMetrics().Select(metric => metric.ServiceName))
					 .Where(name => name.Length > 0)
					 .Distinct(StringComparer.Ordinal)
	];

	public bool HasSignalKindsFor(string serviceName) =>
		GetSpans().Any(span => span.ServiceName == serviceName) &&
		GetMetrics().Any(metric => metric.ServiceName == serviceName) &&
		GetLogs().Any(log => log.ServiceName == serviceName);

	public bool HasCounter(string serviceName, string meterName, string instrumentName) =>
		GetMetrics()
			.Where(resource => resource.ServiceName == serviceName)
			.SelectMany(resource => resource.Instruments)
			.Any(instrument => instrument.MeterName == meterName && instrument.Name == instrumentName);

	private IReadOnlyList<DecodedSpan> GetSpans() =>
	[
		.. _entries.Where(entry => entry.Path == "/v1/traces")
				   .SelectMany(entry => SafeDecode(() => OtlpProtobuf.ReadSpans(entry.Payload)))
	];

	private IReadOnlyList<DecodedLogRecord> GetLogs() =>
	[
		.. _entries.Where(entry => entry.Path == "/v1/logs")
				   .SelectMany(entry => SafeDecode(() => OtlpProtobuf.ReadLogs(entry.Payload)))
	];

	private IReadOnlyList<DecodedMetricsResource> GetMetrics() =>
	[
		.. _entries.Where(entry => entry.Path == "/v1/metrics")
				   .SelectMany(entry => SafeDecode(() => OtlpProtobuf.ReadMetrics(entry.Payload)))
	];

	private static IEnumerable<T> SafeDecode<T>(Func<IReadOnlyList<T>> decode)
	{
		try
		{
			return decode();
		}
		catch (InvalidOperationException)
		{
			return [];
		}
		catch (InvalidProtocolBufferException)
		{
			return [];
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _application.DisposeAsync();
		if (_grpcBridge is not null)
			await _grpcBridge.DisposeAsync();
		if (_bridgeConfigPath is not null)
			try
			{
				File.Delete(_bridgeConfigPath);
			}
			catch
			{
				// best effort
			}
	}

	private sealed record OtlpEntry(string Path, byte[] Payload);
}

[ExcludeFromCodeCoverage]
internal sealed record DecodedSpan(string ServiceName, string ScopeName, string Name);

[ExcludeFromCodeCoverage]
internal sealed record DecodedLogRecord(string ServiceName, string TraceId, string Body);

[ExcludeFromCodeCoverage]
internal sealed record DecodedMetricsResource(string ServiceName, IReadOnlyList<DecodedInstrument> Instruments);

[ExcludeFromCodeCoverage]
internal sealed record DecodedInstrument(string MeterName, string Name);

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

				spans.AddRange(spanPayloads.Select(spanPayload =>
												   {
													   var name = string.Empty;
													   foreach (var (field, message) in ReadFields(spanPayload))
														   if (field == 5)
															   name = Encoding.UTF8.GetString(message);
													   return new DecodedSpan(serviceName,
																			  scopeName,
																			  name);
												   }));
			}
		}

		return spans;
	}

	internal static IReadOnlyList<DecodedLogRecord> ReadLogs(byte[] payload)
	{
		var logs = new List<DecodedLogRecord>();
		foreach (var resourceLogs in ReadRepeatedMessages(payload, fieldNumber: 1))
		{
			var serviceName = string.Empty;
			foreach (var (field, message) in ReadFields(resourceLogs))
				switch (field)
				{
					case 1:
						serviceName = ReadServiceName(message);
						break;
					case 2:
						foreach (var (scopeField, scopeMessage) in ReadFields(message))
							if (scopeField == 2)
							{
								var body = string.Empty;
								var traceId = string.Empty;
								foreach (var (logField, logMessage) in ReadFields(scopeMessage))
									switch (logField)
									{
										case 5:
											body = ReadAnyValue(logMessage);
											break;
										case 9:
											traceId = Convert.ToHexString(logMessage).ToLowerInvariant();
											break;
									}

								logs.Add(new(serviceName,
											 traceId,
											 body));
							}

						break;
				}
		}

		return logs;
	}

	internal static IReadOnlyList<DecodedMetricsResource> ReadMetrics(byte[] payload)
	{
		var resources = new List<DecodedMetricsResource>();
		foreach (var resourceMetrics in ReadRepeatedMessages(payload, fieldNumber: 1))
		{
			var serviceName = string.Empty;
			var instruments = new List<DecodedInstrument>();
			foreach (var (field, message) in ReadFields(resourceMetrics))
				switch (field)
				{
					case 1:
						serviceName = ReadServiceName(message);
						break;
					case 2:
						var meterName = string.Empty;
						foreach (var (scopeField, scopeMessage) in ReadFields(message))
							switch (scopeField)
							{
								case 1:
									meterName = ReadInstrumentationScopeName(scopeMessage);
									break;
								case 2:
									foreach (var (metricField, metricMessage) in ReadFields(scopeMessage))
										if (metricField == 1)
											instruments.Add(new(meterName, Encoding.UTF8.GetString(metricMessage)));
									break;
							}

						break;
				}

			resources.Add(new(serviceName, instruments));
		}

		return resources;
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

		return new(key, value);
	}

	private static string ReadAnyValue(byte[] payload)
	{
		foreach (var (field, message) in ReadFields(payload))
			return field switch
				   {
					   1 => Encoding.UTF8.GetString(message),
					   2 => message.Length > 0 && message[0] != 0 ? bool.TrueString : bool.FalseString,
					   3 => ReadVarint(message).ToString(),
					   4 => BitConverter.ToDouble(message, 0).ToString(CultureInfo.InvariantCulture),
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
}