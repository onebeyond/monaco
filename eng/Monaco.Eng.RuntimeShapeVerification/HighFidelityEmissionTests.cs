using AwesomeAssertions;
using Monaco.Eng.RuntimeShapeVerification.Drivers;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using Monaco.Eng.RuntimeShapeVerification.Workloads;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
[Collection("RuntimeShapeVerification")]
public sealed class HighFidelityEmissionTests
{
	private const string EnforcementSentence =
		"A consumer-owned Collector or compatible downstream pipeline is the enforcement point for filtering, redaction, sanitization, identity treatment, hashing, transformation, sampling policy, cardinality controls, routing, transport, storage, access, retention, and alerting.";

	private const string CompanyLogBody = "Company creation completed.";
	private const string CounterName = "company.successful_creations";
	private const string SqlSystemName = "microsoft.sql_server";

	// The selected SDK exports metrics on its own 60s interval. This wait does not set a schedule variable.
	private static readonly TimeSpan SecondCounterExportWait = TimeSpan.FromSeconds(70);

	private static readonly JsonSerializerOptions ClaimJson = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};

	private static readonly string[] BannedMembers =
	[
		"AddProcessor",
		"AddView",
		"FilterHttpRequestMessage",
		"MetricStreamConfiguration",
		"ForceFlush",
		"SetMissing",
		"ApplyAdoptedDefaults",
		"AddMassTransitInstrumentation",
		"ConfigureMassTransit",
		"SetDbStatement",
		"ResourceKey",
		"AllowList",
		"SeriesCeiling",
		"CardinalityLimit",
		"ObservabilityConfigurationDiagnostics"
	];

	private static readonly Regex BannedType = new("Processor|Adapter|View|Sanitiz|Redact|Hmac|Preflight|SecretProvider",
												   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex TypeName = new(@"\b(?:record\s+class|record\s+struct|class|record|struct|enum|interface)\s+(\w+)",
												 RegexOptions.CultureInvariant);

	private readonly ShapeHiveFixture _shapes;
	private readonly InfrastructureFixture _infra;

	public HighFidelityEmissionTests(ShapeHiveFixture shapes, InfrastructureFixture infra)
	{
		_shapes = shapes;
		_infra = infra;
	}

	public static IEnumerable<object[]> Rows()
	{
		foreach (var shape in Enum.GetValues<RuntimeShape>())
			yield return new object[] { shape };
	}

	[Theory]
	[MemberData(nameof(Rows))]
	public async Task HighFidelityEmission(RuntimeShape id)
	{
		var shape = _shapes.Get(id);
		var http = shape.Options.Api || shape.Options.Gateway;
		var dependency = shape.Options.Api || shape.Options.Worker;
		var messaging = shape.Options.MassTransit;
		var applicationLog = shape.Options.Api;
		var authenticatedEntry = shape.Options.Auth && (shape.Options.Api || shape.Options.Gateway);
		var companyCounter = shape.Options.Api;
		shape.Shape.Should().Be(id);
		Console.WriteLine($"result shape={id} HTTP={Verdict(http)} dependency={Verdict(dependency)} messaging={Verdict(messaging)} applicationLog={Verdict(applicationLog)} authenticatedEntry={Verdict(authenticatedEntry)} companyCounter={Verdict(companyCounter)}");
		AssertGuide(shape);
		AssertGovernance(shape);

		OtlpSignalReceiver? receiver = null;
		ShapeHostSet? hosts = null;
		try
		{
			_infra.EnsureDocker();
			receiver = await OtlpSignalReceiver.StartDevelopmentAsync();
			receiver.Clear();
			var sql = _infra.CreateIsolatedConnectionString(id.ToString());
			await GeneratedDbAccess.MigrateViaEfAsync(shape, sql);
			var armRetry = shape.Options is { Worker: true, MassTransit: true, Api: true };
			hosts = await ShapeHostSet.StartAsync(shape,
												  _infra,
												  sql,
												  DestinationMode.ReceiverAvailable,
												  productionReceiver: null,
												  armRetry);
			var token = await DriveAsync(shape, hosts, sql, receiver);
			await receiver.WaitUntilAsync(() => SignalsReady(receiver, shape, http, dependency, messaging, applicationLog),
										  attempts: RuntimeWorkloads.SignalWaitAttempts,
										  describe: () => Describe(receiver, shape));
			AssertSignals(receiver, shape, http, dependency, messaging, applicationLog, authenticatedEntry, token);
		}
		finally
		{
			if (hosts is not null)
				await hosts.DisposeAsync();
			if (receiver is not null)
				await receiver.DisposeAsync();
		}
	}

	private async Task<string?> DriveAsync(GeneratedShape shape,
										   ShapeHostSet hosts,
										   string sql,
										   OtlpSignalReceiver receiver)
	{
		var token = await AuthTokenHelper.TryGetAdministratorTokenAsync(_infra, shape);
		var entryClient = shape.Options.Gateway ? hosts.GatewayClient : hosts.ApiClient;
		if (entryClient is not null)
			AuthTokenHelper.AttachBearer(entryClient, token);
		if (hosts.ApiClient is not null && !ReferenceEquals(hosts.ApiClient, entryClient))
			AuthTokenHelper.AttachBearer(hosts.ApiClient, token);

		Guid? companyId = null;
		switch (shape.Shape)
		{
			case RuntimeShape.FullApiWorker:
			case RuntimeShape.FullApiWorkerWithoutTests:
				await RuntimeWorkloads.RunFullApiWorkerBusinessAsync(shape, hosts, sql, entryClient!, expectGateway: false);
				break;
			case RuntimeShape.GatewayFull:
				await RuntimeWorkloads.RunFullApiWorkerBusinessAsync(shape, hosts, sql, entryClient!, expectGateway: true);
				break;
			case RuntimeShape.LeanApi:
			case RuntimeShape.PublisherApi:
				var (created, id) = await ApiCalls.CreateCompanyAsync(entryClient!, $"co-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@example.test");
				created.StatusCode.Should().Be(HttpStatusCode.Created);
				companyId = id;
				break;
			case RuntimeShape.WorkerConsumer:
				await WorkerConsumerDriver.ExecuteAsync(shape, hosts, _infra, sql, receiver, DestinationMode.ReceiverAvailable);
				break;
			case RuntimeShape.WorkerBaseline:
				await WorkerBaselineDriver.ExecuteAsync(shape, hosts, sql, receiver, DestinationMode.ReceiverAvailable);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(shape), shape.Shape, "Unknown runtime shape.");
		}

		if (!shape.Options.Api)
			return token;

		var apiName = shape.ServiceName("Api");
		await receiver.WaitUntilAsync(() => CounterIsOne(receiver, apiName, shape.ApplicationMeterName),
									  attempts: RuntimeWorkloads.SignalWaitAttempts,
									  describe: receiver.Describe);
		var (rejected, _) = await ApiCalls.CreateCompanyAsync(entryClient!, "", "not-an-email");
		rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await GeneratedDbAccess.CountCompaniesAsync(sql)).Should().Be(1);
		var metricsBefore = ApiMetricResourceCount(receiver, apiName);
		await Task.Delay(SecondCounterExportWait);
		await receiver.WaitUntilAsync(() => ApiMetricResourceCount(receiver, apiName) > metricsBefore && CounterIsOne(receiver, apiName, shape.ApplicationMeterName),
									  attempts: RuntimeWorkloads.SignalWaitAttempts,
									  describe: receiver.Describe);
		receiver.CounterSum(apiName, shape.ApplicationMeterName, CounterName)
				.Should()
				.BeApproximately(1d, 0.001, receiver.Describe());
		if (shape.Options.Worker)
			receiver.HasCounter(shape.ServiceName("Worker"), shape.ApplicationMeterName, CounterName)
					.Should()
					.BeFalse(receiver.Describe());
		if (shape.Options.Gateway)
			receiver.HasCounter(shape.ServiceName("Common.ApiGateway"), shape.ApplicationMeterName, CounterName)
					.Should()
					.BeFalse(receiver.Describe());

		if (shape.Shape == RuntimeShape.PublisherApi)
		{
			companyId.Should().NotBeNull();
			await ApiCalls.CreateProductWithSeededImageAsync(entryClient!, companyId!.Value);
		}

		return token;
	}

	private static void AssertSignals(OtlpSignalReceiver receiver,
									  GeneratedShape shape,
									  bool http,
									  bool dependency,
									  bool messaging,
									  bool applicationLog,
									  bool authenticatedEntry,
									  string? token)
	{
		var spans = receiver.GetSpans();
		var logs = receiver.GetLogs();
		if (http)
		{
			foreach (var service in HttpServices(shape))
			{
				var httpSpans = spans.Where(span => span.ServiceName == service && span.ScopeName == "Microsoft.AspNetCore").ToList();
				httpSpans.Should().NotBeEmpty(Describe(receiver, shape));
				foreach (var span in httpSpans)
				{
					span.Attributes.Should().Contain(attribute => attribute.Key == "http.request.method" && attribute.Value.Length > 0);
					span.Attributes.Should().Contain(attribute => attribute.Key == "http.response.status_code" && attribute.Value.Length > 0);
				}
			}
		}
		else
			spans.Should().NotContain(span => span.ScopeName == "Microsoft.AspNetCore");

		if (dependency)
		{
			var sqlSpans = spans.Where(span => SqlServices(shape).Contains(span.ServiceName) &&
											   span.ScopeName.Contains("SqlClient", StringComparison.Ordinal))
								.ToList();
			sqlSpans.Should().NotBeEmpty(Describe(receiver, shape));
			foreach (var span in sqlSpans)
				span.Attributes.Should().Contain(new DecodedAttribute("db.system.name", SqlSystemName));
		}

			if (messaging)
			{
				foreach (var service in MessagingServices(shape))
				{
					var messageSpans = spans.Where(span => span.ServiceName == service && span.ScopeName == "MassTransit").ToList();
					messageSpans.Should().NotBeEmpty($"{service} {Describe(receiver, shape)}");
					messageSpans.Should().Contain(span => span.Attributes.Any(attribute => attribute.Key == "messaging.operation" && attribute.Value.Length > 0),
												   string.Join(" | ",
															   messageSpans.Select(span => $"{span.Name} [{string.Join(", ", span.Attributes.Select(attribute => $"{attribute.Key}={attribute.Value}"))}]")));
				}
			}

		if (applicationLog)
			AssertCompanyLog(receiver, shape, authenticatedEntry, token);
		else
			logs.Should().NotContain(log => log.Body == CompanyLogBody);

		if (!authenticatedEntry)
		{
			spans.Should().NotContain(span => span.Attributes.Any(attribute => attribute.Key == "user.id") ||
											  span.Events.Any(@event => @event.Name == "user.claim"));
			logs.Should().NotContain(log => log.Attributes.Any(attribute => attribute.Key == "user.id" || attribute.Key == "user.claims"));
		}
	}

	private static void AssertCompanyLog(OtlpSignalReceiver receiver,
										 GeneratedShape shape,
										 bool authenticatedEntry,
										 string? token)
	{
		var apiName = shape.ServiceName("Api");
		var companyLogs = receiver.GetLogs()
								  .Where(log => log.ServiceName == apiName && log.Body == CompanyLogBody)
								  .ToList();
		companyLogs.Should().NotBeEmpty(receiver.Describe());
		IReadOnlyList<TokenClaim>? claims = null;
		string? subject = null;
		string? scopeJson = null;
		if (authenticatedEntry)
		{
			token.Should().NotBeNullOrWhiteSpace();
			claims = ReadTokenClaims(token!);
			subject = claims.FirstOrDefault(claim => claim.Type == "sub" && claim.Value.Length > 0)?.Value;
			subject.Should().NotBeNullOrWhiteSpace();
			scopeJson = JsonSerializer.Serialize(claims, ClaimJson);
		}

		foreach (var log in companyLogs)
		{
			log.Attributes.Should().Contain(new DecodedAttribute("{OriginalFormat}", CompanyLogBody));
			log.TraceId.Should().NotBeNullOrEmpty();
			receiver.GetSpans()
					.Should()
					.Contain(span => span.ServiceName == apiName &&
									 span.ScopeName == "Microsoft.AspNetCore" &&
									 span.TraceId == log.TraceId);
			if (!authenticatedEntry)
				continue;

			log.Attributes.Should().Contain(new DecodedAttribute("user.id", subject!));
			log.Attributes.Should().Contain(new DecodedAttribute("user.claims", scopeJson!));
		}

		if (!authenticatedEntry)
			return;

		foreach (var service in HttpServices(shape))
		{
			var entries = receiver.GetSpans()
								  .Where(span => span.ServiceName == service &&
												 span.ScopeName == "Microsoft.AspNetCore" &&
												 MentionsCompany(span))
								  .ToList();
			entries.Should().NotBeEmpty(Describe(receiver, shape));
			foreach (var span in entries)
			{
				span.Attributes.Should().Contain(new DecodedAttribute("user.id", subject!));
				var events = span.Events.Where(@event => @event.Name == "user.claim").ToList();
				events.Should().HaveCount(claims!.Count, DescribeEvents(events));
				for (var index = 0; index < claims.Count; index++)
				{
					var claim = claims[index];
					var attributes = events[index].Attributes;
					attributes.Should().Contain(new DecodedAttribute("user.claim.type", claim.Type));
					attributes.Should().Contain(new DecodedAttribute("user.claim.value", claim.Value));
					attributes.Should().Contain(new DecodedAttribute("user.claim.value_type", claim.ValueType));
					attributes.Should().Contain(new DecodedAttribute("user.claim.issuer", claim.Issuer));
					attributes.Should().Contain(new DecodedAttribute("user.claim.original_issuer", claim.OriginalIssuer));
				}
			}
		}
	}

	private static bool SignalsReady(OtlpSignalReceiver receiver,
									 GeneratedShape shape,
									 bool http,
									 bool dependency,
									 bool messaging,
									 bool applicationLog)
	{
		var spans = receiver.GetSpans();
		if (http && HttpServices(shape).Any(service => !spans.Any(span => span.ServiceName == service && span.ScopeName == "Microsoft.AspNetCore")))
			return false;
		if (dependency && !spans.Any(span => SqlServices(shape).Contains(span.ServiceName) &&
											 span.ScopeName.Contains("SqlClient", StringComparison.Ordinal) &&
											 span.Attributes.Any(attribute => attribute.Key == "db.system.name")))
			return false;
		if (messaging && MessagingServices(shape).Any(service => !spans.Any(span => span.ServiceName == service && span.ScopeName == "MassTransit")))
			return false;
		if (applicationLog && !receiver.GetLogs().Any(log => log.ServiceName == shape.ServiceName("Api") && log.Body == CompanyLogBody))
			return false;

		return true;
	}

	private static void AssertGuide(GeneratedShape shape)
	{
		var generated = File.ReadAllText(Path.Combine(shape.OutputDirectory, "OBSERVABILITY.md"));
		generated.Should().Contain(EnforcementSentence);
		generated.Should().Contain("hashing");
		generated.Should().Contain("sampling policy");
		generated.Should().Contain("compliance requirements");
		generated.Should().NotContain("<!--#if");

		var template = File.ReadAllText(FindTemplateObservability());
		var index = template.IndexOf(EnforcementSentence, StringComparison.Ordinal);
		index.Should().BeGreaterThanOrEqualTo(0);
		var before = template[..index];
		(Count(before, "<!--#if") - Count(before, "<!--#endif")).Should().Be(0);
		var sectionStart = template.IndexOf("## High-fidelity telemetry boundary", StringComparison.Ordinal);
		var nextSection = template.IndexOf("\n## ", sectionStart + 1, StringComparison.Ordinal);
		var section = template[sectionStart..(nextSection >= 0 ? nextSection : template.Length)];
		section.Should().Contain("compliance requirements");
		section.Should().Contain("does not certify consumer outcomes");
	}

	private static void AssertGovernance(GeneratedShape shape)
	{
		var project = shape.ProjectDir("Common.Observability");
		foreach (var path in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
		{
			if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
				path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
				continue;

			var source = File.ReadAllText(path);
			foreach (var member in BannedMembers)
				source.Contains(member, StringComparison.Ordinal).Should().BeFalse($"{Path.GetFileName(path)} contains {member}");

			foreach (Match match in TypeName.Matches(source))
				BannedType.IsMatch(match.Groups[1].Value).Should().BeFalse(match.Groups[1].Value);
		}
	}

	private static List<TokenClaim> ReadTokenClaims(string token)
	{
		var payload = token.Split('.')[1];
		var json = Encoding.UTF8.GetString(Base64UrlDecode(payload));
		using var document = JsonDocument.Parse(json);
		var issuer = document.RootElement.TryGetProperty("iss", out var issElement)
						 ? issElement.GetString() ?? string.Empty
						 : string.Empty;
		var claims = new List<TokenClaim>();
		foreach (var property in document.RootElement.EnumerateObject())
			AddClaims(claims, property.Name, property.Value, issuer);

		// Authentication splits one space-delimited scope claim into one claim per scope before enrichment reads the principal.
		var scopeIndex = claims.FindIndex(claim => claim.Type == "scope");
		if (scopeIndex >= 0 && claims[scopeIndex].Value.Contains(' ', StringComparison.Ordinal))
		{
			var scope = claims[scopeIndex];
			claims.RemoveAt(scopeIndex);
			foreach (var value in scope.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
				claims.Add(scope with { Value = value });
		}

		return claims;
	}

	private static void AddClaims(List<TokenClaim> claims, string type, JsonElement value, string issuer)
	{
		switch (value.ValueKind)
		{
			case JsonValueKind.Array:
				foreach (var item in value.EnumerateArray())
					AddClaims(claims, type, item, issuer);
				return;
			case JsonValueKind.Object:
				claims.Add(new(type, value.GetRawText(), "JSON", issuer, issuer));
				return;
			case JsonValueKind.Null:
				claims.Add(new(type, string.Empty, "JSON_NULL", issuer, issuer));
				return;
		}

		var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
		var valueType = value.ValueKind switch
						{
							JsonValueKind.String => "http://www.w3.org/2001/XMLSchema#string",
							JsonValueKind.True or JsonValueKind.False => "http://www.w3.org/2001/XMLSchema#boolean",
							JsonValueKind.Number when type is "exp" or "iat" or "nbf" => "http://www.w3.org/2001/XMLSchema#integer64",
							JsonValueKind.Number when !text.Contains('.', StringComparison.Ordinal) &&
													  !text.Contains('e', StringComparison.OrdinalIgnoreCase) &&
													  long.TryParse(text, out var integer) &&
													  integer is >= int.MinValue and <= int.MaxValue => "http://www.w3.org/2001/XMLSchema#integer32",
							JsonValueKind.Number when !text.Contains('.', StringComparison.Ordinal) &&
													  !text.Contains('e', StringComparison.OrdinalIgnoreCase) => "http://www.w3.org/2001/XMLSchema#integer64",
							JsonValueKind.Number => "http://www.w3.org/2001/XMLSchema#double",
							_ => "http://www.w3.org/2001/XMLSchema#string"
						};
		if (value.ValueKind is JsonValueKind.True)
			text = "true";
		else if (value.ValueKind is JsonValueKind.False)
			text = "false";

		claims.Add(new(type, text, valueType, issuer, issuer));
	}

	private static byte[] Base64UrlDecode(string value)
	{
		var padded = value.Replace('-', '+').Replace('_', '/');
		padded = (padded.Length % 4) switch
				 {
					 2 => padded + "==",
					 3 => padded + "=",
					 _ => padded
				 };
		return Convert.FromBase64String(padded);
	}

	private static bool MentionsCompany(DecodedSpan span) =>
		span.Name.Contains("Companies", StringComparison.Ordinal) ||
		span.Attributes.Any(attribute => attribute.Value.Contains("Companies", StringComparison.Ordinal));

	private static bool CounterIsOne(OtlpSignalReceiver receiver, string serviceName, string meterName) =>
		receiver.CounterSum(serviceName, meterName, CounterName) is >= 0.999 and <= 1.001;

	private static int ApiMetricResourceCount(OtlpSignalReceiver receiver, string serviceName) =>
		receiver.GetMetrics().Count(resource => resource.ServiceName == serviceName);

	private static IEnumerable<string> HttpServices(GeneratedShape shape)
	{
		if (shape.Options.Api)
			yield return shape.ServiceName("Api");
		if (shape.Options.Gateway)
			yield return shape.ServiceName("Common.ApiGateway");
	}

	private static IEnumerable<string> SqlServices(GeneratedShape shape)
	{
		if (shape.Options.Api)
			yield return shape.ServiceName("Api");
		if (shape.Options.Worker)
			yield return shape.ServiceName("Worker");
	}

	private static IEnumerable<string> MessagingServices(GeneratedShape shape)
	{
		if (shape.Options.Api && shape.Options.MassTransit)
			yield return shape.ServiceName("Api");
		if (shape.Options.Worker && shape.Options.MassTransit)
			yield return shape.ServiceName("Worker");
	}

	private static string Verdict(bool applies) =>
		applies ? "Match" : "NotApplicable";

	private static string Describe(OtlpSignalReceiver receiver, GeneratedShape shape)
	{
		var lines = receiver.GetSpans()
							.Take(30)
							.Select(span => $"{span.ServiceName} {span.ScopeName} {span.Name} [{string.Join(", ", span.Attributes.Select(attribute => attribute.Key))}]");
		return $"{receiver.Describe()} spans={string.Join(" | ", lines)} logs={receiver.GetLogs().Count}";
	}

	private static string DescribeEvents(IReadOnlyList<DecodedSpanEvent> events) =>
		string.Join(" || ",
					events.Select(@event => string.Join(", ",
														@event.Attributes.Select(attribute => $"{attribute.Key}={attribute.Value}"))));

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

	private sealed record TokenClaim(string Type, string Value, string ValueType, string Issuer, string OriginalIssuer);
}
