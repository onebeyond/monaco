#if (apiGateway)
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Common.Observability;
using OpenTelemetry;
using OpenTelemetry.Trace;
using GatewayProgram = Monaco.Template.Backend.Common.ApiGateway.Program;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Observability")]
public sealed class GatewayBoundaryExceptionDiagnosticsTests
{
	[Fact(DisplayName = "Gateway propagates W3C causality through native YARP forwarding")]
	public async Task GatewayPropagatesW3CCausalityThroughNativeYarpForwarding()
	{
		await using var collector = await OtlpLoopback.StartAsync();
		await using var downstream = await DownstreamApi.StartAsync(collector.Endpoint);
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var activityListener = new ActivityListener
									 {
										 ShouldListenTo = source => source.Name is "Microsoft.AspNetCore" or "System.Net.Http" or "Yarp.ReverseProxy",
										 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
										 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																											   activity.TraceId,
																											   activity.SpanId,
																											   activity.ParentSpanId,
																											   activity.Status,
																											   activity.TagObjects.ToArray()))
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = new WebApplicationFactory<GatewayProgram>()
			.WithWebHostBuilder(builder =>
								{
									builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>(OtlpLoopback.ExporterConfiguration(collector.Endpoint))
																																{
																																	["ReverseProxy:Clusters:api:Destinations:Template.Api:Address"] = downstream.Endpoint
																																}));
									builder.ConfigureServices(services => services.AddAuthorization(options =>
																									{
																										options.AddPolicy("files:write", policy => policy.RequireAssertion(_ => true));
																										options.AddPolicy("products:write", policy => policy.RequireAssertion(_ => true));
																									})
																				  .PostConfigure<AuthorizationOptions>(options => options.DefaultPolicy = new AuthorizationPolicyBuilder()
																																						  .RequireAssertion(_ => true).Build()));
								});
		using var client = factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri);

		const string traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
		ActivityContext.TryParse(traceparent, null, out var incomingContext).Should().BeTrue();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/api/observability-test");
		request.Headers.TryAddWithoutValidation("traceparent", traceparent);

		var response = await client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		var propagatedTraceparent = downstream.Traceparents.Should().ContainSingle().Which;
		ActivityContext.TryParse(propagatedTraceparent, null, out var downstreamContext).Should().BeTrue();
		downstreamContext.TraceId.Should().Be(incomingContext.TraceId);
		await collector.WaitForTraceResourcesAsync();

		var trace = activities.Where(activity => activity.TraceId == incomingContext.TraceId).ToArray();
		var gatewayServer = trace.Should().ContainSingle(activity => activity.SourceName == "Microsoft.AspNetCore" && activity.ParentSpanId == incomingContext.SpanId).Which;
		var yarpForward = trace.Should().ContainSingle(activity => activity.SourceName == "Yarp.ReverseProxy" && activity.ParentSpanId == gatewayServer.SpanId).Which;
		var gatewayClient = trace.Should().ContainSingle(activity => activity.SourceName == "System.Net.Http" && activity.ParentSpanId == yarpForward.SpanId).Which;
		_ = trace.Should().ContainSingle(activity => activity.SourceName == "Microsoft.AspNetCore" && activity.ParentSpanId == gatewayClient.SpanId);
		var exportedServiceNames = collector.GetTraceResourceServiceNames();
		exportedServiceNames.Should().Contain("Monaco.Template.Backend.Api");
		exportedServiceNames.Should().Contain(serviceName => serviceName != "Monaco.Template.Backend.Api");
	}

	[Fact(DisplayName = "Gateway records a native HttpClient dependency when its test downstream returns 503")]
	public async Task GatewayRecordsNativeHttpClientDependencyWhenTestDownstreamReturns503()
	{
		await using var collector = await OtlpLoopback.StartAsync();
		await using var downstream = await DownstreamApi.StartAsync(collector.Endpoint, StatusCodes.Status503ServiceUnavailable);
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var activityListener = new ActivityListener
									 {
										 ShouldListenTo = source => source.Name is "Microsoft.AspNetCore" or "System.Net.Http" or "Yarp.ReverseProxy",
										 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
										 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																											   activity.TraceId,
																											   activity.SpanId,
																											   activity.ParentSpanId,
																											   activity.Status,
																											   activity.TagObjects.ToArray()))
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = new WebApplicationFactory<GatewayProgram>()
			.WithWebHostBuilder(builder =>
								{
									builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>(OtlpLoopback.ExporterConfiguration(collector.Endpoint))
																																{
																																	["ReverseProxy:Clusters:api:Destinations:Template.Api:Address"] = downstream.Endpoint
																																}));
									builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
									builder.ConfigureServices(services => services.AddAuthorization(options =>
																									{
																										options.AddPolicy("files:write", policy => policy.RequireAssertion(_ => true));
																										options.AddPolicy("products:write",
																														  policy => policy.RequireAssertion(_ => true));
																									})
																				  .PostConfigure<AuthorizationOptions>(options => options.DefaultPolicy =
																																	  new AuthorizationPolicyBuilder()
																																		  .RequireAssertion(_ => true).Build()));
								});
		using var client = factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri);

		const string traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
		ActivityContext.TryParse(traceparent, null, out var incomingContext).Should().BeTrue();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/api/observability-test");
		request.Headers.TryAddWithoutValidation("traceparent", traceparent);

		var response = await client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
		var trace = activities.Where(activity => activity.TraceId == incomingContext.TraceId).ToArray();
		var gatewayServer = trace.Should().ContainSingle(activity => activity.SourceName == "Microsoft.AspNetCore" && activity.ParentSpanId == incomingContext.SpanId).Which;
		var yarpForward = trace.Should().ContainSingle(activity => activity.SourceName == "Yarp.ReverseProxy" && activity.ParentSpanId == gatewayServer.SpanId).Which;
		var gatewayClient = trace.Should().ContainSingle(activity => activity.SourceName == "System.Net.Http" && activity.ParentSpanId == yarpForward.SpanId).Which;
		gatewayClient.Status.Should().Be(ActivityStatusCode.Error);
		Convert.ToString(gatewayClient.Tags.Single(tag => tag.Key == "http.response.status_code").Value).Should().Be("503");
		trace.Select(activity => activity.SourceName)
			 .Except(["Microsoft.AspNetCore", "System.Net.Http", "Yarp.ReverseProxy"])
			 .Should()
			 .BeEmpty();
		trace.Count(activity => activity.SourceName == "System.Net.Http").Should().Be(1);
		trace.Count(activity => activity.SourceName == "Yarp.ReverseProxy").Should().Be(1);
		var propagatedTraceparent = downstream.Traceparents.Should().ContainSingle().Which;
		ActivityContext.TryParse(propagatedTraceparent, null, out var downstreamContext).Should().BeTrue();
		downstreamContext.TraceId.Should().Be(incomingContext.TraceId);
		downstreamContext.SpanId.Should().Be(gatewayClient.SpanId);
		loggerProvider.Entries.Should().NotContain(entry => entry.Category.StartsWith("Monaco.Template.Backend", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "An escaping Gateway request receives HTTP 500 with ordinary exception telemetry")]
	public async Task EscapingGatewayRequestReceivesHttp500WithOrdinaryExceptionTelemetry()
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
																											   activity.TraceId,
																											   activity.SpanId,
																											   activity.ParentSpanId,
																											   activity.Status,
																											   activity.TagObjects.ToArray()))
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = new WebApplicationFactory<GatewayProgram>()
			.WithWebHostBuilder(builder =>
								{
									builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collector.Endpoint)));
									builder.ConfigureLogging(logging => logging.AddFilter("Microsoft.AspNetCore.Authentication.JwtBearer", LogLevel.None)
																			   .AddProvider(loggerProvider));
									builder.ConfigureServices(services => services.AddAuthorization(options =>
																									{
																										options.AddPolicy("files:write", policy => policy.RequireAssertion(_ => true));
																										options.AddPolicy("products:write", policy => policy.RequireAssertion(_ => true));
																									})
																				  .PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
																												   options => options.Events = new JwtBearerEvents
																																			   {
																																				   OnMessageReceived = _ => throw new InvalidOperationException("Boundary test failure.")
																																			   }));
									builder.ConfigureServices(services => services.ConfigureOpenTelemetryTracerProvider(providerBuilder =>
																															providerBuilder.AddProcessor(new CapturingActivityProcessor(pipelineActivities))));
								});
		using var client = factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri);

		var response = await client.GetAsync("/api/observability-test?probe=boundary-request");

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		var log = loggerProvider.Entries
								.Should()
								.ContainSingle(entry => entry.Category == "Monaco.Template.Backend.Common.Observability.BoundaryExceptionHandler")
								.Which;
		log.LogLevel.Should().Be(LogLevel.Error);
		log.Exception.Should().BeOfType<InvalidOperationException>();
		loggerProvider.Entries.Where(entry => entry.Exception is not null).Should().ContainSingle();
		var state = log.State.Should().BeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().Which;
		state[^1].Value.Should().Be("Unhandled HTTP request failure.");
		state.Select(field => field.Key).Should().Equal("{OriginalFormat}");
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

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		internal ConcurrentQueue<CapturedLogEntry> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

		public void Dispose()
		{
		}
	}

	private sealed record CapturedLogEntry(string Category, LogLevel LogLevel, EventId EventId, object State, Exception? Exception);

	private sealed record CapturedActivity(
		string SourceName,
		ActivityTraceId TraceId,
		ActivitySpanId SpanId,
		ActivitySpanId ParentSpanId,
		ActivityStatusCode Status,
		IReadOnlyCollection<KeyValuePair<string, object?>> Tags);

	private sealed class CapturingActivityProcessor(ConcurrentQueue<CapturedActivity> activities) : BaseProcessor<Activity>
	{
		public override void OnEnd(Activity activity) =>
			activities.Enqueue(new CapturedActivity(activity.Source.Name,
													activity.TraceId,
													activity.SpanId,
													activity.ParentSpanId,
													activity.Status,
													activity.TagObjects.ToArray()));
	}

	private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new CapturedLogEntry(category, logLevel, eventId, state!, exception));
	}

	private sealed class DownstreamApi(WebApplication application, ConcurrentQueue<string?> traceparents) : IAsyncDisposable
	{
		internal string Endpoint => application.Urls.Single();
		internal IReadOnlyCollection<string?> Traceparents => traceparents;

		internal static async Task<DownstreamApi> StartAsync(string collectorEndpoint, int responseStatus = StatusCodes.Status204NoContent)
		{
			var traceparents = new ConcurrentQueue<string?>();
			var builder = WebApplication.CreateBuilder();
			builder.WebHost.UseUrls("http://127.0.0.1:0");
			builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(OtlpLoopback.ExporterConfiguration(collectorEndpoint))
														{
															["OTEL_SERVICE_NAME"] = "Monaco.Template.Backend.Api"
														});
			builder.AddApiObservability();

			var application = builder.Build();
			application.MapGet("/api/observability-test", (HttpRequest request) =>
														  {
															  traceparents.Enqueue(request.Headers.TraceParent.SingleOrDefault());
															  return Results.StatusCode(responseStatus);
														  });
			await application.StartAsync();
			return new DownstreamApi(application, traceparents);
		}

		public ValueTask DisposeAsync() => application.DisposeAsync();
	}
}
#endif