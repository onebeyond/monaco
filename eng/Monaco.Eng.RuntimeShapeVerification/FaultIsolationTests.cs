using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Monaco.Eng.FaultOtlpReceiver;
using Monaco.Eng.RuntimeShapeVerification.Drivers;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using Monaco.Eng.RuntimeShapeVerification.Workloads;
using Xunit;
using Xunit.Sdk;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
[Collection("RuntimeShapeVerification")]
[Trait("Runtime Shape Verification", "FAULT-ISOLATION")]
public sealed class FaultIsolationTests
{
	private static readonly string[] Protocols =
	[
		"refuse",
		"timeout",
		"401",
		"403",
		"429",
		"malformed",
		"saturate",
		"shutdown",
		"recovery"
	];

	private readonly ShapeHiveFixture _shapes;
	private readonly InfrastructureFixture _infra;

	public FaultIsolationTests(ShapeHiveFixture shapes, InfrastructureFixture infra)
	{
		_shapes = shapes;
		_infra = infra;
	}

	public static IEnumerable<object[]> Rows()
	{
		foreach (var profile in new[] { "FAULT-API", "FAULT-WORKER", "FAULT-GATEWAY" })
			foreach (var protocol in Protocols)
				yield return new object[] { profile, protocol };
	}

	[Theory]
	[MemberData(nameof(Rows))]
	public async Task FaultIsolation(string profileName, string protocol)
	{
		var profile = FaultProfile.For(profileName);
		Console.WriteLine($"result profile={profile.Name} protocol={protocol} faulted={profile.Role} Api={profile.Api} Worker={profile.Worker} Gateway={profile.Gateway}");
		Console.WriteLine($"characterization={FaultCharacterization.Path}");

		var faultMode = protocol == "recovery" ? "refuse" : protocol;
		OtlpSignalReceiver? collector = null;
		FaultOtlpProcess? fault = null;
		ShapeHostSet? hosts = null;
		var measured = false;
		var healthyRss = 0L;
		var additionalOutageRss = 0L;
		var latencyMs = 0L;
		var telemetryLoss = 0;
		long? recoveryMs = null;
		var soak = new ConcurrentBag<long>();
		try
		{
			try
			{
				_infra.EnsureDocker();
				var shape = _shapes.Get(profile.Shape);
				collector = await OtlpSignalReceiver.StartDevelopmentAsync();
				fault = await FaultOtlpProcess.StartAsync();
				await fault.SetModeAsync(faultMode);

				var sql = _infra.CreateIsolatedConnectionString($"{profile.Name}{protocol}".Replace("-", "", StringComparison.Ordinal));
				await GeneratedDbAccess.MigrateViaEfAsync(shape, sql);
				var overlay = new RuntimeHostOverlay(profile.Role,
													 new Dictionary<string, string?>(StringComparer.Ordinal)
													 {
														 ["OTEL_EXPORTER_OTLP_ENDPOINT"] = fault.DataUrl,
														 ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
													 });
				var armRetry = shape.Options is { Worker: true, MassTransit: true, Api: true };
				hosts = await ShapeHostSet.StartAsync(shape,
													  _infra,
													  sql,
													  DestinationMode.ReceiverAvailable,
													  productionReceiver: null,
													  armRetry,
													  overlay);
				AssertBinding(profile, hosts);
				var processIds = CaptureProcessIds(profile, hosts);
				var faultedProcessId = processIds[profile.Role];
				healthyRss = ReadRss(faultedProcessId);
				measured = true;

				var token = await AuthTokenHelper.TryGetAdministratorTokenAsync(_infra, shape);
				var entryClient = shape.Options.Gateway ? hosts.GatewayClient : hosts.ApiClient;
				if (entryClient is not null)
					AuthTokenHelper.AttachBearer(entryClient, token);
				if (hosts.ApiClient is not null && !ReferenceEquals(hosts.ApiClient, entryClient))
					AuthTokenHelper.AttachBearer(hosts.ApiClient, token);

				soak.Add(ReadRss(faultedProcessId));
				using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
				var sampling = SampleRssAsync(timer, faultedProcessId, soak);
				var started = Stopwatch.GetTimestamp();
				try
				{
					if (profile.Name == "FAULT-WORKER")
						await WorkerConsumerDriver.RunBusinessAsync(shape, hosts, _infra, sql);
					else
						await RuntimeWorkloads.RunFullApiWorkerBusinessAsync(shape,
																			 hosts,
																			 sql,
																			 entryClient!,
																			 expectGateway: profile.Name == "FAULT-GATEWAY");
				}
				finally
				{
					timer.Dispose();
					await sampling;
					if (TryReadRss(faultedProcessId, out var soaked))
						soak.Add(soaked);
					latencyMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
				}

				AssertUnchanged(hosts, processIds);
				var status = await fault.GetStatusAsync();
				telemetryLoss = Math.Max(0, status.Accepted - status.Retained);
				status.Mode.Should().Be(faultMode);
				if (WithholdsExportCompletion(protocol))
					status.Completed.Should().Be(0);
				await AssertExportHeldAsync(fault, shape, profile, protocol, status);

				await Task.Delay(TimeSpan.FromSeconds(1));
				if (TryReadRss(faultedProcessId, out var additional))
					additionalOutageRss = additional;
				AssertUnchanged(hosts, processIds);
				if (profile.Name == "FAULT-WORKER")
					RuntimeWorkloads.AssertAbsences(shape, expectGateway: false);
				AssertOperationalLogs(profile, hosts);
				await AssertProtocolResponseAsync(fault, profile, hosts, protocol);
				AssertStockExporter(shape);

				if (protocol == "recovery")
				{
					var serviceName = FaultedServiceName(shape, profile.Role);
					var recoverStarted = Stopwatch.GetTimestamp();
					await fault.SetModeAsync("healthy");
					var arrived = false;
					for (var attempt = 0; attempt < RuntimeWorkloads.SignalWaitAttempts && !arrived; attempt++)
					{
						arrived = await fault.HasMetricForAsync(serviceName);
						if (!arrived)
							await Task.Delay(100);
					}

					if (!arrived)
						arrived = await fault.HasMetricForAsync(serviceName);

					recoveryMs = (long)Stopwatch.GetElapsedTime(recoverStarted).TotalMilliseconds;
					arrived.Should().BeTrue();
					AssertUnchanged(hosts, processIds);
				}
			}
			catch (Exception ex) when (ContainsCanary(ex.ToString()))
			{
				throw new XunitException("the canary leaked");
			}
		}
		finally
		{
			try
			{
				if (measured)
					FaultCharacterization.Append(new(profile.Name,
													 protocol,
													 soak.ToArray(),
													 healthyRss,
													 additionalOutageRss,
													 telemetryLoss,
													 latencyMs,
													 recoveryMs));
			}
			finally
			{
				await DisposeQuietly(hosts);
				await DisposeQuietly(fault);
				await DisposeQuietly(collector);
			}
		}
	}

	private static async Task DisposeQuietly(IAsyncDisposable? resource)
	{
		if (resource is null)
			return;

		try
		{
			await resource.DisposeAsync();
		}
		catch (Exception)
		{
			// One disposal must not skip the others.
		}
	}

	private static async Task SampleRssAsync(PeriodicTimer timer, int processId, ConcurrentBag<long> samples)
	{
		try
		{
			while (await timer.WaitForNextTickAsync())
			{
				if (TryReadRss(processId, out var rss))
					samples.Add(rss);
			}
		}
		catch (ObjectDisposedException)
		{
		}
		catch (InvalidOperationException)
		{
		}
	}

	private static void AssertBinding(FaultProfile profile, ShapeHostSet hosts)
	{
		AssertRole(profile.Api, hosts, RuntimeHostRole.Api);
		AssertRole(profile.Worker, hosts, RuntimeHostRole.Worker);
		AssertRole(profile.Gateway, hosts, RuntimeHostRole.Gateway);
	}

	private static void AssertRole(string verdict, ShapeHostSet hosts, RuntimeHostRole role)
	{
		if (verdict == "NotApplicable")
		{
			hosts.ProcessId(role).Should().BeNull();
			return;
		}

		hosts.ProcessId(role).Should().NotBeNull();
		hosts.HasExited(role).Should().BeFalse();
	}

	private static Dictionary<RuntimeHostRole, int> CaptureProcessIds(FaultProfile profile, ShapeHostSet hosts)
	{
		var processIds = new Dictionary<RuntimeHostRole, int>();
		Capture(profile.Api, RuntimeHostRole.Api);
		Capture(profile.Worker, RuntimeHostRole.Worker);
		Capture(profile.Gateway, RuntimeHostRole.Gateway);
		return processIds;

		void Capture(string verdict, RuntimeHostRole role)
		{
			if (verdict == "Match")
				processIds[role] = hosts.ProcessId(role)!.Value;
		}
	}

	private static void AssertUnchanged(ShapeHostSet hosts, IReadOnlyDictionary<RuntimeHostRole, int> processIds)
	{
		foreach (var (role, processId) in processIds)
		{
			hosts.HasExited(role).Should().BeFalse();
			hosts.ProcessId(role).Should().Be(processId);
		}
	}

	private static async Task AssertExportHeldAsync(FaultOtlpProcess fault,
													GeneratedShape shape,
													FaultProfile profile,
													string protocol,
													FaultReceiverStatus status)
	{
		if (protocol is "timeout" or "saturate")
		{
			// The business call has already returned. The stock batch exporter can still be in flight.
			status = await WaitForStatusAsync(fault, current => current.Accepted > 0 && current.InFlight > 0);
			status.Accepted.Should().BeGreaterThan(0);
			status.InFlight.Should().BeGreaterThan(0);
			status.Completed.Should().Be(0);
		}

		if (protocol == "saturate")
		{
			(await fault.GetPayloadsAsync()).Should().BeEmpty();
			return;
		}

		if (protocol == "timeout")
		{
			(await fault.GetPayloadsAsync()).Should().NotBeEmpty();
			await AssertFaultedHostOnlyAsync(fault, shape, profile.Role, requireObserved: true);
			return;
		}

		if (protocol is "429" or "malformed")
		{
			status = await WaitForStatusAsync(fault, current => current.Accepted > 0);
			status.Accepted.Should().BeGreaterThan(0);
			var payloads = await fault.GetPayloadsAsync();
			if (payloads.Count > 0)
				await AssertFaultedHostOnlyAsync(fault, shape, profile.Role, requireObserved: true);
			return;
		}

		await AssertFaultedHostOnlyAsync(fault, shape, profile.Role, requireObserved: false);
	}

	private static async Task AssertProtocolResponseAsync(FaultOtlpProcess fault,
														  FaultProfile profile,
														  ShapeHostSet hosts,
														  string protocol)
	{
		if (protocol is "401" or "403")
		{
			await AssertCanarySentAsync(fault, protocol);
			AssertNoCanary(profile, hosts);
			return;
		}

		if (protocol is "429" or "malformed")
		{
			var response = protocol == "429"
							   ? await WaitForResponseAsync(fault, current => current.Status == 429)
							   : await WaitForResponseAsync(fault,
															current => current.Status == 200 &&
																	   Encoding.UTF8.GetString(current.Body) == "not-protobuf");
			if (protocol == "429")
			{
				response.Status.Should().Be(429);
				Encoding.UTF8.GetString(response.Body).Should().Be("slow down");
			}
			else
			{
				response.Status.Should().Be(200);
				Encoding.UTF8.GetString(response.Body).Should().Be("not-protobuf");
			}
		}

		AssertNoCanary(profile, hosts);
	}

	private static async Task<FaultReceiverStatus> WaitForStatusAsync(FaultOtlpProcess fault, Func<FaultReceiverStatus, bool> ready)
	{
		var status = await fault.GetStatusAsync();
		for (var attempt = 0; attempt < 300 && !ready(status); attempt++)
		{
			await Task.Delay(100);
			status = await fault.GetStatusAsync();
		}

		return status;
	}

	private static async Task<FaultDataResponse> WaitForResponseAsync(FaultOtlpProcess fault, Func<FaultDataResponse, bool> ready)
	{
		var response = await fault.GetResponseAsync();
		for (var attempt = 0; attempt < 300 && !ready(response); attempt++)
		{
			await Task.Delay(100);
			response = await fault.GetResponseAsync();
		}

		return response;
	}

	private static async Task AssertCanarySentAsync(FaultOtlpProcess fault, string protocol)
	{
		var expected = protocol == "401" ? 401 : 403;
		var response = await WaitForResponseAsync(fault, current => current.Status == expected);
		response.Status.Should().Be(expected);
		var body = Encoding.UTF8.GetString(response.Body);
		if (!body.Contains(FaultCanaries.Credential, StringComparison.Ordinal) ||
			!body.Contains(FaultCanaries.Prose, StringComparison.Ordinal))
			throw new XunitException("the canary was missing");
	}

	private static async Task AssertFaultedHostOnlyAsync(FaultOtlpProcess fault,
														 GeneratedShape shape,
														 RuntimeHostRole role,
														 bool requireObserved)
	{
		var expected = FaultedServiceName(shape, role);
		var names = await fault.ObservedServiceNamesAsync();
		if (requireObserved)
			names.Should().NotBeEmpty();
		foreach (var name in names)
			name.Should().Be(expected);
	}

	private static void AssertOperationalLogs(FaultProfile profile, ShapeHostSet hosts)
	{
		var output = CombinedOutput(profile, hosts);
		var (company, longRunning) = profile.Name switch
									 {
										 "FAULT-WORKER" => (0, 1),
										 _ => (1, 1)
									 };
		CountLines(output, "Company creation completed.").Should().Be(company);
		CountLines(output, "Long-running process command completed.").Should().Be(longRunning);
		CountLines(output, "Unhandled HTTP request failure.").Should().Be(0);
	}

	private static void AssertNoCanary(FaultProfile profile, ShapeHostSet hosts)
	{
		var output = CombinedOutput(profile, hosts);
		if (output.Contains(FaultCanaries.Credential, StringComparison.Ordinal) ||
			output.Contains(FaultCanaries.Prose, StringComparison.Ordinal))
			throw new XunitException("the canary leaked");
	}

	private static void AssertStockExporter(GeneratedShape shape)
	{
		var project = shape.ProjectDir("Common.Observability");
		var composition = File.ReadAllText(Path.Combine(project, "ObservabilityHostBuilderExtensions.cs"));
		CountOccurrences(composition, "UseOtlpExporter").Should().Be(1);
		CountOccurrences(composition, "AddOtlpExporter").Should().Be(0);

		var bannedMembers = new[]
							{
								"AddOtlpExporter",
								"AddProcessor",
								"AddView",
								"FilterHttpRequestMessage",
								"MetricStreamConfiguration"
							};
		var typeName = new Regex(@"\b(?:class|record|struct|enum|interface)\s+(\w+)", RegexOptions.CultureInvariant);
		var bannedType = new Regex("Processor|Adapter|View|Sanitiz|Redact|Hmac|Preflight|SecretProvider",
								   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		foreach (var path in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
		{
			if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
				path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
				continue;

			var source = File.ReadAllText(path);
			foreach (var member in bannedMembers)
				source.Contains(member, StringComparison.Ordinal).Should().BeFalse(Path.GetFileName(path));

			foreach (Match match in typeName.Matches(source))
				bannedType.IsMatch(match.Groups[1].Value).Should().BeFalse(match.Groups[1].Value);
		}

		var projectFile = Directory.EnumerateFiles(project, "*.csproj").Single();
		var csproj = File.ReadAllText(projectFile);
		csproj.Contains("Jaeger", StringComparison.Ordinal).Should().BeFalse();
		csproj.Contains("Prometheus", StringComparison.Ordinal).Should().BeFalse();
		csproj.Contains("InMemory", StringComparison.Ordinal).Should().BeFalse();
	}

	private static string CombinedOutput(FaultProfile profile, ShapeHostSet hosts)
	{
		var output = string.Empty;
		Append(profile.Api, RuntimeHostRole.Api);
		Append(profile.Worker, RuntimeHostRole.Worker);
		Append(profile.Gateway, RuntimeHostRole.Gateway);
		return output;

		void Append(string verdict, RuntimeHostRole role)
		{
			if (verdict == "Match")
				output += hosts.Output(role);
		}
	}

	private static string FaultedServiceName(GeneratedShape shape, RuntimeHostRole role) =>
		role switch
		{
			RuntimeHostRole.Api => shape.ServiceName("Api"),
			RuntimeHostRole.Worker => shape.ServiceName("Worker"),
			RuntimeHostRole.Gateway => shape.ServiceName("Common.ApiGateway"),
			_ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown host.")
		};

	private static bool WithholdsExportCompletion(string protocol) =>
		protocol is "refuse" or "timeout" or "saturate" or "shutdown" or "recovery";

	private static bool ContainsCanary(string text) =>
		text.Contains(FaultCanaries.Credential, StringComparison.Ordinal) ||
		text.Contains(FaultCanaries.Prose, StringComparison.Ordinal);

	private static int CountLines(string output, string message) =>
		output.Split('\n').Count(line => line.Contains(message, StringComparison.Ordinal));

	private static int CountOccurrences(string text, string value)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += value.Length;
		}

		return count;
	}

	private static long ReadRss(int processId)
	{
		using var process = Process.GetProcessById(processId);
		process.Refresh();
		return process.WorkingSet64;
	}

	private static bool TryReadRss(int processId, out long rss)
	{
		try
		{
			rss = ReadRss(processId);
			return true;
		}
		catch (ArgumentException)
		{
			rss = 0;
			return false;
		}
	}
}

internal sealed record FaultProfile(string Name, RuntimeShape Shape, RuntimeHostRole Role, string Api, string Worker, string Gateway)
{
	internal static FaultProfile For(string name) =>
		name switch
		{
			"FAULT-API" => new("FAULT-API", RuntimeShape.FullApiWorker, RuntimeHostRole.Api, "Match", "Match", "NotApplicable"),
			"FAULT-WORKER" => new("FAULT-WORKER", RuntimeShape.WorkerConsumer, RuntimeHostRole.Worker, "NotApplicable", "Match", "NotApplicable"),
			"FAULT-GATEWAY" => new("FAULT-GATEWAY", RuntimeShape.GatewayFull, RuntimeHostRole.Gateway, "Match", "Match", "Match"),
			_ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown fault profile.")
		};
}
