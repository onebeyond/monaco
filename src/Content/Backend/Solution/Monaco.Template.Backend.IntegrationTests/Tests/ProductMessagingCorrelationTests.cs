#if (apiService && workerService && massTransitIntegration && filesSupport)
using AutoFixture.Xunit2;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using Google.Protobuf;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.IntegrationTests.Apis;
using Monaco.Template.Backend.IntegrationTests.Factories;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using File = System.IO.File;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Observability")]
public sealed class ProductMessagingCorrelationTests(AppFixture fixture) : IntegrationTest(fixture)
{
#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RunScriptAsync(@"Scripts\Products.sql");
		var images = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								  .Set<Image>()
								  .AsNoTracking()
								  .ToListAsync();
		var blobContainerClient = new BlobContainerClient(Fixture.StorageConnectionString, AppFixture.StorageContainer);
		foreach (var image in images)
		{
			var blobClient = blobContainerClient.GetBlobClient($"{(image.IsTemp ? "temp/" : string.Empty)}{image.Id}");
			await using var stream = File.OpenRead(@$"Imports\Pictures\{image.Name}{image.Extension}");
			await blobClient.UploadAsync(stream, overwrite: true);
		}
#if (auth)
		await SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif
	}

	[Theory(DisplayName = "Product create correlates native MassTransit activity with the completion log")]
	[AutoData]
	public async Task ProductCreateCorrelatesNativeMassTransitActivityWithTheCompletionLog(string title,
																						   string description,
																						   decimal price)
	{
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var listener = Listen(activities);
		await using var collector = await OtlpLoopback.StartAsync();
		await using var hosts = StartHosts(collector.Endpoint, loggerProvider);
		var dto = await CreateProductDto(hosts.Api, title, description, price);
		var api = GetApi<IProductsApi>(hosts.Api);

		var response = await api.Create(dto);

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var completionLog = await WaitForCompletionLog(loggerProvider);
		var connected = await WaitForConnectedSpans(collector, completionLog.TraceId.ToString());
		AssertCausalStory(connected, completionLog.TraceId.ToString());
		AssertDistinctApiAndWorkerMassTransitResources(connected);
		activities.Where(activity => activity.SourceName == "MassTransit")
				  .Should()
				  .Contain(activity => activity.TraceId == completionLog.TraceId);
		await collector.WaitUntilAsync(() => HasMassTransitMeters(collector), 200);
		collector.GetMeterNamesByServiceSuffix(".Api").Should().Contain(HostMetricNames.MassTransit);
		collector.GetMeterNamesByServiceSuffix(".Worker").Should().Contain(HostMetricNames.MassTransit);
	}

	[Theory(DisplayName = "A one-shot consume failure retries in the same causal story without committing or faulting")]
	[AutoData]
	public async Task OneShotConsumeFailureRetriesInTheSameCausalStoryWithoutCommittingOrFaulting(string title,
																								  string description,
																								  decimal price)
	{
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var listener = Listen(activities);
		await using var collector = await OtlpLoopback.StartAsync();
		await using var hosts = StartHosts(collector.Endpoint, loggerProvider);
		hosts.Worker.Services.GetRequiredService<OneShotConsumeFailure>()
			 .Arm(new TransientConsumeException("Transient consume failure."));
		var dto = await CreateProductDto(hosts.Api, title, description, price);
		var api = GetApi<IProductsApi>(hosts.Api);

		var response = await api.Create(dto);

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var completionLog = await WaitForCompletionLog(loggerProvider);
		var failure = hosts.Worker.Services.GetRequiredService<OneShotConsumeFailure>();
		failure.ThrownMessageId.Should().NotBeNull();
		loggerProvider.Entries.Should()
					  .Contain(entry => entry.Exception is TransientConsumeException &&
										entry.Exception.Message == "Transient consume failure.");
		var workerTrace = activities.Where(activity => activity.SourceName == "MassTransit")
									.ToArray();
		workerTrace.Should().Contain(activity => activity.Status == ActivityStatusCode.Error && activity.TraceId == completionLog.TraceId);
		workerTrace.Should().Contain(activity => activity.Status != ActivityStatusCode.Error && activity.TraceId == completionLog.TraceId);
		var connected = await WaitForConnectedSpans(collector, completionLog.TraceId.ToString());
		AssertCausalStory(connected, completionLog.TraceId.ToString());
		AssertDistinctApiAndWorkerMassTransitResources(connected);

		var dbContext = Fixture.GetDbContext(hosts.Worker.Services);
		(await dbContext.Set<InboxState>().CountAsync(state => state.Delivered != null)).Should().Be(1);
		var outbox = await dbContext.Set<OutboxMessage>().ToListAsync();
		outbox.Should().NotContain(message => message.MessageType.Contains("Fault", StringComparison.Ordinal));
	}

	[Theory(DisplayName = "An unhandled consume exception keeps native error span and ordinary log context")]
	[AutoData]
	public async Task UnhandledConsumeExceptionKeepsNativeErrorSpanAndOrdinaryLogContext(string title,
																						 string description,
																						 decimal price)
	{
		var loggerProvider = new CapturingLoggerProvider();
		var activities = new ConcurrentQueue<CapturedActivity>();
		using var listener = Listen(activities);
		await using var collector = await OtlpLoopback.StartAsync();
		await using var hosts = StartHosts(collector.Endpoint, loggerProvider);
		const string message = "Unhandled consume failure.";
		hosts.Worker
			 .Services
			 .GetRequiredService<OneShotConsumeFailure>()
			 .Arm(new InvalidOperationException(message));
		var dto = await CreateProductDto(hosts.Api, title, description, price);
		var api = GetApi<IProductsApi>(hosts.Api);

		var response = await api.Create(dto);

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		await WaitUntilAsync(() => loggerProvider.Entries.Any(entry => HasException<InvalidOperationException>(entry.Exception, message)));
		await WaitUntilAsync(() => activities.Any(activity => activity is { SourceName: "MassTransit", Status: ActivityStatusCode.Error }));
		var errorLog = loggerProvider.Entries.Should()
									 .Contain(entry => HasException<InvalidOperationException>(entry.Exception, message))
									 .Which;
		loggerProvider.Entries.Should().NotContain(entry => entry.EventId == new EventId(2000, "LongRunningProcessCompleted"));
		var errorActivity = activities.Should()
									  .Contain(activity => activity.SourceName == "MassTransit" && activity.Status == ActivityStatusCode.Error)
									  .Which;
		errorLog.TraceId.Should().Be(errorActivity.TraceId);
		await collector.WaitUntilAsync(() =>
									   {
										   try
										   {
											   return collector.GetSpans().Any(span => span.TraceId == errorActivity.TraceId.ToString() &&
																					   span.ScopeName == "MassTransit" &&
																					   span.IsError);
										   }
										   catch (InvalidProtocolBufferException)
										   {
											   return false;
										   }
										   catch (InvalidOperationException)
										   {
											   return false;
										   }
									   },
									   200);
		collector.GetSpans()
				 .Should()
				 .Contain(span => span.TraceId == errorActivity.TraceId.ToString() &&
								  span.ScopeName == "MassTransit" &&
								  span.IsError);
	}

	public override async Task DisposeAsync()
	{
		var container = new BlobContainerClient(Fixture.StorageConnectionString, AppFixture.StorageContainer);
		await Parallel.ForEachAsync(container.GetBlobs(),
									async (blob, ct) => await container.DeleteBlobAsync(blob.Name, cancellationToken: ct));
		await base.DisposeAsync();
	}

	private HostPair StartHosts(string collectorEndpoint, CapturingLoggerProvider loggerProvider)
	{
		var apiFactory = Fixture.WebAppFactory.GetCustomFactory(builder =>
																{
																	builder.UseSetting(WebHostDefaults.ApplicationKey, "Monaco.Template.Backend.Api");
																	builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collectorEndpoint)));
																});
		var workerFactory = Fixture.WorkerServiceFactory.GetCustomFactory(builder =>
																		  {
																			  builder.UseSetting(WebHostDefaults.ApplicationKey, "Monaco.Template.Backend.Worker");
																			  builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collectorEndpoint)));
																			  builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
																			  builder.AddObservabilityConsumeRetry();
																		  });
		_ = apiFactory.Services;
		_ = workerFactory.Services;
		return new HostPair(apiFactory, workerFactory);
	}

	private async Task<ProductCreateEditDto> CreateProductDto(WebApplicationFactory<Api.Program> apiFactory,
															  string title,
															  string description,
															  decimal price)
	{
		var dbContext = Fixture.GetDbContext(apiFactory.Services);
		var tempImages = await dbContext.Set<Image>()
										.Where(image => image.IsTemp && image.ThumbnailId.HasValue)
										.ToListAsync();
		return new ProductCreateEditDto($"{title}-{Guid.NewGuid():N}",
										description,
										price < 0 ? 0 : price,
										Guid.Parse("8CEFE8FA-F747-4A3A-D8C9-08DC18C76CDC"),
										[.. tempImages.Select(image => image.Id)],
										tempImages.Last().Id);
	}

	private static async Task<CapturedLogEntry> WaitForCompletionLog(CapturingLoggerProvider loggerProvider)
	{
		await WaitUntilAsync(() => loggerProvider.Entries.Any(entry => entry.EventId == new EventId(2000, "LongRunningProcessCompleted")), 200);
		var completionLog = loggerProvider.Entries.Should()
										  .ContainSingle(entry => entry.EventId == new EventId(2000, "LongRunningProcessCompleted"))
										  .Which;
		completionLog.LogLevel.Should().Be(LogLevel.Information);
		completionLog.EventId.Should().Be(new EventId(2000, "LongRunningProcessCompleted"));
		completionLog.Exception.Should().BeNull();
		completionLog.TraceId.Should().NotBe(default(ActivityTraceId));
		return completionLog;
	}

	private static async Task<DecodedSpan[]> WaitForConnectedSpans(OtlpLoopback collector, string seedTraceId)
	{
		await collector.WaitUntilAsync(() =>
									   {
										   try
										   {
											   var connected = GetConnectedSpans(collector.GetSpans(), seedTraceId);
											   var massTransitServices = connected.Where(span => span.ScopeName == "MassTransit")
																				  .Select(span => span.ServiceName)
																				  .Distinct(StringComparer.Ordinal)
																				  .ToArray();
											   return massTransitServices.Any(name => name.EndsWith(".Api", StringComparison.Ordinal)) &&
													  massTransitServices.Any(name => name.EndsWith(".Worker", StringComparison.Ordinal)) &&
													  collector.GetLogs().Any(log => log.TraceId == seedTraceId ||
																					 connected.Any(span => span.TraceId == log.TraceId));
										   }
										   catch (InvalidProtocolBufferException)
										   {
											   return false;
										   }
										   catch (InvalidOperationException)
										   {
											   return false;
										   }
									   },
									   200);
		return GetConnectedSpans(collector.GetSpans(), seedTraceId);
	}

	private static void AssertDistinctApiAndWorkerMassTransitResources(IReadOnlyCollection<DecodedSpan> connected)
	{
		var serviceNames = connected.Where(span => span.ScopeName == "MassTransit")
									.Select(span => span.ServiceName)
									.Distinct(StringComparer.Ordinal)
									.ToArray();
		serviceNames.Should().Contain(name => name.EndsWith(".Api", StringComparison.Ordinal));
		serviceNames.Should().Contain(name => name.EndsWith(".Worker", StringComparison.Ordinal));
	}

	private static bool HasMassTransitMeters(OtlpLoopback collector)
	{
		try
		{
			return collector.GetMeterNamesByServiceSuffix(".Api").Contains(HostMetricNames.MassTransit) &&
				   collector.GetMeterNamesByServiceSuffix(".Worker").Contains(HostMetricNames.MassTransit);
		}
		catch (InvalidProtocolBufferException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static void AssertCausalStory(IReadOnlyCollection<DecodedSpan> connected, string seedTraceId)
	{
		var massTransit = connected.Where(span => span.ScopeName == "MassTransit").ToArray();
		massTransit.Should().NotBeEmpty();
		massTransit.Should()
				   .Contain(span => span.TraceId == seedTraceId ||
									span.Links.Any(link => HasId(link.TraceId) && link.TraceId == seedTraceId));
	}

	private static DecodedSpan[] GetConnectedSpans(IReadOnlyList<DecodedSpan> spans, string seedTraceId)
	{
		var traceIds = new HashSet<string>(StringComparer.Ordinal) { seedTraceId };
		var spanIds = new HashSet<string>(StringComparer.Ordinal);
		bool changed;
		do
		{
			changed = false;
			foreach (var span in spans)
			{
				var connected = (HasId(span.TraceId) && traceIds.Contains(span.TraceId)) ||
								(HasId(span.SpanId) && spanIds.Contains(span.SpanId)) ||
								(HasId(span.ParentSpanId) && spanIds.Contains(span.ParentSpanId)) ||
								span.Links.Any(link => (HasId(link.TraceId) && traceIds.Contains(link.TraceId)) ||
													   (HasId(link.SpanId) && spanIds.Contains(link.SpanId)));
				if (!connected)
					continue;
				if (HasId(span.TraceId))
					changed |= traceIds.Add(span.TraceId);
				if (HasId(span.SpanId))
					changed |= spanIds.Add(span.SpanId);
				if (HasId(span.ParentSpanId))
					changed |= spanIds.Add(span.ParentSpanId);
				foreach (var link in span.Links)
				{
					if (HasId(link.TraceId))
						changed |= traceIds.Add(link.TraceId);
					if (HasId(link.SpanId))
						changed |= spanIds.Add(link.SpanId);
				}
			}
		} while (changed);

		return
		[
			.. spans.Where(span => (HasId(span.TraceId) && traceIds.Contains(span.TraceId)) ||
								   (HasId(span.SpanId) && spanIds.Contains(span.SpanId)))
		];
	}

	private static bool HasId(string id) =>
		id.Length > 0 && !id.All(static character => character == '0');

	private static ActivityListener Listen(ConcurrentQueue<CapturedActivity> activities)
	{
		var listener = new ActivityListener
					   {
						   ShouldListenTo = static source => source.Name is "MassTransit" or "Microsoft.AspNetCore",
						   Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
						   ActivityStopped = activity => activities.Enqueue(new CapturedActivity(activity.Source.Name,
																								 activity.TraceId,
																								 activity.Status))
					   };
		ActivitySource.AddActivityListener(listener);
		return listener;
	}

	private static async Task WaitUntilAsync(Func<bool> predicate, int attempts = 200)
	{
		for (var attempt = 0; attempt < attempts && !predicate(); attempt++)
			await Task.Delay(100);

		if (!predicate())
			throw new TimeoutException("The expected messaging observability evidence was not observed in time.");
	}

	private static bool HasException<T>(Exception? exception, string message) where T : Exception
	{
		for (var current = exception; current is not null; current = current.InnerException)
			if (current is T && current.Message == message)
				return true;

		return false;
	}

	private sealed record CapturedActivity(string SourceName, ActivityTraceId TraceId, ActivityStatusCode Status);

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		internal ConcurrentQueue<CapturedLogEntry> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

		public void Dispose()
		{
		}
	}

	private sealed record CapturedLogEntry(LogLevel LogLevel,
										   EventId EventId,
										   Exception? Exception,
										   ActivityTraceId TraceId);

	private sealed class CapturingLogger(ConcurrentQueue<CapturedLogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel,
								EventId eventId,
								TState state,
								Exception? exception,
								Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new CapturedLogEntry(logLevel,
												 eventId,
												 exception,
												 Activity.Current?.TraceId ?? default));
	}
}

internal sealed class HostPair(WebApplicationFactory<Api.Program> api,
							   WebApplicationFactory<Worker.Program> worker) : IAsyncDisposable
{
	public WebApplicationFactory<Api.Program> Api { get; } = api;
	public WebApplicationFactory<Worker.Program> Worker { get; } = worker;

	public async ValueTask DisposeAsync()
	{
		await Worker.DisposeAsync();
		await Api.DisposeAsync();
	}
}
#endif