using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Backend.Application.DependencyInjection;

var host = new HostBuilder()
		   .ConfigureServices((context, services) =>
							  {
								  var configuration = context.Configuration;

								  services.ConfigureApplication(options =>
																{
																	options.EntityFramework.ConnectionString = configuration.GetConnectionString("AppDbContext")!;
																	options.EntityFramework.EnableEfSensitiveLogging = bool.Parse(configuration["EnableEFSensitiveLogging"] ?? "false");
#if (filesSupport)
																	options.BlobStorage.ConnectionString = configuration["BlobStorage:ConnectionString"]!;
																	options.BlobStorage.ContainerName = configuration["BlobStorage:Container"]!;
#endif
																});

								  services.AddApplicationInsightsTelemetryWorkerService();
								  services.ConfigureFunctionsApplicationInsights();
							  })
		   .Build();

host.Run();