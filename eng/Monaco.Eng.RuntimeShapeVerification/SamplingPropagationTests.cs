using AwesomeAssertions;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using Monaco.Eng.RuntimeShapeVerification.Workloads;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
[Collection("RuntimeShapeVerification")]
public sealed class SamplingPropagationTests
{
	private const string UnexportedTraceSentence =
		"An Operational Log may include a TraceId for a trace that was not sampled and not exported. That TraceId is correlation context, not an exported trace.";

	private static readonly string[] GatewayHttpScopes =
	[
		"Microsoft.AspNetCore",
		"System.Net.Http",
		"Yarp.ReverseProxy"
	];

	private readonly ShapeHiveFixture _shapes;
	private readonly InfrastructureFixture _infra;

	public SamplingPropagationTests(ShapeHiveFixture shapes, InfrastructureFixture infra)
	{
		_shapes = shapes;
		_infra = infra;
	}

	public static IEnumerable<object[]> Rows()
	{
		foreach (var flow in new[] { "SAMP-HTTP", "SAMP-MSG" })
			foreach (var scenario in new[] { "SAMP-01", "SAMP-02", "SAMP-03", "SAMP-04" })
				yield return new object[] { flow, scenario };
	}

	[Theory]
	[MemberData(nameof(Rows))]
	public async Task SamplingPropagation(string flow, string scenario)
	{
		var shape = _shapes.Get(flow == "SAMP-HTTP" ? RuntimeShape.GatewayFull : RuntimeShape.FullApiWorker);
		var sampler = Sampler(scenario);
		var gatewayVerdict = flow == "SAMP-HTTP" && File.Exists(shape.BuiltDll("Common.ApiGateway"))
								 ? "Match"
								 : "NotApplicable";
		AssertBind(flow, shape);
		Console.WriteLine($"result flow={flow} scenario={scenario} sampler={sampler} Gateway={gatewayVerdict}");
		gatewayVerdict.Should().Be(flow == "SAMP-HTTP" ? "Match" : "NotApplicable");
		AssertUnexportedTraceSentence(shape);

		var samplerBefore = Environment.GetEnvironmentVariable("OTEL_TRACES_SAMPLER");
		var argumentBefore = Environment.GetEnvironmentVariable("OTEL_TRACES_SAMPLER_ARG");
		OtlpSignalReceiver? receiver = null;
		ShapeHostSet? hosts = null;
		try
		{
			_infra.EnsureDocker();
			var childEnvironment = ChildSampler(scenario, sampler);
			receiver = await OtlpSignalReceiver.StartDevelopmentAsync();
			receiver.Clear();
			var sql = _infra.CreateIsolatedConnectionString($"{flow}{scenario}".Replace("-", "", StringComparison.Ordinal));
			await GeneratedDbAccess.MigrateViaEfAsync(shape, sql);
			var armRetry = shape.Options is { Worker: true, MassTransit: true, Api: true };
			hosts = await ShapeHostSet.StartAsync(shape,
												  _infra,
												  sql,
												  DestinationMode.ReceiverAvailable,
												  productionReceiver: null,
												  armRetry,
												  childEnvironment: childEnvironment);
			Environment.GetEnvironmentVariable("OTEL_TRACES_SAMPLER").Should().Be(samplerBefore);
			Environment.GetEnvironmentVariable("OTEL_TRACES_SAMPLER_ARG").Should().Be(argumentBefore);

			var token = await AuthTokenHelper.TryGetAdministratorTokenAsync(_infra, shape);
			var entryClient = shape.Options.Gateway ? hosts.GatewayClient! : hosts.ApiClient!;
			AuthTokenHelper.AttachBearer(entryClient, token);
			if (hosts.ApiClient is not null && !ReferenceEquals(hosts.ApiClient, entryClient))
				AuthTokenHelper.AttachBearer(hosts.ApiClient, token);

			await RuntimeWorkloads.RunFullApiWorkerBusinessAsync(shape,
																 hosts,
																 sql,
																 entryClient,
																 expectGateway: flow == "SAMP-HTTP");
			var (badResponse, _) = await ApiCalls.CreateCompanyAsync(entryClient, "", "not-an-email");
			badResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
			(await GeneratedDbAccess.CountCompaniesAsync(sql)).Should().Be(1);

			await receiver.WaitUntilAsync(() => BusinessSignalsReady(receiver, shape, flow),
										  attempts: RuntimeWorkloads.SignalWaitAttempts,
										  describe: () => DescribeSignals(receiver, shape, flow));
			var apiName = shape.ServiceName("Api");
			receiver.CounterSum(apiName, shape.ApplicationMeterName, "company.successful_creations")
					.Should()
					.BeApproximately(1d, 0.001, receiver.Describe());
			receiver.HasCounter(shape.ServiceName("Common.ApiGateway"),
								shape.ApplicationMeterName,
								"company.successful_creations")
					.Should()
					.BeFalse();
			AssertMeters(receiver, shape, flow);
			if (ExportsSpans(scenario))
				AssertLinkedTrace(receiver, shape, flow);
			else
				AssertNoExportedSpans(receiver, shape, flow);
		}
		finally
		{
			if (hosts is not null)
				await hosts.DisposeAsync();
			if (receiver is not null)
				await receiver.DisposeAsync();
		}
	}

	private static void AssertBind(string flow, GeneratedShape shape)
	{
		if (flow == "SAMP-HTTP")
		{
			shape.Shape.Should().Be(RuntimeShape.GatewayFull);
			shape.ServiceName("Api").Should().Be("GatewayFull.Api");
			shape.ServiceName("Worker").Should().Be("GatewayFull.Worker");
			shape.ServiceName("Common.ApiGateway").Should().Be("GatewayFull.Common.ApiGateway");
			return;
		}

		flow.Should().Be("SAMP-MSG");
		shape.Shape.Should().Be(RuntimeShape.FullApiWorker);
		shape.ServiceName("Api").Should().Be("FullApiWorker.Api");
		shape.ServiceName("Worker").Should().Be("FullApiWorker.Worker");
		shape.Options.Gateway.Should().BeFalse();
		File.Exists(shape.BuiltDll("Common.ApiGateway")).Should().BeFalse();
	}

	private static void AssertUnexportedTraceSentence(GeneratedShape shape)
	{
		File.ReadAllText(Path.Combine(shape.OutputDirectory, "OBSERVABILITY.md"))
			.Should()
			.Contain(UnexportedTraceSentence);

		var template = File.ReadAllText(FindTemplateObservability());
		var index = template.IndexOf(UnexportedTraceSentence, StringComparison.Ordinal);
		index.Should().BeGreaterThanOrEqualTo(0);
		var before = template[..index];
		var endIfs = Count(before, "<!--#endif");
		var opened = Count(before, "<!--#if") - endIfs;
		opened.Should().Be(0, "the unexported TraceId sentence stays outside template conditions");
	}

	private static void AssertMeters(OtlpSignalReceiver receiver, GeneratedShape shape, string flow)
	{
		foreach (var (role, name) in Hosts(shape, flow))
			foreach (var meter in RequiredMeters(role))
				receiver.HasMeterPoint(name, meter)
						.Should()
						.BeTrue($"{name} is missing {meter}. {DescribeSignals(receiver, shape, flow)}");
	}

	private static void AssertLinkedTrace(OtlpSignalReceiver receiver, GeneratedShape shape, string flow)
	{
		var spans = receiver.GetSpans();
		var apiName = shape.ServiceName("Api");
		var workerName = shape.ServiceName("Worker");
		var gatewayName = flow == "SAMP-HTTP" ? shape.ServiceName("Common.ApiGateway") : null;
		if (gatewayName is null)
			spans.Should().NotContain(span => span.ServiceName.EndsWith(".Common.ApiGateway", StringComparison.Ordinal));

		var traceId = FindLinkedTrace(spans, gatewayName, apiName, workerName);
		traceId.Should().NotBeNullOrEmpty($"one trace id must link the participating hosts. {DescribeSpans(spans)}");
		if (gatewayName is null)
			return;

		var gatewaySpans = spans.Where(span => span.ServiceName == gatewayName && span.TraceId == traceId).ToList();
		gatewaySpans.Should().NotBeEmpty();
		gatewaySpans.Should().OnlyContain(span => GatewayHttpScopes.Contains(span.ScopeName));
	}

	private static void AssertNoExportedSpans(OtlpSignalReceiver receiver, GeneratedShape shape, string flow)
	{
		var participating = Hosts(shape, flow).Select(host => host.Name).ToHashSet(StringComparer.Ordinal);
		foreach (var name in participating)
		{
			receiver.HasSpans(name).Should().BeFalse(DescribeSpans(receiver.GetSpans()));
			receiver.HasMetrics(name).Should().BeTrue(receiver.Describe());
		}

		receiver.GetSpans().Should().NotContain(span => participating.Contains(span.ServiceName));
		foreach (var log in receiver.GetLogs().Where(log => participating.Contains(log.ServiceName) && log.TraceId.Length > 0))
			receiver.GetSpans().Should().NotContain(span => span.TraceId == log.TraceId);
	}

	private static string? FindLinkedTrace(IReadOnlyList<DecodedSpan> spans,
										   string? gatewayName,
										   string apiName,
										   string workerName)
	{
		var api = spans.Where(span => span.ServiceName == apiName && span.TraceId.Length > 0).ToList();
		var worker = spans.Where(span => span.ServiceName == workerName && span.TraceId.Length > 0).ToList();
		var gateway = gatewayName is null
						  ? []
						  : spans.Where(span => span.ServiceName == gatewayName && span.TraceId.Length > 0).ToList();
		IEnumerable<string> candidates = api.Select(span => span.TraceId).Intersect(worker.Select(span => span.TraceId));
		if (gatewayName is not null)
			candidates = candidates.Intersect(gateway.Select(span => span.TraceId));

		foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
		{
			var apiSpans = api.Where(span => span.TraceId == candidate).ToList();
			var workerSpans = worker.Where(span => span.TraceId == candidate).ToList();
			var workerLinked = workerSpans.Any(span => apiSpans.Any(parent => parent.SpanId.Length > 0 && parent.SpanId == span.ParentSpanId));
			if (!workerLinked)
				continue;

			if (gatewayName is null)
				return candidate;

			var gatewaySpans = gateway.Where(span => span.TraceId == candidate).ToList();
			var apiLinked = apiSpans.Any(span => gatewaySpans.Any(parent => parent.SpanId.Length > 0 && parent.SpanId == span.ParentSpanId));
			if (apiLinked)
				return candidate;
		}

		return null;
	}

	private static bool BusinessSignalsReady(OtlpSignalReceiver receiver, GeneratedShape shape, string flow)
	{
		var sum = receiver.CounterSum(shape.ServiceName("Api"), shape.ApplicationMeterName, "company.successful_creations");
		if (sum is not (>= 0.999 and <= 1.001))
			return false;

		foreach (var (role, name) in Hosts(shape, flow))
			foreach (var meter in RequiredMeters(role))
				if (!receiver.HasMeterPoint(name, meter))
					return false;

		return true;
	}

	private static IEnumerable<(RuntimeHostRole Role, string Name)> Hosts(GeneratedShape shape, string flow)
	{
		yield return (RuntimeHostRole.Api, shape.ServiceName("Api"));
		yield return (RuntimeHostRole.Worker, shape.ServiceName("Worker"));
		if (flow == "SAMP-HTTP")
			yield return (RuntimeHostRole.Gateway, shape.ServiceName("Common.ApiGateway"));
	}

	private static IReadOnlyList<string> RequiredMeters(RuntimeHostRole role) =>
		role switch
		{
			RuntimeHostRole.Api =>
			[
				"System.Runtime",
				"Microsoft.AspNetCore.Hosting",
				"System.Net.Http",
				"OpenTelemetry.Instrumentation.SqlClient",
				"MassTransit"
			],
			RuntimeHostRole.Worker =>
			[
				"System.Runtime",
				"System.Net.Http",
				"OpenTelemetry.Instrumentation.SqlClient",
				"MassTransit"
			],
			RuntimeHostRole.Gateway =>
			[
				"System.Runtime",
				"Microsoft.AspNetCore.Hosting",
				"System.Net.Http"
			],
			_ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown host.")
		};

	private static Dictionary<string, string?> ChildSampler(string scenario, string sampler)
	{
		var child = new Dictionary<string, string?>(StringComparer.Ordinal)
					{
						["OTEL_TRACES_SAMPLER"] = sampler
					};
		child["OTEL_TRACES_SAMPLER_ARG"] = scenario switch
										   {
											   "SAMP-03" => "1",
											   "SAMP-04" => "0",
											   _ => null
										   };
		return child;
	}

	private static string Sampler(string scenario) =>
		scenario switch
		{
			"SAMP-01" => "always_on",
			"SAMP-02" => "always_off",
			"SAMP-03" or "SAMP-04" => "parentbased_traceidratio",
			_ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown sampling scenario.")
		};

	private static bool ExportsSpans(string scenario) =>
		scenario is "SAMP-01" or "SAMP-03";

	private static string DescribeSignals(OtlpSignalReceiver receiver, GeneratedShape shape, string flow)
	{
		var meters = Hosts(shape, flow).Select(host =>
											   {
												   var names = receiver.GetMetrics()
																	   .Where(resource => resource.ServiceName == host.Name)
																	   .SelectMany(resource => resource.Instruments)
																	   .Select(instrument => instrument.MeterName)
																	   .Distinct(StringComparer.Ordinal);
												   return $"{host.Name}=[{string.Join(", ", names)}]";
											   });
		return $"{receiver.Describe()} meters={string.Join("; ", meters)} spans={receiver.GetSpans().Count}";
	}

	private static string DescribeSpans(IReadOnlyList<DecodedSpan> spans)
	{
		var lines = spans.Take(40)
						 .Select(span => $"{span.ServiceName} {span.ScopeName} {span.Name} trace={span.TraceId} span={span.SpanId} parent={span.ParentSpanId}");
		return string.Join(" | ", lines);
	}

	private static string FindTemplateObservability()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName,
										 "src",
										 "Content",
										 "Backend",
										 "Solution",
										 "OBSERVABILITY.md");
			if (File.Exists(candidate))
				return candidate;
		}

		throw new FileNotFoundException("Could not locate the template OBSERVABILITY.md.");
	}

	private static int Count(string text, string token)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += token.Length;
		}

		return count;
	}
}
