#if (apiService)
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using AwesomeAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.IntegrationTests.Apis;
using OpenTelemetry;
using OpenTelemetry.Trace;

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

	[Fact(DisplayName = "An escaping API request receives HTTP 500 with ordinary exception telemetry")]
	public async Task EscapingApiRequestReceivesHttp500WithOrdinaryExceptionTelemetry()
	{
		await using var collector = await OtlpLoopback.StartAsync();
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		var pipelineActivities = new ConcurrentQueue<CapturedActivity>();
		using var activityListener = new ActivityListener
									 {
										 ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
										 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
										 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																											   activity.Status,
																											   [.. activity.TagObjects]))
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = Fixture.WebAppFactory
										 .GetCustomFactory(builder =>
														   {
															   builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collector.Endpoint)));
															   builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
															   builder.ConfigureServices(services =>
																						 {
																							 services.RemoveAll<ISender>();
																							 services.AddSingleton<ISender, ThrowingSender>();
																							 services.ConfigureOpenTelemetryTracerProvider(providerBuilder =>
																																			   providerBuilder.AddProcessor(new CapturingActivityProcessor(pipelineActivities)));
																						 });
														   });
		var api = GetApi<ICountriesApi>(factory);

		var response = await api.Query(["boundary-request"]);

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		var log = loggerProvider.Entries
								.Should()
								.ContainSingle(entry => entry.Category == "Monaco.Template.Backend.Common.Observability.BoundaryExceptionHandler")
								.Which;
		log.Exception.Should().BeOfType<InvalidOperationException>();
		loggerProvider.Entries.Where(entry => entry.Exception is not null).Should().ContainSingle();
		activities.Should().ContainSingle(activity => activity.SourceName == "Microsoft.AspNetCore" && activity.Status == ActivityStatusCode.Error);
		await collector.WaitForSignalsAsync();
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/logs");
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/traces");
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/metrics");
		pipelineActivities.Should().Contain(activity => activity.SourceName == "Microsoft.AspNetCore");
		pipelineActivities.Should().NotContain(activity => TargetsEndpoint(activity, collector.Endpoint));
	}

	private static bool TargetsEndpoint(CapturedActivity activity, string endpoint)
	{
		if (activity.SourceName != "System.Net.Http")
			return false;

		var target = new Uri(endpoint);
		var requestUrl = activity.Tags.FirstOrDefault(tag => tag.Key is "url.full" or "http.url").Value?.ToString();
		if (Uri.TryCreate(requestUrl, UriKind.Absolute, out var requestUri))
			return requestUri.Scheme == target.Scheme && requestUri.Host == target.Host && requestUri.Port == target.Port;

		var address = activity.Tags.FirstOrDefault(tag => tag.Key is "server.address" or "net.peer.name").Value?.ToString();
		var port = activity.Tags.FirstOrDefault(tag => tag.Key is "server.port" or "net.peer.port").Value?.ToString();
		return address == target.Host && port == target.Port.ToString();
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
		string SourceName,
		ActivityStatusCode Status,
		IReadOnlyCollection<KeyValuePair<string, object?>> Tags);

	private sealed class CapturingActivityProcessor(ConcurrentQueue<CapturedActivity> activities) : BaseProcessor<Activity>
	{
		public override void OnEnd(Activity activity) =>
			activities.Enqueue(new CapturedActivity(activity.Source.Name, activity.Status, activity.TagObjects.ToArray()));
	}

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
}
#endif
