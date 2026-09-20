using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Monaco.Template.Backend.Application.DependencyInjection;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Infrastructure.Persistence;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.DependencyInjection;

[ExcludeFromCodeCoverage]
[Trait("Application Dependency Injection", "Auditable Save Changes Interceptor")]
public sealed class AuditableSaveChangesInterceptorRegistrationTests
{
	[Fact(DisplayName = "ConfigureApplication registers the auditable interceptor on AppDbContext options")]
	public void ConfigureApplicationRegistersAuditableInterceptorOnAppDbContextOptions()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ILogger<PersistenceActorFallbackDiagnostics>>(NullLogger<PersistenceActorFallbackDiagnostics>.Instance);
		services.AddScoped<IPersistenceActorProvider, TestPersistenceActorProvider>();
		services.ConfigureApplication(options =>
									  {
										  options.EntityFramework.ConnectionString = "Server=.;Database=stamp-wiring;Trusted_Connection=True;TrustServerCertificate=True;";
#if (filesSupport)
										  options.BlobStorage.ConnectionString = "UseDevelopmentStorage=true";
										  options.BlobStorage.ContainerName = "stamp-wiring";
#endif
									  });

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
		var interceptors = options.FindExtension<CoreOptionsExtension>()
								  ?.Interceptors;

		scope.ServiceProvider
			 .GetService<AuditableSaveChangesInterceptor>()
			 .Should()
			 .NotBeNull();
		interceptors.Should()
					.NotBeNull()
					.And
					.ContainSingle(interceptor => interceptor is AuditableSaveChangesInterceptor);
	}

	private sealed class TestPersistenceActorProvider : IPersistenceActorProvider
	{
		public string HostRole =>
			PersistenceActor.ApiHostRole;

		public string GetActor() =>
			"subject:wiring-test";
	}
}