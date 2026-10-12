using AwesomeAssertions;
using Monaco.Eng.RuntimeShapeVerification.Drivers;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Monaco.Eng.RuntimeShapeVerification.Workloads;

[ExcludeFromCodeCoverage]
internal static class RuntimeWorkloads
{
	// Default metric export interval is 60s when no OTEL_* schedule overrides are allowed.
	private const int SignalWaitAttempts = 900;

	internal static async Task ExecuteAsync(GeneratedShape shape,
											InfrastructureFixture infra,
											DestinationMode mode,
											OtlpSignalReceiver? receiver)
	{
		infra.EnsureDocker();

		if (mode == DestinationMode.ReceiverAvailable)
		{
			receiver.Should().NotBeNull("Receiver available requires the Development OTLP receiver on :4317.");
			receiver.Clear();
		}

		var sql = infra.CreateIsolatedConnectionString($"{shape.Id}-{mode}");
		await GeneratedDbAccess.MigrateViaEfAsync(shape, sql);
		var armRetry = shape.Options is { Worker: true, MassTransit: true, Api: true };
		await using var hosts = await ShapeHostSet.StartAsync(shape, infra, sql, mode, mode == DestinationMode.ProductionOverride ? receiver : null, armRetry);

		var token = await AuthTokenHelper.TryGetAdministratorTokenAsync(infra, shape);
		var entryClient = shape.Options.Gateway ? hosts.GatewayClient! : hosts.ApiClient;
		if (entryClient is not null)
			AuthTokenHelper.AttachBearer(entryClient, token);
		if (hosts.ApiClient is not null && !ReferenceEquals(hosts.ApiClient, entryClient))
			AuthTokenHelper.AttachBearer(hosts.ApiClient, token);

		switch (shape.Shape)
		{
			case RuntimeShape.FullApiWorker:
			case RuntimeShape.FullApiWorkerWithoutTests:
				await RunFullApiWorkerAsync(shape, hosts, sql, entryClient!, receiver, mode, expectGateway: false);
				break;
			case RuntimeShape.GatewayFull:
				await RunFullApiWorkerAsync(shape, hosts, sql, entryClient!, receiver, mode, expectGateway: true);
				break;
			case RuntimeShape.LeanApi:
				await RunLeanApiAsync(shape, sql, entryClient!, receiver, mode);
				break;
			case RuntimeShape.PublisherApi:
				await RunPublisherApiAsync(shape, hosts, sql, entryClient!, receiver, mode);
				break;
			case RuntimeShape.WorkerConsumer:
				await WorkerConsumerDriver.ExecuteAsync(shape, hosts, infra, sql, receiver, mode);
				break;
			case RuntimeShape.WorkerBaseline:
				await WorkerBaselineDriver.ExecuteAsync(shape, hosts, sql, receiver, mode);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(shape), shape.Shape, "Unknown runtime shape.");
		}
	}

	private static async Task RunFullApiWorkerAsync(GeneratedShape shape,
													ShapeHostSet hosts,
													string sql,
													HttpClient entryClient,
													OtlpSignalReceiver? receiver,
													DestinationMode mode,
													bool expectGateway)
	{
		var companyName = $"co-{Guid.NewGuid():N}";
		var (createResponse, companyId) = await ApiCalls.CreateCompanyAsync(entryClient, companyName, $"{Guid.NewGuid():N}@example.test");
		createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		companyId.Should().NotBeNull();
		await AssertCompanyStampAsync(sql, companyId!.Value);

		var (productId, fileId) = await ApiCalls.CreateProductWithSeededImageAsync(entryClient, companyId.Value);
		await AssertProductAndFileStampsAsync(sql, productId, fileId);

		hosts.RetryMarkerPath.Should().NotBeNull();
		await WaitUntilAsync(() => Task.FromResult(File.Exists(hosts.RetryMarkerPath!)), attempts: 300);
		File.Exists(hosts.RetryMarkerPath).Should().BeTrue("Worker must throw the armed one-shot failure once.");

		await WaitUntilAsync(async () => await GeneratedDbAccess.CountInboxDeliveredAsync(sql) > 0, attempts: 300);
		(await GeneratedDbAccess.CountInboxDeliveredAsync(sql)).Should().BeGreaterThan(0);

		AssertAbsences(shape, expectGateway);
		await AssertSignalsAsync(shape, receiver, mode, expectGateway);
		if (mode != DestinationMode.ReceiverUnavailable)
			await AssertCompanyCounterAsync(shape, receiver!);
	}

	private static async Task RunLeanApiAsync(GeneratedShape shape,
											  string sql,
											  HttpClient entryClient,
											  OtlpSignalReceiver? receiver,
											  DestinationMode mode)
	{
		var companyName = $"lean-{Guid.NewGuid():N}";
		var (okResponse, companyId) = await ApiCalls.CreateCompanyAsync(entryClient, companyName, $"{Guid.NewGuid():N}@example.test");
		okResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		companyId.Should().NotBeNull();
		await AssertCompanyStampAsync(sql, companyId!.Value);

		var (badResponse, _) = await ApiCalls.CreateCompanyAsync(entryClient, "", "not-an-email");
		badResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var count = await GeneratedDbAccess.CountCompaniesByNameAsync(sql, companyName);
		count.Should().Be(1);

		AssertAbsences(shape, expectGateway: false);
		await AssertSignalsAsync(shape, receiver, mode, expectGateway: false);
		if (mode != DestinationMode.ReceiverUnavailable)
			await AssertCompanyCounterAsync(shape, receiver!);
	}

	private static async Task RunPublisherApiAsync(GeneratedShape shape,
												   ShapeHostSet hosts,
												   string sql,
												   HttpClient entryClient,
												   OtlpSignalReceiver? receiver,
												   DestinationMode mode)
	{
		hosts.WorkerServices.Should().BeNull("The publisher API must not start a Worker.");

		var companyName = $"pub-{Guid.NewGuid():N}";
		var (createResponse, companyId) = await ApiCalls.CreateCompanyAsync(entryClient, companyName, $"{Guid.NewGuid():N}@example.test");
		createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		companyId.Should().NotBeNull();
		await AssertCompanyStampAsync(sql, companyId!.Value);

		var sawOutbox = false;
		var outboxProbe = Task.Run(async () =>
								   {
									   for (var i = 0; i < 100 && !sawOutbox; i++)
										   if (await GeneratedDbAccess.CountOutboxMessagesAsync(sql) > 0)
											   sawOutbox = true;
										   else
											   await Task.Delay(20);
								   });

		var (productId, fileId) = await ApiCalls.CreateProductWithSeededImageAsync(entryClient, companyId.Value);
		await outboxProbe;
		await AssertProductAndFileStampsAsync(sql, productId, fileId);

		await WaitUntilAsync(async () => await GeneratedDbAccess.CountOutboxMessagesAsync(sql) == 0, attempts: 200);
		(await GeneratedDbAccess.CountOutboxMessagesAsync(sql)).Should().Be(0);
		sawOutbox.Should().BeTrue("The publisher API must show outbox publication after Product create.");
		(await GeneratedDbAccess.CountInboxDeliveredAsync(sql)).Should().Be(0, "The publisher API must not claim consumer completion.");

		AssertAbsences(shape, expectGateway: false);
		await AssertSignalsAsync(shape, receiver, mode, expectGateway: false);
		if (mode != DestinationMode.ReceiverUnavailable)
			await AssertCompanyCounterAsync(shape, receiver!);
	}


	private static void AssertAbsences(GeneratedShape shape, bool expectGateway)
	{
		Directory.Exists(shape.ProjectDir("Common.ApiGateway")).Should().Be(expectGateway);
		if (!shape.Options.Worker)
			Directory.Exists(shape.ProjectDir("Worker")).Should().BeFalse();
		if (!shape.Options.Api)
			Directory.Exists(shape.ProjectDir("Api")).Should().BeFalse();
		if (!shape.Options.Tests)
			Directory.Exists(shape.ProjectDir("IntegrationTests")).Should().BeFalse();
		if (!shape.Options.MassTransit)
			Directory.Exists(shape.ProjectDir("Messages")).Should().BeFalse();
		if (!shape.Options.Auth)
			Directory.Exists(Path.Combine(shape.ProjectDir("Api"), "Auth")).Should().BeFalse();
	}

	private static async Task AssertSignalsAsync(GeneratedShape shape,
												 OtlpSignalReceiver? receiver,
												 DestinationMode mode,
												 bool expectGateway)
	{
		if (mode == DestinationMode.ReceiverUnavailable)
		{
			receiver.Should().BeNull();
			return;
		}

		receiver.Should().NotBeNull();
		var expected = new List<string>();
		if (shape.Options.Api)
			expected.Add(shape.ServiceName("Api"));
		if (shape.Options.Worker)
			expected.Add(shape.ServiceName("Worker"));
		if (expectGateway)
			expected.Add(shape.ServiceName("Common.ApiGateway"));

		await receiver.WaitUntilAsync(() => expected.All(receiver.HasSignalKindsFor), attempts: SignalWaitAttempts);
		foreach (var name in expected)
			receiver.HasSignalKindsFor(name).Should().BeTrue(name);

		if (!expectGateway)
			receiver.GetServiceNames().Should().NotContain(name => name.EndsWith(".Common.ApiGateway", StringComparison.Ordinal));
	}

	private static async Task AssertCompanyCounterAsync(GeneratedShape shape, OtlpSignalReceiver receiver)
	{
		var apiName = shape.ServiceName("Api");
		await receiver.WaitUntilAsync(() => receiver.HasCounter(apiName, shape.ApplicationMeterName, "company.successful_creations"),
									  attempts: SignalWaitAttempts);
		receiver.HasCounter(apiName, shape.ApplicationMeterName, "company.successful_creations").Should().BeTrue();
	}

	private static async Task AssertCompanyStampAsync(string sql, Guid companyId)
	{
		var (createdBy, createdAt) = await GeneratedDbAccess.ReadCompanyStampAsync(sql, companyId);
		createdBy.Should().NotBeNullOrWhiteSpace();
		createdAt.Should().NotBeNull();
	}

	private static async Task AssertProductAndFileStampsAsync(string sql, Guid productId, Guid fileId)
	{
		var product = await GeneratedDbAccess.ReadProductStampAsync(sql, productId);
		product.CreatedBy.Should().NotBeNullOrWhiteSpace();
		product.CreatedAtUtc.Should().NotBeNull();

		var file = await GeneratedDbAccess.ReadFileStampAsync(sql, fileId);
		file.CreatedBy.Should().NotBeNullOrWhiteSpace();
		file.CreatedAtUtc.Should().NotBeNull();
	}

	private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int attempts)
	{
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			if (await predicate())
				return;
			await Task.Delay(100);
		}

		if (!await predicate())
			throw new TimeoutException("Workload condition was not met in time.");
	}
}

[ExcludeFromCodeCoverage]
internal static class ApiCalls
{
	internal static async Task<(HttpResponseMessage Response, Guid? Id)> CreateCompanyAsync(HttpClient client,
																							string name,
																							string email)
	{
		var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
											   {
												   ["name"] = name,
												   ["email"] = email,
												   ["webSiteUrl"] = null,
												   ["street"] = null,
												   ["city"] = null,
												   ["county"] = null,
												   ["postCode"] = null,
												   ["countryId"] = null
											   });
		using var content = new StringContent(payload,
											  Encoding.UTF8,
											  "application/json");
		var response = await client.PostAsync("api/v1/Companies", content);
		return (response, await ReadCreatedIdAsync(response));
	}

	internal static async Task<(Guid ProductId, Guid FileId)> CreateProductWithSeededImageAsync(HttpClient client, Guid companyId)
	{
		var fileId = await UploadSeedImageAsync(client);
		var title = $"product-{Guid.NewGuid():N}";
		var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
											   {
												   ["title"] = title,
												   ["description"] = "Runtime shape verification product",
												   ["price"] = 12.5m,
												   ["companyId"] = companyId,
												   ["pictures"] = new[] { fileId },
												   ["defaultPictureId"] = fileId
											   });
		using var content = new StringContent(payload,
											  Encoding.UTF8,
											  "application/json");
		using var response = await client.PostAsync("api/v1/Products", content);
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var productId = await ReadCreatedIdAsync(response);
		productId.Should().NotBeNull();
		return (productId!.Value, fileId);
	}

	private static async Task<Guid> UploadSeedImageAsync(HttpClient client)
	{
		var imagePath = FindSeedPng();
		await using var stream = File.OpenRead(imagePath);
		using var content = new MultipartFormDataContent();
		var part = new StreamContent(stream);
		part.Headers.ContentType = new("image/png");
		content.Add(part,
					"file",
					Path.GetFileName(imagePath));

		using var response = await client.PostAsync("api/v1/Files", content);
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var fileId = await ReadCreatedIdAsync(response);
		fileId.Should().NotBeNull();
		return fileId!.Value;
	}

	private static string FindSeedPng()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName,
										 "src",
										 "Content",
										 "Backend",
										 "Solution",
										 "Monaco.Template.Backend.IntegrationTests",
										 "Imports",
										 "Pictures",
										 "CSharp-Logo.png");
			if (File.Exists(candidate))
				return candidate;
		}

		throw new FileNotFoundException("Could not locate IntegrationTests Imports/Pictures/CSharp-Logo.png for product file seeding.");
	}

	private static async Task<Guid?> ReadCreatedIdAsync(HttpResponseMessage response)
	{
		if (response.Headers.Location is not null)
		{
			var location = response.Headers.Location.IsAbsoluteUri
							   ? response.Headers.Location
							   : new(new("http://localhost/"), response.Headers.Location);
			if (Guid.TryParse(location.Segments.LastOrDefault()?.TrimEnd('/'), out var fromLocation))
				return fromLocation;
		}

		if (!response.IsSuccessStatusCode)
			return null;

		var body = await response.Content.ReadAsStringAsync();
		if (string.IsNullOrWhiteSpace(body))
			return null;

		using var document = JsonDocument.Parse(body);
		if (document.RootElement.TryGetProperty("id", out var idProperty) && idProperty.TryGetGuid(out var parsed))
			return parsed;

		return null;
	}
}