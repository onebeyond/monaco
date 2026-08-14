using AutoFixture.Xunit2;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.IntegrationTests.Apis;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Observability")]
public sealed class CompanyDependencyCorrelationTests(AppFixture fixture) : IntegrationTest(fixture)
{
#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RunScriptAsync(@"Scripts\Companies.sql");
#if (auth)
		await SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif
	}

	[Theory(DisplayName = "Company creation correlates the application log with native SQL activity")]
	[AutoData]
	public async Task CompanyCreationCorrelatesApplicationLogWithNativeSqlActivity(string name,
																				   string email)
	{
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var listener = new ActivityListener
							 {
								 ShouldListenTo = static _ => true,
								 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
								 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																									   activity.TraceId,
																									   activity.SpanId,
																									   activity.ParentSpanId,
																									   activity.GetTagItem("db.system.name") as string,
																									   activity.Status,
																									   activity.TagObjects.ToArray(),
																									   activity.Events.Select(@event => @event.Name).ToArray()))
							 };
		ActivitySource.AddActivityListener(listener);
		await using var factory = Fixture.WebAppFactory.GetCustomFactory(builder => builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider)));
		var api = GetApi<ICompaniesApi>(factory);
		var company = new CompanyCreateEditDto($"{name}-{Guid.NewGuid():N}",
											   $"{Guid.NewGuid():N}@{email.Split('@').LastOrDefault() ?? "example.test"}",
											   null,
											   null,
											   null,
											   null,
											   null,
											   null);

		var response = await api.Create(company);

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var applicationLog = loggerProvider.Entries.Should().ContainSingle(entry => entry.EventId == new EventId(2001, "CompanyCreated")).Which;
		applicationLog.Category.Should().Be("Monaco.Template.Backend.Application.Features.Company.CreateCompany.Handler");
		applicationLog.LogLevel.Should().Be(LogLevel.Information);
		applicationLog.Message.Should().Be("Company creation completed.");
		applicationLog.Exception.Should().BeNull();
		applicationLog.TraceId.Should().NotBe(null);
		applicationLog.SpanId.Should().NotBe(null);
		applicationLog.State.Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>()
					  .Which
					  .Select(field => field.Key)
					  .Should()
					  .Equal("{OriginalFormat}");

		var trace = activities.Where(activity => activity.TraceId == applicationLog.TraceId).ToArray();
		var serverActivity = trace.Should().ContainSingle(activity => activity.SourceName == "Microsoft.AspNetCore" && activity.SpanId == applicationLog.SpanId).Which;
		var sqlActivities = GetSqlActivities(trace);
		sqlActivities.Should().OnlyContain(activity => activity.ParentSpanId == serverActivity.SpanId);
		AssertSafeNativeSqlActivities(sqlActivities);
	}

	[Fact(DisplayName = "Company persistence failure retains native SQL attribution and one boundary diagnostic")]
	public async Task CompanyPersistenceFailureRetainsNativeSqlAttributionAndOneBoundaryDiagnostic()
	{
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var listener = new ActivityListener
							 {
								 ShouldListenTo = static _ => true,
								 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
								 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																									   activity.TraceId,
																									   activity.SpanId,
																									   activity.ParentSpanId,
																									   activity.GetTagItem("db.system.name") as string,
																									   activity.Status,
																									   activity.TagObjects.ToArray(),
																									   activity.Events.Select(@event => @event.Name).ToArray()))
							 };
		ActivitySource.AddActivityListener(listener);
		await using var factory = Fixture.WebAppFactory
										 .GetCustomFactory(builder => builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider))
																			 .ConfigureServices(services =>
																								{
																									services.RemoveAll<DbContextOptions<AppDbContext>>();
																									services.AddDbContext<AppDbContext>(options => options.UseSqlServer(Fixture.SqlConnectionString)
																																						  .AddInterceptors(new FailingDbCommandInterceptor()));
																								}));
		var api = GetApi<ICompaniesApi>(factory);
		var name = $"company-{Guid.NewGuid():N}";
		var response = await api.Create(new CompanyCreateEditDto(name,
																 $"{Guid.NewGuid():N}@example.test",
																 null,
																 null,
																 null,
																 null,
																 null,
																 null));

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		var boundaryLog = loggerProvider.Entries.Should().ContainSingle(entry => entry.Category == "Monaco.Template.Backend.Common.Observability.Boundary").Which;
		boundaryLog.LogLevel.Should().Be(LogLevel.Error);
		boundaryLog.EventId.Should().Be(new EventId(1000, "UnhandledBoundaryException"));
		boundaryLog.Exception.Should().NotBeNull();
		loggerProvider.Entries.Where(entry => entry.Category.StartsWith("Monaco.Template.Backend", StringComparison.Ordinal) && entry.Exception is not null)
					  .Should()
					  .ContainSingle();
		var state = boundaryLog.State.Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().Which;
		state[^1].Value.Should().Be("Unhandled {BoundaryKind} failure ({ExceptionType}); TraceId={TraceId}; SpanId={SpanId}");
		state.Select(field => field.Key).Should().Equal("BoundaryKind", "ExceptionType", "TraceId", "SpanId", "{OriginalFormat}");
		loggerProvider.Entries.Should().NotContain(entry => entry.EventId == new EventId(2001, "CompanyCreated"));
		var trace = activities.Where(activity => activity.TraceId == boundaryLog.TraceId).ToArray();
		var sqlActivities = GetSqlActivities(trace);
		sqlActivities.Should().ContainSingle(activity => activity.Status == ActivityStatusCode.Error);
		AssertSafeNativeSqlActivities(sqlActivities);
		var companyExists = await Fixture.GetDbContext(Fixture.WebAppFactory.Services).Set<Company>().AnyAsync(company => company.Name == name);
		companyExists.Should().BeFalse();
	}

	private static CapturedActivity[] GetSqlActivities(IEnumerable<CapturedActivity> trace)
	{
		var sqlActivities = trace.Where(activity => activity.DatabaseSystemName is not null).ToArray();
		sqlActivities.Should().NotBeEmpty();
		return sqlActivities;
	}

	private static void AssertSafeNativeSqlActivities(IReadOnlyCollection<CapturedActivity> sqlActivities)
	{
		sqlActivities.Should().OnlyContain(activity => activity.SourceName == "OpenTelemetry.Instrumentation.SqlClient" &&
													   activity.DatabaseSystemName == "microsoft.sql_server");
		sqlActivities.SelectMany(activity => activity.Tags)
					 .Where(tag => tag.Key == "db.statement" ||
								   tag.Key == "db.connection_string" ||
								   tag.Key == "db.user" ||
								   tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal))
					 .Should()
					 .BeEmpty();
		sqlActivities.SelectMany(activity => activity.Tags)
					 .Where(tag => tag.Key == "db.query.text")
					 .Select(tag => Convert.ToString(tag.Value))
					 .Except(["SELECT statement", "INSERT statement", "UPDATE statement", "DELETE statement", "EXEC statement"])
					 .Should()
					 .BeEmpty();
		sqlActivities.Should().OnlyContain(activity => activity.EventNames.Count == 0);
	}

	private sealed record CapturedActivity(string SourceName,
										   ActivityTraceId TraceId,
										   ActivitySpanId SpanId,
										   ActivitySpanId ParentSpanId,
										   string? DatabaseSystemName,
										   ActivityStatusCode Status,
										   IReadOnlyCollection<KeyValuePair<string, object?>> Tags,
										   IReadOnlyCollection<string> EventNames);

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		internal ConcurrentQueue<CapturedLogEntry> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

		public void Dispose()
		{
		}
	}

	private sealed class FailingDbCommandInterceptor : DbCommandInterceptor
	{
		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
																						 CommandEventData eventData,
																						 InterceptionResult<DbDataReader> result,
																						 CancellationToken cancellationToken = default)
		{
			if (command.CommandText.Contains("INSERT INTO [Company]", StringComparison.Ordinal))
				command.CommandText = "RAISERROR ('test persistence fault', 16, 1);";

			return ValueTask.FromResult(result);
		}
	}

	private sealed record CapturedLogEntry(string Category,
										   LogLevel LogLevel,
										   EventId EventId,
										   object State,
										   string Message,
										   Exception? Exception,
										   ActivityTraceId TraceId,
										   ActivitySpanId SpanId);

	private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel,
								EventId eventId,
								TState state,
								Exception? exception,
								Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new CapturedLogEntry(category,
												 logLevel,
												 eventId,
												 state!,
												 formatter(state, exception),
												 exception,
												 Activity.Current?.TraceId ?? default,
												 Activity.Current?.SpanId ?? default));
	}
}