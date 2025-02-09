using Flurl.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
#if (workerService)
using Microsoft.Extensions.Hosting;
#endif
using Monaco.Template.Backend.Application.Infrastructure.Context;
using System.Diagnostics.CodeAnalysis;
using MassTransit.Testing;
using Monaco.Template.Backend.IntegrationTests.Factories;
#if (apiService && auth)
using Monaco.Template.Backend.IntegrationTests.Auth;
#endif

namespace Monaco.Template.Backend.IntegrationTests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
public abstract class IntegrationTest : IClassFixture<AppFixture>, IAsyncLifetime
{
	protected readonly AppFixture Fixture;
#if (apiService)
	protected ApiWebApplicationFactory WebAppFactory;
	protected IFlurlClient Client;
#endif
#if (workerService)
	protected WorkerServiceFactory WorkerServiceFactory;
	protected IHost WorkerServiceInstance;
#endif
#if (apiService && auth)
	protected KeycloakService? KeycloakService;
	protected AccessTokenDto? AccessToken;

	protected abstract bool RequiresAuthentication { get; }
#endif

	protected IntegrationTest(AppFixture fixture)
	{
		Fixture = fixture;
#if (apiService)
		WebAppFactory = new ApiWebApplicationFactory(Fixture);
#endif
#if (workerService)
		WorkerServiceFactory = new WorkerServiceFactory(Fixture);
#endif

#if (apiService)
		var clientOptions = new WebApplicationFactoryClientOptions
							{
								AllowAutoRedirect = false
							};

		Client = new FlurlClient(WebAppFactory.CreateClient(clientOptions))
#if (auth)
				 .AllowAnyHttpStatus()
				 .BeforeCall(call =>
							 {
								 if (AccessToken is not null)
									 call.Request.WithOAuthBearerToken(AccessToken.AccessToken);
							 });

		if (RequiresAuthentication)
			KeycloakService = new KeycloakService(Fixture.KeycloakContainer.GetBaseAddress(),
												  AppFixture.KeycloakRealm,
												  AppFixture.KeycloakRealmUsername,
												  AppFixture.KeycloakRealmPassword);
#else
				 .AllowAnyHttpStatus();

#endif
#endif
#if (workerService)
		WorkerServiceInstance = WorkerServiceFactory.GetHostInstance();
#endif
	}

#if (apiService)
	protected IFlurlRequest CreateRequest(string endpoint) => Client.Request(endpoint);

#endif
	public virtual async Task InitializeAsync()
	{
		await ApplyDbMigrations();
	}

#if (apiService && auth)
	protected virtual async Task SetupAccessToken(string audienceClientId,
												  string[] roles,
												  string[] scopes) 
	{
		if (!RequiresAuthentication)
			return;

		var client = await KeycloakService!.CreateTestClient(audienceClientId, roles, scopes);
		AccessToken = await KeycloakService.GetAccessToken(client);
	}

	protected virtual Task SetupAccessToken(string[] roles) =>
		SetupAccessToken(Auth.Auth.AudienceClientId,
						 roles,
						 Auth.Auth.Scopes);

#endif
	protected virtual AppDbContext GetDbContext() =>
#if (apiService)
		WebAppFactory.Services
#elif (workerService)
		WorkerServiceInstance.Services
#endif
					 .CreateScope()
					 .ServiceProvider
					 .GetRequiredService<AppDbContext>();

	protected virtual async Task ApplyDbMigrations(string? targetMigration = null) =>
		await GetDbContext().GetService<IMigrator>()
							.MigrateAsync(targetMigration);

	protected virtual async Task RollbackDbMigrations() =>
		await ApplyDbMigrations("0");

	protected virtual async Task RunScriptAsync(string filePath)
	{
		var script = await File.ReadAllTextAsync(filePath);
		await GetDbContext().Database
							.ExecuteSqlRawAsync(script);
	}

#if (apiService)
	protected virtual ITestHarness GetApiTestHarness() =>
		WebAppFactory.Services
					 .GetTestHarness();

#endif
#if (workerService)
	protected virtual ITestHarness GetServiceTestHarness() =>
		WorkerServiceInstance.Services
							 .GetTestHarness();

#endif
	public virtual async Task DisposeAsync()
	{
		await RollbackDbMigrations();
#if (apiService)
		await WebAppFactory.DisposeAsync();
#endif
#if (workerService)

		await WorkerServiceInstance.StopAsync();
		WorkerServiceInstance.Dispose();
#endif
	}
}