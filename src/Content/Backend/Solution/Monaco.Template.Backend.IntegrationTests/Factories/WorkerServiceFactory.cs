#if (massTransitIntegration)
using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Backend.Application.Features.Product;
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Monaco.Template.Backend.IntegrationTests.Factories;

public class WorkerServiceFactory : WebApplicationFactory<Worker.Program>
{
	private readonly AppFixture _fixture;

	public WorkerServiceFactory(AppFixture fixture)
	{
		_fixture = fixture;
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder) =>
		builder.UseConfiguration(new ConfigurationManager
								 {
									 ["ConnectionStrings:AppDbContext"] = _fixture.SqlConnectionString,
#if (filesSupport)
									 ["BlobStorage:ConnectionString"] = _fixture.StorageConnectionString,
#endif
#if (massTransitIntegration)
									 ["MessageBus:RabbitMQ:Host"] = _fixture.RabbitMqHost,
									 ["MessageBus:RabbitMQ:Port"] = _fixture.RabbitMqPort.ToString(),
									 ["MessageBus:RabbitMQ:Username"] = _fixture.RabbitMqUsername,
									 ["MessageBus:RabbitMQ:Password"] = _fixture.RabbitMqPassword
#endif
								 })
			   .Configure(_ => { });

	public WebApplicationFactory<Worker.Program> GetCustomFactory(Action<IWebHostBuilder> configure) =>
		WithWebHostBuilder(configure);
}
#if (massTransitIntegration)

public static class WorkerServiceFactoryExtensions
{
	extension(IWebHostBuilder builder)
	{
		public IWebHostBuilder AddMassTransitTestHarnessForWorker() =>
			builder.ConfigureServices((context, services) =>
										  services.AddMassTransitTestHarness(cfg =>
																			 {
																				 var rabbitMqConfig = context.Configuration.GetSection("MessageBus:RabbitMQ");
																				 if (rabbitMqConfig.Exists())
																					 cfg.UsingRabbitMq((ctx, busCfg) =>
																									   {
																										   busCfg.Host(rabbitMqConfig["Host"],
																													   ushort.Parse(rabbitMqConfig["Port"] ?? "5672"),
																													   rabbitMqConfig["VHost"],
																													   h =>
																													   {
																														   h.Username(rabbitMqConfig["Username"]!);
																														   h.Password(rabbitMqConfig["Password"]!);
																													   });

																										   busCfg.ConfigureEndpoints(ctx, new DefaultEndpointNameFormatter(true));
																									   });
																			 }));

		public IWebHostBuilder AddObservabilityConsumeRetry() =>
			builder.ConfigureServices(services =>
									  {
										  services.AddSingleton<OneShotConsumeFailure>();
										  services.AddSingleton<IConfigureReceiveEndpoint, ObservabilityConsumeRetryConfiguration>();
										  services.AddTransient(typeof(IPipelineBehavior<,>), typeof(OneShotConsumeBehavior<,>));
									  });
	}
}

[ExcludeFromCodeCoverage]
internal sealed class TransientConsumeException(string message) : Exception(message);

[ExcludeFromCodeCoverage]
internal sealed class OneShotConsumeFailure
{
	private Exception? _exception;
	private int _thrown;
	private Guid? _messageId;

	internal Guid? ThrownMessageId => _messageId;

	internal void Arm(Exception exception) =>
		_exception = exception;

	internal bool TryThrow(Guid? messageId, [NotNullWhen(true)] out Exception? exception)
	{
		exception = null;
		if (_exception is null || Interlocked.CompareExchange(ref _thrown, 1, 0) != 0)
			return false;

		_messageId = messageId;
		exception = _exception;
		return true;
	}
}

[ExcludeFromCodeCoverage]
internal sealed class ObservabilityConsumeRetryConfiguration : IConfigureReceiveEndpoint
{
	public void Configure(string name, IReceiveEndpointConfigurator configurator)
	{
		if (name.Contains("_bus_", StringComparison.OrdinalIgnoreCase))
			return;

		configurator.UseMessageRetry(retry =>
									 {
										 retry.Immediate(1);
										 retry.Handle<TransientConsumeException>();
									 });
	}
}

[ExcludeFromCodeCoverage]
internal sealed class OneShotConsumeBehavior<TRequest, TResponse>(IServiceProvider services, OneShotConsumeFailure failure) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		if (request is not LongRunningProcess.Command)
			return next(cancellationToken);

		var consumeContext = services.GetService<ConsumeContext>();
		if (consumeContext is not null && failure.TryThrow(consumeContext.MessageId, out var exception))
			throw exception;

		return next(cancellationToken);
	}
}
#endif