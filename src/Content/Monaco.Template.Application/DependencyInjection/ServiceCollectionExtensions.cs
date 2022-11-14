using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
#if includeMassTransitSupport
using MassTransit;
#endif
using Monaco.Template.Application.Infrastructure.Context;
#if includeFilesSupport
using Monaco.Template.Application.Services;
using Monaco.Template.Application.Services.Contracts;
#endif
using Monaco.Template.Common.Application.Commands.Behaviors;
using Monaco.Template.Common.Application.Validators.Contracts;
using System.Reflection;
using Monaco.Template.Common.Application.Policies;
#if includeFilesSupport
using Monaco.Template.Common.BlobStorage.Extensions;
#endif

namespace Monaco.Template.Application.DependencyInjection;

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
		services.AddPolicies<Policies.Policies>()
				.AddMediatR(GetApplicationAssembly())
				.RegisterCommandConcurrencyExceptionBehavior()
				.RegisterCommandValidationBehaviors(GetApplicationAssembly())
				.AddValidatorsFromAssembly(GetApplicationAssembly(), filter: filter => !filter.ValidatorType
																							  .GetInterfaces()
																							  .Contains(typeof(INonInjectable)) &&
																					   !filter.ValidatorType.IsAbstract)
				.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(optionsValue.EntityFramework.ConnectionString,
																	  sqlOptions =>
																	  {
																		  sqlOptions.MigrationsAssembly("Monaco.Template.Application.Infrastructure.Migrations");
																		  sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null);
																	  })
														.UseLazyLoadingProxies()
														.EnableSensitiveDataLogging(optionsValue.EntityFramework.EnableEfSensitiveLogging));
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