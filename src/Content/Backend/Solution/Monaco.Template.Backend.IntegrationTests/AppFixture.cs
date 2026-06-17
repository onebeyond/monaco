#if (filesSupport)
using Azure.Storage.Blobs;
using Testcontainers.Azurite;
#endif
#if (auth)
using Testcontainers.Keycloak;
#endif
using System.Diagnostics.CodeAnalysis;
using Flurl;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Domain.Model.Entities;
using Respawn;
using Testcontainers.MsSql;
#if (massTransitIntegration)
using Testcontainers.RabbitMq;
#endif
using Monaco.Template.Backend.IntegrationTests.Factories;
using Monaco.Template.Backend.Application.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Monaco.Template.Backend.IntegrationTests;

[ExcludeFromCodeCoverage]
public class AppFixture : IAsyncLifetime
{
#if (massTransitIntegration)
	public const string RabbitMqVHost = "monaco";
#endif
#if (filesSupport)
	public const string StorageContainer = "files-store";
#endif
#if (auth)
	public const string KeycloakRealm = "monaco-template";
	public const string KeycloakRealmUsername = "admin@admin.com";
	public const string KeycloakRealmPassword = "admin";
#endif

	public MsSqlContainer SqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
#if (massTransitIntegration)
	public RabbitMqContainer RabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.11").WithEnvironment("RABBITMQ_DEFAULT_VHOST", RabbitMqVHost)
																					 .Build();
#endif
#if (filesSupport)
	public AzuriteContainer AzuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.28.0").WithCommand("--skipApiVersionCheck")
																												   .Build();
#endif
#if (auth)
	public KeycloakContainer KeycloakContainer = new KeycloakBuilder("quay.io/keycloak/keycloak:25.0.6").WithResourceMapping(new FileInfo("./Imports/Keycloak/realm-export-template.json"),
																															 new FileInfo("/opt/keycloak/data/import/realm-export-template.json"))
																										.WithCommand("--import-realm")
																										.Build();
#endif

#if (apiService)
	public ApiWebApplicationFactory WebAppFactory = null!;

#endif
#if (workerService)
	public WorkerServiceFactory WorkerServiceFactory = null!;

#endif
	private Respawner? _respawner;

	public async Task InitializeAsync()
	{
		await SqlContainer.StartAsync();
#if (auth)
		await KeycloakContainer.StartAsync();
#endif
#if (massTransitIntegration)
		await RabbitMqContainer.StartAsync();
#endif
#if (filesSupport)
		await AzuriteContainer.StartAsync();

		await InitStorage();
#endif

#if (apiService)
		WebAppFactory = new ApiWebApplicationFactory(this);
#endif
#if (workerService)
		WorkerServiceFactory = new WorkerServiceFactory(this);
#endif

		await ApplyDbMigrationsAsync();
	}

	public virtual AppDbContext GetDbContext(IServiceProvider services) =>
		services.CreateScope()
				.ServiceProvider
				.GetRequiredService<AppDbContext>();

	protected virtual async Task ApplyDbMigrationsAsync(string? targetMigration = null) =>
#if (apiService)
		await GetDbContext(WebAppFactory.Services)
#elif (workerService)
		await GetDbContext(WorkerServiceInstance.Services)
#endif
			.GetService<IMigrator>()
			.MigrateAsync(targetMigration);

	public string SqlConnectionString =>
		SqlContainer.GetConnectionString();
#if (massTransitIntegration)

	public Url RabbitMqConnectionString =>
		RabbitMqContainer.GetConnectionString();

	public string RabbitMqHost =>
		RabbitMqConnectionString.Host;

	public int RabbitMqPort =>
		RabbitMqConnectionString.Port!.Value;

	public string RabbitMqUsername =>
		RabbitMqConnectionString.UserInfo
								.Split(':')
								.First();

	public string RabbitMqPassword =>
		RabbitMqConnectionString.UserInfo
								.Split(':')
								.Last();
#endif
#if (filesSupport)

	public Url StorageConnectionString =>
		AzuriteContainer.GetConnectionString();
#endif
#if (auth)

	public Url KeycloakRealmUrl =>
		KeycloakContainer.GetBaseAddress()
						 .AppendPathSegments("realms", KeycloakRealm);
#endif

		public async Task DisposeAsync()
		{
#if (apiService)
			await WebAppFactory.DisposeAsync();

#endif
#if (workerService)
			await WorkerServiceFactory.DisposeAsync();

#endif
			await SqlContainer.StopAsync();
#if (massTransitIntegration)
			await RabbitMqContainer.StopAsync();
#endif
#if (filesSupport)
			await AzuriteContainer.StopAsync();
#endif
#if (auth)
			await KeycloakContainer.StopAsync();
#endif

			await SqlContainer.DisposeAsync();
#if (massTransitIntegration)
			await RabbitMqContainer.DisposeAsync();
#endif
#if (filesSupport)
			await AzuriteContainer.DisposeAsync();
#endif
#if (auth)
			await KeycloakContainer.DisposeAsync();
#endif
		}
#if (filesSupport)

	private async Task InitStorage()
	{
		await new BlobContainerClient(AzuriteContainer.GetConnectionString(), StorageContainer)
			.CreateAsync();
	}
#endif

	/// <summary>
	/// Resets database data using Respawn. This is much faster than rolling back migrations.
	/// Respawner is lazily initialized on first call.
	/// </summary>
	public async Task ResetDatabaseDataAsync()
	{
#if (apiService)
		var services = WebAppFactory.Services;
#elif (workerService)
		var services = WorkerServiceInstance.Services;
#endif
		var connection = GetDbContext(services).Database
											   .GetDbConnection();

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync();

		_respawner ??= await Respawner.CreateAsync(connection,
												   new RespawnerOptions
												   {
													   DbAdapter = DbAdapter.SqlServer,
													   TablesToIgnore =
													   [
														   "__EFMigrationsHistory",
														   nameof(Country)
													   ],
													   SchemasToInclude = ["dbo"]
												   });

		await _respawner.ResetAsync(connection);
	}
}