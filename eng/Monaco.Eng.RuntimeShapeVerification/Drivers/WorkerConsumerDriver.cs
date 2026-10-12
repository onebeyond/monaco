using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AwesomeAssertions;
using MassTransit;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;

namespace Monaco.Eng.RuntimeShapeVerification.Drivers;

[ExcludeFromCodeCoverage]
internal static class WorkerConsumerDriver
{
	internal static async Task ExecuteAsync(GeneratedShape shape,
											ShapeHostSet hosts,
											InfrastructureFixture infra,
											string sql,
											OtlpSignalReceiver? receiver,
											DestinationMode mode)
	{
		hosts.ApiClient.Should().BeNull();
		hosts.WorkerServices.Should().BeNull();
		Directory.Exists(shape.ProjectDir("Api")).Should().BeFalse();
		Directory.Exists(shape.ProjectDir("IntegrationTests")).Should().BeFalse();

		var companyId = Guid.NewGuid();
		await PublishProductCreatedAsync(shape, infra, companyId);
		await WaitUntilAsync(async () => await GeneratedDbAccess.CountInboxDeliveredAsync(sql) > 0, attempts: 300);
		(await GeneratedDbAccess.CountInboxDeliveredAsync(sql)).Should().BeGreaterThan(0);

		hosts.StampMarkerPath.Should().NotBeNull();
		await WaitUntilAsync(() => Task.FromResult(File.Exists(hosts.StampMarkerPath!)), attempts: 300);
		var stampedId = Guid.Parse(await File.ReadAllTextAsync(hosts.StampMarkerPath!));
		var (createdBy, createdAt) = await GeneratedDbAccess.ReadCompanyStampAsync(sql, stampedId);
		createdBy.Should().NotBeNullOrWhiteSpace();
		createdAt.Should().NotBeNull();

		if (mode != DestinationMode.ReceiverUnavailable)
		{
			receiver.Should().NotBeNull();
			await receiver.WaitUntilAsync(() => receiver.HasSignalKindsFor(shape.ServiceName("Worker")), attempts: 900);
			receiver.HasSignalKindsFor(shape.ServiceName("Worker")).Should().BeTrue();
			receiver.HasCounter(shape.ServiceName("Worker"),
								shape.ApplicationMeterName,
								"company.successful_creations")
					.Should()
					.BeFalse();
		}
	}

	private static IBusControl? _publisher;
	private static readonly SemaphoreSlim PublisherGate = new(1, 1);

	internal static async Task PublishProductCreatedAsync(GeneratedShape shape,
														  InfrastructureFixture infra,
														  Guid companyId)
	{
		var messagesDll = Path.Combine(shape.ProjectDir("Messages"),
									   "bin",
									   "Debug",
									   "net10.0",
									   $"{shape.Name}.Messages.dll");
		Assembly.LoadFrom(messagesDll);
		var messageType = FindType(shape,
								   "ProductCreated",
								   ".Messages");
		var message = Activator.CreateInstance(messageType,
											   Guid.NewGuid(),
											   $"product-{Guid.NewGuid():N}",
											   "description",
											   12.5m,
											   companyId)!;

		await PublisherGate.WaitAsync();
		try
		{
			if (_publisher is null)
			{
				_publisher = Bus.Factory.CreateUsingRabbitMq(cfg =>
															 {
																 cfg.Host(infra.RabbitMqHost,
																		  (ushort)infra.RabbitMqPort,
																		  InfrastructureFixture.RabbitMqVHost,
																		  host =>
																		  {
																			  host.Username(infra.RabbitMqUsername);
																			  host.Password(infra.RabbitMqPassword);
																		  });
															 });
				await _publisher.StartAsync();
			}

			await PublishDynamicAsync(_publisher,
									  messageType,
									  message);
		}
		finally
		{
			PublisherGate.Release();
		}
	}

	private static async Task PublishDynamicAsync(IPublishEndpoint bus,
												  Type messageType,
												  object message)
	{
		var publish = typeof(IPublishEndpoint)
					  .GetMethods()
					  .First(method => method is { Name: nameof(IPublishEndpoint.Publish), IsGenericMethodDefinition: true } &&
									   method.GetParameters().Length == 2 &&
									   method.GetParameters()[1].ParameterType == typeof(CancellationToken));
		var task = (Task)publish.MakeGenericMethod(messageType).Invoke(bus, [message, CancellationToken.None])!;
		await task;
	}

	private static Type FindType(GeneratedShape shape,
								 string name,
								 string namespaceFragment) =>
		AppDomain.CurrentDomain
				 .GetAssemblies()
				 .SelectMany(SafeGetTypes)
				 .First(type => type.Name == name &&
								(type.Namespace?.StartsWith(shape.Name, StringComparison.Ordinal) ?? false) &&
								(type.Namespace?.Contains(namespaceFragment, StringComparison.Ordinal) ?? false));

	private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int attempts)
	{
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			if (await predicate())
				return;
			await Task.Delay(100);
		}

		if (!await predicate())
			throw new TimeoutException("Worker consumer evidence was not observed in time.");
	}

	private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(type => type is not null)!;
		}
	}
}