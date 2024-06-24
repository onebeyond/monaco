using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Backend.Application.Infrastructure.Context;
#if (filesSupport)
using Monaco.Template.Backend.Application.Services;
using Monaco.Template.Backend.Application.Services.Contracts;
#endif
using Monaco.Template.Backend.Common.Application.Commands.Behaviors;
using Monaco.Template.Backend.Common.Application.Validators.Contracts;
using System.Reflection;
using Monaco.Template.Backend.Common.Application.Policies;
using Monaco.Template.Backend.Common.Infrastructure.Context;
#if (filesSupport)
using Monaco.Template.Backend.Common.BlobStorage.Extensions;
#endif
using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Application.DependencyInjection;

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
				.AddMediatR(config => config.RegisterServicesFromAssemblies(GetApplicationAssembly()))
				.RegisterCommandConcurrencyExceptionBehaviors(GetApplicationAssembly())
				.RegisterCommandValidationBehaviors(GetApplicationAssembly())
				.AddValidatorsFromAssembly(GetApplicationAssembly(), filter: filter => !filter.ValidatorType
																							  .GetInterfaces()
																							  .Contains(typeof(INonInjectable)) &&
																					   !filter.ValidatorType.IsAbstract)
				.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(optionsValue.EntityFramework.ConnectionString,
																	  sqlOptions =>
																	  {
																		  sqlOptions.MigrationsAssembly("Monaco.Template.Backend.Application.Infrastructure.Migrations");
																		  sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null);
																	  })
														.UseLazyLoadingProxies()
														.EnableSensitiveDataLogging(optionsValue.EntityFramework.EnableEfSensitiveLogging))
				.AddScoped<BaseDbContext, AppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
		#if (filesSupport)
		services.RegisterBlobStorageService(opts =>
											{
												opts.ConnectionString = optionsValue.BlobStorage.ConnectionString;
												opts.ContainerName = optionsValue.BlobStorage.ContainerName;
											})
				.AddScoped<IFileService, FileService>();
#endif

		services.AddMemoryCache();
		services.AddSingleton<ICachedQueryService, CachedQueryService>();

		return services;
	}

	private static Assembly GetApplicationAssembly() => Assembly.GetAssembly(typeof(ServiceCollectionExtensions))!;
}