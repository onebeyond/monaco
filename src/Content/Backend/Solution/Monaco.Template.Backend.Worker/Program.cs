#if (massTransitIntegration)
using MassTransit;
#endif
using Monaco.Template.Backend.Application.DependencyInjection;
using Monaco.Template.Backend.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
builder.Logging
	   .ClearProviders()
	   .Services
	   .AddSerilog(cfg => cfg.ReadFrom.Configuration(configuration))
	   .ConfigureApplication(options =>
							 {
								 options.EntityFramework.ConnectionString = configuration.GetConnectionString("AppDbContext")!;
								 options.EntityFramework.EnableEfSensitiveLogging = bool.Parse(configuration["EnableEFSensitiveLogging"] ?? bool.FalseString);
#if (filesSupport)
								 options.BlobStorage.ConnectionString = configuration["BlobStorage:ConnectionString"]!;
								 options.BlobStorage.ContainerName = configuration["BlobStorage:Container"]!;
#endif
							 })
#if (massTransitIntegration)
	   .AddMassTransit(cfg =>
					   {
						   cfg.AddConsumersFromNamespaceContaining<Worker>();
						   cfg.AddActivitiesFromNamespaceContaining<Worker>();
						   
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
						   else //For all other environments, use Azure Service Bus
							   cfg.UsingAzureServiceBus((ctx, busCfg) =>
														{
															busCfg.Host(configuration["MessageBus:ASBConnectionString"]);
															busCfg.ConfigureEndpoints(ctx, new DefaultEndpointNameFormatter(true));
														});
					   })
#endif
	   .AddHostedService<Worker>();

var host = builder.Build();
host.Run();

namespace Monaco.Template.Backend.Worker
{
	public partial class Program;
}