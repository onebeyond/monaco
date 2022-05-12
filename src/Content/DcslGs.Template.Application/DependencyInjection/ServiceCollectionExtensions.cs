using System.Reflection;
using DcslGs.Template.Application.Infrastructure.Context;
using DcslGs.Template.Common.Application.Commands.Behaviors;
using DcslGs.Template.Common.Application.Validators.Contracts;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
#if includeFilesSupport
using DcslGs.Template.Application.Services;
using DcslGs.Template.Application.Services.Contracts;
#endif
#if includeFilesSupport
using DcslGs.Template.Common.BlobStorage.Extensions;
#endif
#if includeMassTransitSupport
using MassTransit;
#endif

namespace DcslGs.Template.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers and configures all the services and dependencies of the Application 
	/// </summary>
	/// <param name="services"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static IServiceCollection ConfigureApplication(this IServiceCollection services,
														  Action<ApplicationOptions> options)
	{
		var optionsValue = new ApplicationOptions();
		options.Invoke(optionsValue);
		services.AddMediatR(GetApplicationAssembly())
				.RegisterPreCommandProcessorBehaviors(GetApplicationAssembly())
				.AddValidatorsFromAssembly(GetApplicationAssembly(), filter: filter => !filter.ValidatorType
																							  .GetInterfaces()
																							  .Contains(typeof(INonInjectable)) &&
																					   !filter.ValidatorType.IsAbstract)
				.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(optionsValue.EntityFramework.ConnectionString,
																	  sqlOptions =>
																	  {
																		  sqlOptions.MigrationsAssembly("DcslGs.Template.Application.Infrastructure.Migrations");
																		  sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null);
																	  })
														.UseLazyLoadingProxies()
														.EnableSensitiveDataLogging(optionsValue.EntityFramework.EnableEfSensitiveLogging));
#if includeMassTransitSupport
		services.AddMassTransit(x => x.UsingAzureServiceBus((_, cfg) => cfg.Host(optionsValue.MessageBus.AzureServiceBusConnectionString)));
#endif
#if includeFilesSupport
		services.RegisterBlobStorageService(opts =>
											{
												opts.ConnectionString = optionsValue.BlobStorage.ConnectionString;
												opts.ContainerName = optionsValue.BlobStorage.ContainerName;
											})
				.AddScoped<IFileService, FileService>();
#endif

		return services;
	}

	private static Assembly GetApplicationAssembly() => Assembly.GetAssembly(typeof(ServiceCollectionExtensions))!;
}