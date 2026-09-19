using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;
using Monaco.Template.Backend.Common.Infrastructure.Persistence;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Infrastructure Persistence", "Persistence Actor")]
public sealed class PersistenceActorTests
{
	[Fact(DisplayName = "Authenticated valid sub resolves subject actor")]
	public void AuthenticatedValidSubResolvesSubjectActor()
	{
		const string sub = "User|Org:Id";
		var (diagnostics, logger) = CreateDiagnostics();
		var provider = new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix + sub);

		PersistenceActor.Resolve(provider, diagnostics)
						.Should()
						.Be(PersistenceActor.SubjectPrefix + sub);
		logger.Entries
			  .Should()
			  .BeEmpty();
	}

	[Fact(DisplayName = "Case-distinct and delimiter-bearing subjects remain distinct")]
	public void CaseDistinctAndDelimiterBearingSubjectsRemainDistinct()
	{
		var (diagnostics, _) = CreateDiagnostics();
		var upper = PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix + "AbC"),
											 diagnostics);
		var lower = PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix + "abc"),
											 diagnostics);
		var delimited = PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix + "user|iss:https://idp"),
												 diagnostics);

		upper.Should()
			 .Be("subject:AbC");
		lower.Should()
			 .Be("subject:abc");
		delimited.Should()
				 .Be("subject:user|iss:https://idp");
		upper.Should()
			 .NotBe(lower);
	}

	[Fact(DisplayName = "Subject suffix is not trimmed")]
	public void SubjectSuffixIsNotTrimmed()
	{
		var (diagnostics, logger) = CreateDiagnostics();
		const string actor = PersistenceActor.SubjectPrefix + "  user";

		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => actor), diagnostics)
						.Should()
						.Be(actor);
		logger.Entries
			  .Should()
			  .BeEmpty();
	}

	[Fact(DisplayName = "Unauthenticated HTTP actor is anonymous")]
	public void UnauthenticatedHttpActorIsAnonymous()
	{
		var (diagnostics, logger) = CreateDiagnostics();

		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.Anonymous), diagnostics)
						.Should()
						.Be(PersistenceActor.Anonymous);
		logger.Entries
			  .Should()
			  .BeEmpty();
	}

	[Fact(DisplayName = "API without HTTP context resolves system actor")]
	public void ApiWithoutHttpContextResolvesSystemActor()
	{
		var (diagnostics, logger) = CreateDiagnostics();

		const string apiSystem = "system:Host.Api";
		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => apiSystem), diagnostics)
						.Should()
						.Be(apiSystem);
		logger.Entries
			  .Should()
			  .BeEmpty();
	}

	[Fact(DisplayName = "Worker provider resolves service actor")]
	public void WorkerProviderResolvesServiceActor()
	{
		var (diagnostics, logger) = CreateDiagnostics();

		const string workerService = "service:Host.Worker";
		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.WorkerHostRole, () => workerService), diagnostics)
						.Should()
						.Be(workerService);
		logger.Entries
			  .Should()
			  .BeEmpty();
	}

	[Theory(DisplayName = "Missing or whitespace subject suffix resolves null")]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("\t")]
	public void MissingOrWhitespaceSubjectSuffixResolvesNull(string? sub)
	{
		var (diagnostics, logger) = CreateDiagnostics();
		var actor = PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix + sub),
											 diagnostics);

		actor.Should()
			 .BeNull();
		AssertSingleDiagnostic(logger, PersistenceActor.MissingCode, PersistenceActor.ApiHostRole);
	}

	[Fact(DisplayName = "Control character subject suffix resolves null as malformed")]
	public void ControlCharacterSubjectSuffixResolvesNullAsMalformed()
	{
		var (diagnostics, logger) = CreateDiagnostics();
		var actor = PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix + "ok\u0001"),
											 diagnostics);

		actor.Should()
			 .BeNull();
		AssertSingleDiagnostic(logger, PersistenceActor.MalformedCode, PersistenceActor.ApiHostRole);
	}

	[Fact(DisplayName = "Oversized complete actor resolves null")]
	public void OversizedCompleteActorResolvesNull()
	{
		var (diagnostics, logger) = CreateDiagnostics();
		var oversized = PersistenceActor.SubjectPrefix + new string('a', IAuditable.ActorMaxLength - PersistenceActor.SubjectPrefix.Length + 1);
		var actor = PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => oversized), diagnostics);

		actor.Should()
			 .BeNull();
		AssertSingleDiagnostic(logger, PersistenceActor.OversizedCode, PersistenceActor.ApiHostRole);
		oversized.Length
				 .Should()
				 .Be(IAuditable.ActorMaxLength + 1);
	}

	[Fact(DisplayName = "Maximum-length complete actor is accepted")]
	public void MaximumLengthCompleteActorIsAccepted()
	{
		var (diagnostics, logger) = CreateDiagnostics();
		var actor = PersistenceActor.SubjectPrefix + new string('a', IAuditable.ActorMaxLength - PersistenceActor.SubjectPrefix.Length);

		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => actor), diagnostics)
						.Should()
						.Be(actor);
		logger.Entries
			  .Should()
			  .BeEmpty();
		actor.Length
			 .Should()
			 .Be(IAuditable.ActorMaxLength);
	}

	[Fact(DisplayName = "Throwing inner provider resolves null")]
	public void ThrowingInnerProviderResolvesNull()
	{
		var (diagnostics, logger) = CreateDiagnostics();
		var provider = new StubActorProvider(PersistenceActor.ApiHostRole, () => throw new InvalidOperationException("token-secret"));

		var act = () => PersistenceActor.Resolve(provider, diagnostics);

		act.Should()
		   .NotThrow();
		act()
			.Should()
			.BeNull();
		AssertSingleDiagnostic(logger, PersistenceActor.ThrewCode, PersistenceActor.ApiHostRole);
		logger.Entries[0]
			  .Message
			  .Should()
			  .NotContain("token-secret");
		logger.Entries[0]
			  .State
			  .Select(pair => pair.Value?.ToString() ?? string.Empty)
			  .Should()
			  .NotContain(value => value.Contains("token-secret", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "Activity and baggage identity do not change the actor")]
	public void ActivityAndBaggageIdentityDoNotChangeTheActor()
	{
		using var activity = new Activity("tel-01").Start();
		activity.SetTag("user.id", "trace-user");
		activity.SetTag("service.name", "trace-service");
		activity.SetBaggage("user.id", "baggage-user");
		var (diagnostics, logger) = CreateDiagnostics();

		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.Anonymous), diagnostics)
						.Should()
						.Be(PersistenceActor.Anonymous);
		const string workerService = "service:Host.Worker";
		PersistenceActor.Resolve(new StubActorProvider(PersistenceActor.WorkerHostRole, () => workerService), diagnostics)
						.Should()
						.Be(workerService);
		logger.Entries
			  .Should()
			  .BeEmpty();
	}

	[Fact(DisplayName = "First warning is emitted and repeats are aggregated after five minutes")]
	public void FirstWarningIsEmittedAndRepeatsAreAggregatedAfterFiveMinutes()
	{
		var time = new FakeTimeProvider();
		var (diagnostics, logger) = CreateDiagnostics(time);
		var provider = new StubActorProvider(PersistenceActor.ApiHostRole, () => PersistenceActor.SubjectPrefix);

		PersistenceActor.Resolve(provider, diagnostics);
		PersistenceActor.Resolve(provider, diagnostics);

		logger.Entries
			  .Should()
			  .ContainSingle();
		AssertDiagnosticFields(logger.Entries[0],
							   [
								   "code", "stage", "hostRole"
							   ]);
		logger.Entries[0]
			  .State
			  .Should()
			  .Contain(pair => pair.Key == "code" && Equals(pair.Value, PersistenceActor.MissingCode));
		logger.Entries[0]
			  .State
			  .Should()
			  .Contain(pair => pair.Key == "stage" && Equals(pair.Value, PersistenceActor.ResolutionStage));
		logger.Entries[0]
			  .State
			  .Should()
			  .Contain(pair => pair.Key == "hostRole" && Equals(pair.Value, PersistenceActor.ApiHostRole));

		time.Advance(TimeSpan.FromMinutes(5));
		PersistenceActor.Resolve(provider, diagnostics);

		logger.Entries
			  .Should()
			  .HaveCount(3);
		logger.Entries
			  .Select(entry => entry.Level)
			  .Should()
			  .AllBeEquivalentTo(LogLevel.Warning);
		logger.Entries
			  .Should()
			  .OnlyContain(entry => entry.Exception == null);

		AssertDiagnosticFields(logger.Entries[0],
							   [
								   "code", "stage", "hostRole"
							   ]);
		DiagnosticFields(logger.Entries[0])["code"]
			.Should()
			.Be(PersistenceActor.MissingCode);
		DiagnosticFields(logger.Entries[0])["stage"]
			.Should()
			.Be(PersistenceActor.ResolutionStage);
		DiagnosticFields(logger.Entries[0])["hostRole"]
			.Should()
			.Be(PersistenceActor.ApiHostRole);

		AssertDiagnosticFields(logger.Entries[1],
							   [
								   "code", "hostRole", "suppressedCount"
							   ]);
		DiagnosticFields(logger.Entries[1])["code"]
			.Should()
			.Be(PersistenceActor.MissingCode);
		DiagnosticFields(logger.Entries[1])["hostRole"]
			.Should()
			.Be(PersistenceActor.ApiHostRole);
		Convert.ToInt32(DiagnosticFields(logger.Entries[1])["suppressedCount"])
			   .Should()
			   .Be(1);

		AssertDiagnosticFields(logger.Entries[2],
							   [
								   "code", "stage", "hostRole"
							   ]);
		DiagnosticFields(logger.Entries[2])["code"].Should()
												   .Be(PersistenceActor.MissingCode);
		DiagnosticFields(logger.Entries[2])["stage"].Should()
													.Be(PersistenceActor.ResolutionStage);
		DiagnosticFields(logger.Entries[2])["hostRole"].Should()
													   .Be(PersistenceActor.ApiHostRole);

		logger.Entries
			  .Select(entry => entry.Message)
			  .Should()
			  .OnlyContain(message => !message.Contains("subject:", StringComparison.Ordinal));
	}

	private static void AssertSingleDiagnostic(CapturingLogger logger, string code, string hostRole)
	{
		logger.Entries
			  .Should()
			  .ContainSingle();
		AssertDiagnosticFields(logger.Entries[0],
							   [
								   "code", "stage", "hostRole"
							   ]);
		DiagnosticFields(logger.Entries[0])["code"].Should()
												   .Be(code);
		DiagnosticFields(logger.Entries[0])["stage"].Should()
													.Be(PersistenceActor.ResolutionStage);
		DiagnosticFields(logger.Entries[0])["hostRole"].Should()
													   .Be(hostRole);
		logger.Entries[0]
			  .Exception
			  .Should()
			  .BeNull();
	}

	private static void AssertDiagnosticFields(CapturedLog log, string[] allowedKeys) =>
		DiagnosticFields(log).Keys
							 .Should()
							 .BeEquivalentTo(allowedKeys);

	private static Dictionary<string, object?> DiagnosticFields(CapturedLog log) =>
		log.State
		   .Where(pair => pair.Key != "{OriginalFormat}")
		   .ToDictionary(pair => pair.Key, pair => pair.Value);

	private static (PersistenceActorFallbackDiagnostics Diagnostics, CapturingLogger Logger) CreateDiagnostics(TimeProvider? timeProvider = null)
	{
		var logger = new CapturingLogger();
		return (new PersistenceActorFallbackDiagnostics(logger, timeProvider ?? TimeProvider.System), logger);
	}

	private sealed class StubActorProvider : IPersistenceActorProvider
	{
		private readonly Func<string?> _getActor;

		public StubActorProvider(string hostRole, Func<string?> getActor)
		{
			HostRole = hostRole;
			_getActor = getActor;
		}

		public string HostRole { get; }

		public string? GetActor() =>
			_getActor();
	}

	private sealed class CapturingLogger : ILogger<PersistenceActorFallbackDiagnostics>
	{
		public List<CapturedLog> Entries { get; } =
			[];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
			null;

		public bool IsEnabled(LogLevel logLevel) =>
			true;

		public void Log<TState>(LogLevel logLevel,
								EventId eventId,
								TState state,
								Exception? exception,
								Func<TState, Exception?, string> formatter)
		{
			var pairs = new List<KeyValuePair<string, object?>>();
			if (state is IEnumerable<KeyValuePair<string, object>> values)
				pairs.AddRange(values.Select(pair => new KeyValuePair<string, object?>(pair.Key, pair.Value)));

			Entries.Add(new CapturedLog(logLevel, exception, formatter(state, exception), pairs));
		}
	}

	private sealed record CapturedLog(LogLevel Level,
									  Exception? Exception,
									  string Message,
									  IReadOnlyList<KeyValuePair<string, object?>> State);
}