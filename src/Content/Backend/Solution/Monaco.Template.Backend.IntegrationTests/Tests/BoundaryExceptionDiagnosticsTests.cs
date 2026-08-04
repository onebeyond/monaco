#if (apiService)
using AwesomeAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.IntegrationTests.Apis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Observability")]
public sealed class BoundaryExceptionDiagnosticsTests(AppFixture fixture) : IntegrationTest(fixture)
{
#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
#if (auth)
		await SetupAccessToken([]);
#endif
	}

	[Fact(DisplayName = "An escaping API request receives the authoritative safe boundary response")]
	public async Task EscapingApiRequestReceivesTheAuthoritativeSafeBoundaryResponse()
	{
		await using var collector = await OtlpCollector.StartAsync();
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var activityListener = new ActivityListener
									 {
										 ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
										 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
										 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Status,
																											   activity.StatusDescription,
																											   activity.Tags.ToArray(),
																											   activity.Events.ToArray()))
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = Fixture.WebAppFactory
										 .GetCustomFactory(builder =>
														   {
															   builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
																																						   {
																																							   ["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.Endpoint,
																																							   ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
																																							   ["OTEL_BSP_SCHEDULE_DELAY"] = "1",
																																							   ["OTEL_BLRP_SCHEDULE_DELAY"] = "1",
																																							   ["OTEL_METRIC_EXPORT_INTERVAL"] = "1"
																																						   }));
															   builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
															   builder.ConfigureServices(services =>
																						 {
																							 services.RemoveAll<ISender>();
																							 services.AddSingleton<ISender, ThrowingSender>();
																						 });
														   });
		var api = GetApi<ICountriesApi>(factory);

		const string canary = "boundary-request-canary";
		var response = await api.Query([canary]);

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		var log = loggerProvider.Entries
								.Should()
								.ContainSingle(entry => entry.Category == "Monaco.Template.Backend.Common.Observability.Boundary")
								.Which;
		log.Exception.Should().BeOfType<InvalidOperationException>();
		loggerProvider.Entries.Where(entry => entry.Exception is not null).Should().ContainSingle();
		var span = activities.Should().ContainSingle(activity => activity.Status == ActivityStatusCode.Error).Which;
		span.StatusDescription.Should().BeEmpty();
		span.Tags.Should().Contain(new KeyValuePair<string, string?>("error.category", "unhandled"));
		span.Tags.Should().Contain(new KeyValuePair<string, string?>("error.type", typeof(InvalidOperationException).FullName));
		span.Tags.Should().NotContain(tag => tag.Key.StartsWith("exception.", StringComparison.Ordinal) || tag.Key == "error.code");
		span.Events.Should().BeEmpty();
		await collector.WaitForSignalsAsync();
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/logs");
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/traces");
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/metrics");
		collector.Entries.Select(entry => Encoding.UTF8.GetString(entry.Payload)).Should().NotContain(text => text.Contains(canary, StringComparison.Ordinal));
	}

	private sealed class ThrowingSender : ISender
	{
		public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
			Task.FromException<TResponse>(new InvalidOperationException("Boundary test failure."));

		public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
			Task.FromException(new InvalidOperationException("Boundary test failure."));

		public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
			Task.FromException<object?>(new InvalidOperationException("Boundary test failure."));

		public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();
	}

	private sealed record CapturedActivity(
		ActivityStatusCode Status,
		string? StatusDescription,
		IReadOnlyList<KeyValuePair<string, string?>> Tags,
		IReadOnlyList<ActivityEvent> Events);

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		internal ConcurrentQueue<CapturedLogEntry> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

		public void Dispose()
		{
		}
	}

	private sealed record CapturedLogEntry(string Category, Exception? Exception);

	private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new CapturedLogEntry(category, exception));
	}

	private sealed class OtlpCollector(WebApplication application, ConcurrentQueue<OtlpEntry> entries) : IAsyncDisposable
	{
		internal string Endpoint => application.Urls.Single();
		internal IReadOnlyCollection<OtlpEntry> Entries => entries;

		internal static async Task<OtlpCollector> StartAsync()
		{
			var entries = new ConcurrentQueue<OtlpEntry>();
			var builder = WebApplication.CreateBuilder();
			builder.WebHost.UseUrls("http://127.0.0.1:0");
			var application = builder.Build();
			application.MapPost("/{**path}", async context =>
											 {
												 await using var body = new MemoryStream();
												 await context.Request.Body.CopyToAsync(body);
												 entries.Enqueue(new OtlpEntry(context.Request.Path, body.ToArray()));
												 context.Response.StatusCode = StatusCodes.Status200OK;
											 });
			await application.StartAsync();
			return new OtlpCollector(application, entries);
		}

		internal async Task WaitForSignalsAsync()
		{
			for (var attempt = 0; attempt < 50 && entries.Select(entry => entry.Path).Distinct().Count() < 3; attempt++)
				await Task.Delay(100);
		}

		public ValueTask DisposeAsync() => application.DisposeAsync();
	}

	private sealed record OtlpEntry(string Path, byte[] Payload);
}
#endif