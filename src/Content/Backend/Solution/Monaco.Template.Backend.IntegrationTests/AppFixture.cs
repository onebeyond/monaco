using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using Testcontainers.Keycloak;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Monaco.Template.Backend.IntegrationTests;

public class AppFixture : IAsyncLifetime
{
	public const string RabbitMqVHost = "monaco";
	public const string StorageContainer = "files-store";
	public const string KeycloakRealm = "monaco-template";
	public const string KeycloakRealmUsername = "admin@admin.com";
	public const string KeycloakRealmPassword = "admin";

	public MsSqlContainer SqlContainer = new MsSqlBuilder().Build();
	public RabbitMqContainer RabbitMqContainer = new RabbitMqBuilder().WithEnvironment("RABBITMQ_DEFAULT_VHOST", RabbitMqVHost)
																	  .Build();
	public AzuriteContainer AzuriteContainer = new AzuriteBuilder().WithCommand("--skipApiVersionCheck")
																   .Build();
	public KeycloakContainer KeycloakContainer = new KeycloakBuilder().WithImage("quay.io/keycloak/keycloak:25.0.6")
																	  .WithResourceMapping(new FileInfo("./Imports/Keycloak/realm-export-template.json"),
																						   new FileInfo("/opt/keycloak/data/import/realm-export-template.json"))
																	  .WithCommand("--import-realm")
																	  .Build();
	
	public async Task InitializeAsync()
	{
		await KeycloakContainer.StartAsync();
		await SqlContainer.StartAsync();
		await RabbitMqContainer.StartAsync();
		await AzuriteContainer.StartAsync();

		await InitStorage();
	}
	
	public async Task DisposeAsync()
	{
		await SqlContainer.StopAsync();
		await RabbitMqContainer.StopAsync();
		await AzuriteContainer.StopAsync();
		await KeycloakContainer.StopAsync();

		await SqlContainer.DisposeAsync();
		await RabbitMqContainer.DisposeAsync();
		await AzuriteContainer.DisposeAsync();
		await KeycloakContainer.DisposeAsync();
	}

	private async Task InitStorage()
	{
		await new BlobContainerClient(AzuriteContainer.GetConnectionString(), StorageContainer)
			.CreateAsync();
	}
}