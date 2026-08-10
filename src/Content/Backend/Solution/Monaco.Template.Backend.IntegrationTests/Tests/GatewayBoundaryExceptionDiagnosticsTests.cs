#if (apiGateway)
using AwesomeAssertions;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Common.Observability;
using GatewayProgram = Monaco.Template.Backend.Common.ApiGateway.Program;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Observability")]
public sealed class GatewayBoundaryExceptionDiagnosticsTests
{
	[Fact(DisplayName = "Gateway propagates W3C causality through native YARP forwarding")]
	public async Task GatewayPropagatesW3CCausalityThroughNativeYarpForwarding()
	{
		await using var collector = await OtlpCollector.StartAsync();
		await using var downstream = await DownstreamApi.StartAsync(collector.Endpoint);
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var activityListener = new ActivityListener
									 {
										 ShouldListenTo = source => source.Name is "Microsoft.AspNetCore" or "System.Net.Http" or "Yarp.ReverseProxy",
										 Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
										 ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																											   activity.TraceId,
																											   activity.SpanId,
																											   activity.ParentSpanId))
									 };
		ActivitySource.AddActivityListener(activityListener);
		await using var factory = new WebApplicationFactory<GatewayProgram>().WithWebHostBuilder(builder =>
																								 {
																									 builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
																																		   {
																																			   ["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.Endpoint,
																																			   ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
																																			   ["OTEL_BSP_SCHEDULE_DELAY"] = "1",
																																			   ["ReverseProxy:Clusters:api:Destinations:Template.Api:Address"] = downstream.Endpoint
																																		   }));
																									 builder.ConfigureServices(services =>
																															   {
																																   services.AddAuthorization(options =>
																																							 {
																																								 options.AddPolicy("files:write", policy => policy.RequireAssertion(_ => true));
																																								 options.AddPolicy("products:write", policy => policy.RequireAssertion(_ => true));
																																							 });
																																   services.PostConfigure<AuthorizationOptions>(options => options.DefaultPolicy = new AuthorizationPolicyBuilder()
																																	   .RequireAssertion(_ => true).Build());
																															   });
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

	private sealed record CapturedActivity(string SourceName, ActivityTraceId TraceId, ActivitySpanId SpanId, ActivitySpanId ParentSpanId);

	private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new CapturedLogEntry(category, logLevel, eventId, state!, exception));
	}

	private sealed class OtlpCollector(WebApplication application, ConcurrentQueue<OtlpEntry> entries) : IAsyncDisposable
	{
		private static readonly byte[] ServiceNameKey = Encoding.UTF8.GetBytes("service.name");

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

		internal async Task WaitForTraceResourcesAsync()
		{
			for (var attempt = 0; attempt < 50 && GetTraceResourceServiceNames().Distinct(StringComparer.Ordinal).Count() < 2; attempt++)
				await Task.Delay(100);
		}

		internal IReadOnlyCollection<string> GetTraceResourceServiceNames() =>
			entries.Where(entry => entry.Path == "/v1/traces")
				   .SelectMany(entry => ReadResourceServiceNames(entry.Payload))
				   .Distinct(StringComparer.Ordinal)
				   .ToArray();

		private static IReadOnlyCollection<string> ReadResourceServiceNames(byte[] payload)
		{
			var serviceNames = new List<string>();
			var remaining = payload.AsSpan();

			while (true)
			{
				var keyIndex = remaining.IndexOf(ServiceNameKey);
				if (keyIndex < 0)
					return serviceNames;

				var value = remaining[(keyIndex + ServiceNameKey.Length)..];
				if (TryReadServiceName(value, out var serviceName))
					serviceNames.Add(serviceName);

				remaining = value;
			}
		}

		private static bool TryReadServiceName(ReadOnlySpan<byte> value, out string serviceName)
		{
			serviceName = string.Empty;
			if (value.Length < 3 || value[0] != 0x12 || !TryReadLength(value[1..], out var anyValueLength, out var anyValueLengthBytes))
				return false;

			var anyValue = value.Slice(1 + anyValueLengthBytes, anyValueLength);
			if (anyValue.Length < 2 || anyValue[0] != 0x0a || !TryReadLength(anyValue[1..], out var stringLength, out var stringLengthBytes))
				return false;

			var stringValue = anyValue.Slice(1 + stringLengthBytes, stringLength);
			serviceName = Encoding.UTF8.GetString(stringValue);
			return true;
		}

		private static bool TryReadLength(ReadOnlySpan<byte> value, out int length, out int bytesRead)
		{
			length = 0;
			bytesRead = 0;

			for (var index = 0; index < value.Length && index < 5; index++)
			{
				var current = value[index];
				length |= (current & 0x7f) << (7 * index);
				bytesRead++;
				if ((current & 0x80) == 0)
					return length <= value.Length - bytesRead;
			}

			return false;
		}

		public ValueTask DisposeAsync() => application.DisposeAsync();
	}

	private sealed record OtlpEntry(string Path, byte[] Payload);

	private sealed class DownstreamApi(WebApplication application, ConcurrentQueue<string?> traceparents) : IAsyncDisposable
	{
		internal string Endpoint => application.Urls.Single();
		internal IReadOnlyCollection<string?> Traceparents => traceparents;

		internal static async Task<DownstreamApi> StartAsync(string collectorEndpoint)
		{
			var traceparents = new ConcurrentQueue<string?>();
			var builder = WebApplication.CreateBuilder();
			builder.WebHost.UseUrls("http://127.0.0.1:0");
			builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
														{
															["OTEL_EXPORTER_OTLP_ENDPOINT"] = collectorEndpoint,
															["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
															["OTEL_BSP_SCHEDULE_DELAY"] = "1",
															["OTEL_SERVICE_NAME"] = "Monaco.Template.Backend.Api"
														});
			builder.AddApiObservability();

			var application = builder.Build();
			application.MapGet("/api/observability-test", (HttpRequest request) =>
														  {
															  traceparents.Enqueue(request.Headers.TraceParent.SingleOrDefault());
															  return Results.NoContent();
														  });
			await application.StartAsync();
			return new DownstreamApi(application, traceparents);
		}

		public ValueTask DisposeAsync() => application.DisposeAsync();
	}
}
#endif