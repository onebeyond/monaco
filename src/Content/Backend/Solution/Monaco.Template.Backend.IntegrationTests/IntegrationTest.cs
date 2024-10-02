using Flurl.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using System.Diagnostics.CodeAnalysis;

namespace Monaco.Template.Backend.IntegrationTests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
public abstract class IntegrationTest : IClassFixture<AppFixture>, IAsyncLifetime
{
	protected readonly AppFixture Fixture;
	protected ApiWebApplicationFactory WebAppFactory;
	protected IFlurlClient Client;
	protected readonly bool RequiresAuthentication;
	protected KeycloakService KeycloakService;
	protected AccessTokenDto? AccessToken;
	
	protected IntegrationTest(AppFixture fixture, bool requiresAuthentication)
	{
		Fixture = fixture;
		WebAppFactory = new ApiWebApplicationFactory(Fixture);

		var clientOptions = new WebApplicationFactoryClientOptions
							{
								AllowAutoRedirect = false
							};

		Client = new FlurlClient(WebAppFactory.CreateClient(clientOptions)).AllowAnyHttpStatus()
																		   .BeforeCall(call =>
																					   {
																						   if (AccessToken is not null)
																							   call.Request.WithOAuthBearerToken(AccessToken.AccessToken);
																					   });
		RequiresAuthentication = requiresAuthentication;
		if (RequiresAuthentication)
			KeycloakService = new KeycloakService(Fixture.KeycloakContainer.GetBaseAddress(),
												  AppFixture.KeycloakRealm,
												  AppFixture.KeycloakRealmUsername,
												  AppFixture.KeycloakRealmPassword);
	}

	protected IFlurlRequest CreateRequest(string endpoint) => Client.Request(endpoint);

	public virtual async Task InitializeAsync()
	{
		await ApplyDbMigrations();
	}

	protected virtual async Task SetupAccessToken(string audienceClientId,
												  string[] roles,
												  string[] scopes) 
	{
		if (!RequiresAuthentication)
			return;

		var client = await KeycloakService.CreateTestClient(audienceClientId, roles, scopes);
		AccessToken = await KeycloakService.GetAccessToken(client);
	}

	protected virtual Task SetupAccessToken(string[] roles) =>
		SetupAccessToken(Auth.AudienceClientId,
						 roles,
						 Auth.Scopes);

	protected virtual AppDbContext GetDbContext() =>
		WebAppFactory.Services
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

	public virtual async Task DisposeAsync()
	{
		await RollbackDbMigrations();
		await WebAppFactory.DisposeAsync();
	}
}