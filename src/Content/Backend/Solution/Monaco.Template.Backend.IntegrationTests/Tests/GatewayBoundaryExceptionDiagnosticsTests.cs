#if (apiGateway)
using AwesomeAssertions;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GatewayProgram = Monaco.Template.Backend.Common.ApiGateway.Program;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Observability")]
public sealed class GatewayBoundaryExceptionDiagnosticsTests
{
	[Fact(DisplayName = "An escaping Gateway request receives the authoritative safe boundary response")]
	public async Task EscapingGatewayRequestReceivesTheAuthoritativeSafeBoundaryResponse()
	{
		await using var collector = await OtlpCollector.StartAsync();
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<Activity>();
		using var activityListener = new ActivityListener
									 {
										 ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
										 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
										 ActivityStopped = activities.Enqueue
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = new WebApplicationFactory<GatewayProgram>().WithWebHostBuilder(builder =>
																								 {
																									 builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
																																				{
																																					["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.Endpoint,
																																					["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
																																					["OTEL_BSP_SCHEDULE_DELAY"] = "1",
																																					["OTEL_BLRP_SCHEDULE_DELAY"] = "1",
																																					["OTEL_METRIC_EXPORT_INTERVAL"] = "1"
																																				}));
																									 builder.ConfigureLogging(logging => logging.AddFilter("Microsoft.AspNetCore.Authentication.JwtBearer", LogLevel.None)
																																				.AddProvider(loggerProvider));
																									 builder.ConfigureServices(services =>
																															   {
																																   services.AddAuthorization(options =>
																																							 {
																																								 options.AddPolicy("files:write", policy => policy.RequireAssertion(_ => true));
																																								 options.AddPolicy("products:write", policy => policy.RequireAssertion(_ => true));
																																							 });
																																   services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
																																	options => options.Events = new JwtBearerEvents
																																								{
																																									OnMessageReceived =
																																										_ => throw new InvalidOperationException("Boundary test failure.")
																																								});
																															   });
																								 });
		using var client = factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri);

		const string canary = "gateway-request-canary";
		var response = await client.GetAsync($"/api/observability-test?probe={canary}");

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		var log = loggerProvider.Entries
								.Should()
								.ContainSingle(entry => entry.Category == "Monaco.Template.Backend.Common.Observability.Boundary")
								.Which;
		log.LogLevel.Should().Be(LogLevel.Error);
		log.EventId.Should().Be(new EventId(1000, "UnhandledBoundaryException"));
		log.Exception.Should().BeOfType<InvalidOperationException>();
		loggerProvider.Entries.Where(entry => entry.Exception is not null).Should().ContainSingle();
		var state = log.State.Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().Which;
		state[^1].Value.Should().Be("Unhandled {BoundaryKind} failure ({ExceptionType}); TraceId={TraceId}; SpanId={SpanId}");
		state.Select(field => field.Key).Should().Equal("BoundaryKind", "ExceptionType", "TraceId", "SpanId", "{OriginalFormat}");
		var span = activities.Should().ContainSingle(activity => activity.Status == ActivityStatusCode.Error).Which;
		span.StatusDescription.Should().BeEmpty();
		span.GetTagItem("error.category").Should().Be("unhandled");
		span.GetTagItem("error.type").Should().Be(typeof(InvalidOperationException).FullName);
		span.GetTagItem("error.code").Should().BeNull();
		span.Events.Should().BeEmpty();
		await collector.WaitForSignalsAsync();
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/logs");
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/traces");
		collector.Entries.Should().Contain(entry => entry.Path == "/v1/metrics");
		collector.Entries.Select(entry => Encoding.UTF8.GetString(entry.Payload)).Should().NotContain(text => text.Contains(canary, StringComparison.Ordinal));
	}

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		internal ConcurrentQueue<CapturedLogEntry> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

		public void Dispose()
		{
		}
	}

	private sealed record CapturedLogEntry(string Category, LogLevel LogLevel, EventId EventId, object State, Exception? Exception);

	private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new CapturedLogEntry(category, logLevel, eventId, state!, exception));
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