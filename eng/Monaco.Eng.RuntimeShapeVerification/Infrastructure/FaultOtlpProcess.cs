using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Monaco.Eng.FaultOtlpReceiver;

namespace Monaco.Eng.RuntimeShapeVerification.Infrastructure;

[ExcludeFromCodeCoverage]
internal sealed class FaultOtlpProcess : IAsyncDisposable
{
	private readonly Process _process;
	private readonly HttpClient _control;
	private readonly StringBuilder _stdout = new();
	private readonly StringBuilder _stderr = new();
	private readonly Lock _outputGate = new();

	private FaultOtlpProcess(Process process, HttpClient control, string dataUrl)
	{
		_process = process;
		_control = control;
		DataUrl = dataUrl;
	}

	internal string DataUrl { get; }

	internal static async Task<FaultOtlpProcess> StartAsync()
	{
		var dll = Path.Combine(AppContext.BaseDirectory, "Monaco.Eng.FaultOtlpReceiver.dll");
		if (!File.Exists(dll))
			throw new FileNotFoundException("The fault receiver build output was not copied beside the suite.", dll);

		var startInfo = new ProcessStartInfo("dotnet", $"\"{dll}\"")
						{
							WorkingDirectory = AppContext.BaseDirectory,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true
						};
		startInfo.Environment.Remove("OTEL_EXPORTER_OTLP_ENDPOINT");
		startInfo.Environment.Remove("OTEL_EXPORTER_OTLP_PROTOCOL");
		startInfo.Environment.Remove("OTEL_EXPORTER_OTLP_HEADERS");

		var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {dll}");
		var stdout = new StringBuilder();
		var stderr = new StringBuilder();
		var gate = new Lock();
		string? dataUrl = null;
		string? controlUrl = null;
		var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		process.OutputDataReceived += (_, eventArgs) =>
									  {
										  if (eventArgs.Data is null)
											  return;

										  var line = eventArgs.Data.Trim().TrimStart('\uFEFF');
										  lock (gate)
										  {
											  stdout.AppendLine(line);
											  if (line.StartsWith("data=", StringComparison.Ordinal))
												  dataUrl = line["data=".Length..].Trim();
											  if (line.StartsWith("control=", StringComparison.Ordinal))
												  controlUrl = line["control=".Length..].Trim();
											  if (dataUrl is not null && controlUrl is not null)
												  ready.TrySetResult();
										  }
									  };
		process.ErrorDataReceived += (_, eventArgs) =>
									 {
										 if (eventArgs.Data is null)
											 return;

										 lock (gate)
											 stderr.AppendLine(eventArgs.Data);
									 };
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		process.Exited += (_, _) => ready.TrySetException(new InvalidOperationException("Fault receiver exited before publishing its URLs."));
		process.EnableRaisingEvents = true;

		var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(30)));
		if (completed != ready.Task || dataUrl is null || controlUrl is null)
		{
			var output = Snapshot(gate, stdout, stderr);
			TryKill(process);
			throw new TimeoutException($"Fault receiver did not publish its URLs. {output}");
		}

		await ready.Task;
		var control = new HttpClient
					  {
						  BaseAddress = new(controlUrl.TrimEnd('/') + "/"),
						  Timeout = TimeSpan.FromSeconds(10)
					  };
		var receiver = new FaultOtlpProcess(process, control, dataUrl);
		lock (gate)
		{
			receiver._stdout.Append(stdout);
			receiver._stderr.Append(stderr);
		}

		return receiver;
	}

	internal async Task SetModeAsync(string mode)
	{
		using var response = await _control.PostAsync("mode", new StringContent(mode, Encoding.UTF8, "text/plain"));
		response.EnsureSuccessStatusCode();
	}

	internal async Task<FaultReceiverStatus> GetStatusAsync()
	{
		var json = await _control.GetStringAsync("status");
		return JsonSerializer.Deserialize<FaultReceiverStatus>(json, FaultReceiverJsonOptions.Options)
			   ?? throw new InvalidOperationException("Fault receiver status was empty.");
	}

	internal async Task<IReadOnlyList<FaultPayload>> GetPayloadsAsync()
	{
		var json = await _control.GetStringAsync("payloads");
		return JsonSerializer.Deserialize<List<FaultPayload>>(json, FaultReceiverJsonOptions.Options) ?? [];
	}

	internal async Task<FaultDataResponse> GetResponseAsync()
	{
		var json = await _control.GetStringAsync("response");
		return JsonSerializer.Deserialize<FaultDataResponse>(json, FaultReceiverJsonOptions.Options)
			   ?? throw new InvalidOperationException("Fault receiver response was empty.");
	}

	internal async Task<bool> HasMetricForAsync(string serviceName)
	{
		foreach (var payload in await GetPayloadsAsync())
		{
			if (!payload.Path.Contains("v1/metrics", StringComparison.Ordinal))
				continue;

			try
			{
				if (OtlpProtobuf.ReadMetrics(payload.Body)
							   .Any(resource => resource.ServiceName == serviceName && resource.Instruments.Count > 0))
					return true;
			}
			catch (InvalidOperationException)
			{
			}
			catch (InvalidProtocolBufferException)
			{
			}
		}

		return false;
	}

	internal async Task<IReadOnlyCollection<string>> ObservedServiceNamesAsync()
	{
		var names = new HashSet<string>(StringComparer.Ordinal);
		foreach (var payload in await GetPayloadsAsync())
		{
			CollectNames(names, () => OtlpProtobuf.ReadSpans(payload.Body).Select(span => span.ServiceName));
			CollectNames(names, () => OtlpProtobuf.ReadLogs(payload.Body).Select(log => log.ServiceName));
			CollectNames(names, () => OtlpProtobuf.ReadMetrics(payload.Body).Select(metric => metric.ServiceName));
		}

		return names;
	}

	private static void CollectNames(HashSet<string> names, Func<IEnumerable<string>> read)
	{
		try
		{
			foreach (var name in read())
				if (name.Length > 0)
					names.Add(name);
		}
		catch (InvalidOperationException)
		{
		}
		catch (InvalidProtocolBufferException)
		{
		}
	}

	public async ValueTask DisposeAsync()
	{
		_control.Dispose();
		TryKill(_process);
		if (!_process.HasExited)
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try
			{
				await _process.WaitForExitAsync(timeout.Token);
			}
			catch (OperationCanceledException)
			{
				OrphanHostKiller.KillPidTree(_process.Id);
			}
		}

		_process.Dispose();
	}

	private static string Snapshot(Lock gate, StringBuilder stdout, StringBuilder stderr)
	{
		lock (gate)
			return stdout.ToString() + stderr.ToString();
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (!process.HasExited)
				process.Kill(entireProcessTree: true);
		}
		catch
		{
			// The process already left.
		}
	}
}

internal static class FaultReceiverJsonOptions
{
	internal static JsonSerializerOptions Options { get; } = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};
}
