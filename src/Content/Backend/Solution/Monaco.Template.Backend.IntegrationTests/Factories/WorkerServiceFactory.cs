using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Monaco.Template.Backend.IntegrationTests.Factories;

public class WorkerServiceFactory : WebApplicationFactory<Service.Program>
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
			   .ConfigureServices((context, services) =>
								  {
									  var configuration = context.Configuration;
									  services.AddMassTransitTestHarness(cfg =>
																		 {
																			 var rabbitMqConfig = configuration.GetSection("MessageBus:RabbitMQ");
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
																		 });
								  })
			   .Configure(_ => { });

	public IHost GetHostInstance() =>
		Services.GetRequiredService<IHost>();
}