#if (massTransitIntegration)
using MassTransit;
using Monaco.Template.Backend.Application.Persistence;
#endif
using Monaco.Template.Backend.Application.DependencyInjection;
using Monaco.Template.Backend.Common.Observability;
using Monaco.Template.Backend.Worker;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
builder.Services
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
						   cfg.AddEntityFrameworkOutbox<AppDbContext>(o =>
																	  {
																		  o.UseSqlServer();
																		  o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
																	  });
						   
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
						   
						   cfg.AddConfigureEndpointsCallback((context, _, config) => config.UseEntityFrameworkOutbox<AppDbContext>(context));
					   })
#endif
	   .AddHostedService<Worker>();

builder.AddWorkerObservability();

var host = builder.Build();
host.Run();

namespace Monaco.Template.Backend.Worker
{
	public partial class Program;
}