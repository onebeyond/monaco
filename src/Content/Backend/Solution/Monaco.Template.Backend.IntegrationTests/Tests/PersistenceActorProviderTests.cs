#if (apiService || workerService)
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
#if (apiService)
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Api.Persistence;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;
#endif
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Monaco.Template.Backend.Common.Infrastructure.Persistence;
#if (workerService)
using Monaco.Template.Backend.Worker.Persistence;
#endif

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Persistence Actor")]
public sealed class PersistenceActorProviderTests
{
#if (apiService)
	[Fact(DisplayName = "Authenticated API HTTP context resolves subject actor")]
	public void AuthenticatedApiHttpContextResolvesSubjectActor()
	{
		var diagnostics = CreateDiagnostics();
		var provider = new ApiPersistenceActorProvider(new StubHttpContextAccessor { HttpContext = AuthenticatedContext("raw-sub|Id") },
													   diagnostics);

		PersistenceActor.Resolve(provider, diagnostics)
						.Should()
						.Be("subject:raw-sub|Id");
	}

	[Fact(DisplayName = "Unauthenticated API HTTP context resolves anonymous actor")]
	public void UnauthenticatedApiHttpContextResolvesAnonymousActor()
	{
		var diagnostics = CreateDiagnostics();
		var provider = new ApiPersistenceActorProvider(new StubHttpContextAccessor { HttpContext = new DefaultHttpContext() },
													   diagnostics);

		PersistenceActor.Resolve(provider, diagnostics)
						.Should()
						.Be(PersistenceActor.Anonymous);
	}

	[Fact(DisplayName = "API provider without HTTP context resolves system actor")]
	public void ApiProviderWithoutHttpContextResolvesSystemActor()
	{
		var diagnostics = CreateDiagnostics();
		var provider = new ApiPersistenceActorProvider(new StubHttpContextAccessor(), diagnostics);

		PersistenceActor.Resolve(provider, diagnostics)
						.Should()
						.Be(ApiPersistenceActorProvider.SystemActor)
						.And
						.Be($"system:{typeof(Monaco.Template.Backend.Api.Program).Namespace}");
	}

	[Fact(DisplayName = "API actor ignores telemetry identity sources")]
	public void ApiActorIgnoresTelemetryIdentitySources()
	{
		using var activity = new Activity("tel-01").Start();
		activity.SetTag("user.id", "trace-user");
		activity.SetTag("service.name", "trace-service");
		activity.SetBaggage("user.id", "baggage-user");
		var diagnostics = CreateDiagnostics();
		var provider = new ApiPersistenceActorProvider(new StubHttpContextAccessor { HttpContext = AuthenticatedContext("trusted-sub") },
													   diagnostics);

		PersistenceActor.Resolve(provider, diagnostics)
						.Should()
						.Be("subject:trusted-sub");
	}

	[Fact(DisplayName = "Authenticated API HTTP with missing sub resolves null")]
	public void AuthenticatedApiHttpWithMissingSubResolvesNull() =>
		AssertRejectedActor(AuthenticatedWithoutSub(), PersistenceActor.MissingCode);

	[Fact(DisplayName = "Authenticated API HTTP with empty sub resolves null")]
	public void AuthenticatedApiHttpWithEmptySubResolvesNull() =>
		AssertRejectedActor(AuthenticatedContext(string.Empty), PersistenceActor.MissingCode);

	[Fact(DisplayName = "Authenticated API HTTP with whitespace-only sub resolves null")]
	public void AuthenticatedApiHttpWithWhitespaceOnlySubResolvesNull() =>
		AssertRejectedActor(AuthenticatedContext(" \t"), PersistenceActor.MissingCode);

	[Fact(DisplayName = "Authenticated API HTTP with control-bearing sub resolves null")]
	public void AuthenticatedApiHttpWithControlBearingSubResolvesNull() =>
		AssertRejectedActor(AuthenticatedContext("ok\u0001"), PersistenceActor.MalformedCode);

	[Fact(DisplayName = "Authenticated API HTTP with oversized sub resolves null")]
	public void AuthenticatedApiHttpWithOversizedSubResolvesNull()
	{
		var sub = new string('a', IAuditable.ActorMaxLength - PersistenceActor.SubjectPrefix.Length + 1);
		AssertRejectedActor(AuthenticatedContext(sub), PersistenceActor.OversizedCode);
	}

	private static void AssertRejectedActor(HttpContext httpContext, string expectedCode)
	{
		var logger = new CapturingLogger();
		var diagnostics = new PersistenceActorFallbackDiagnostics(logger, TimeProvider.System);
		var provider = new ApiPersistenceActorProvider(new StubHttpContextAccessor { HttpContext = httpContext }, diagnostics);

		PersistenceActor.Resolve(provider, diagnostics)
						.Should()
						.BeNull();
		logger.Entries
			  .Should()
			  .ContainSingle();
		var fields = DiagnosticFields(logger.Entries[0]);
		fields.Keys
			  .Should()
			  .BeEquivalentTo("code", "stage", "hostRole");
		fields["code"].Should()
					  .Be(expectedCode);
		fields["stage"].Should()
					   .Be(PersistenceActor.ResolutionStage);
		fields["hostRole"].Should()
						  .Be(PersistenceActor.ApiHostRole);
		logger.Entries[0]
			  .Exception
			  .Should()
			  .BeNull();
	}

	private static Dictionary<string, object?> DiagnosticFields(CapturedLog log) =>
		log.State
		   .Where(pair => pair.Key != "{OriginalFormat}")
		   .ToDictionary(pair => pair.Key, pair => pair.Value);

	private static DefaultHttpContext AuthenticatedContext(string sub) =>
		new()
		{
			User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", sub)], "Bearer"))
		};

	private static DefaultHttpContext AuthenticatedWithoutSub() =>
		new() { User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Bearer")) };

	private sealed class StubHttpContextAccessor : IHttpContextAccessor
	{
		public HttpContext? HttpContext { get; set; }
	}

	private sealed class CapturingLogger : ILogger<PersistenceActorFallbackDiagnostics>
	{
		public List<CapturedLog> Entries { get; } =
			[];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
			null;

		public bool IsEnabled(LogLevel logLevel) =>
			true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			var pairs = new List<KeyValuePair<string, object?>>();
			if (state is IEnumerable<KeyValuePair<string, object>> values)
				pairs.AddRange(values.Select(pair => new KeyValuePair<string, object?>(pair.Key, pair.Value)));

			Entries.Add(new CapturedLog(exception, pairs));
		}
	}

	private sealed record CapturedLog(Exception? Exception, IReadOnlyList<KeyValuePair<string, object?>> State);
#endif

#if (workerService)
	[Fact(DisplayName = "Worker provider resolves service actor without a subject")]
	public void WorkerProviderResolvesServiceActorWithoutASubject()
	{
		using var activity = new Activity("tel-01").Start();
		activity.SetTag("user.id", "trace-user");
		activity.SetTag("service.name", "trace-service");
		activity.SetBaggage("user.id", "header-user");
		var diagnostics = CreateDiagnostics();
		var provider = new WorkerPersistenceActorProvider();

		var actor = PersistenceActor.Resolve(provider, diagnostics);

		actor.Should()
			 .Be(WorkerPersistenceActorProvider.ServiceActor)
			 .And
			 .Be($"service:{typeof(Monaco.Template.Backend.Worker.Program).Namespace}");
		actor.Should()
			 .NotContain("subject:");
	}
#endif

	private static PersistenceActorFallbackDiagnostics CreateDiagnostics() =>
		new(NullLogger<PersistenceActorFallbackDiagnostics>.Instance, TimeProvider.System);
}
#endif