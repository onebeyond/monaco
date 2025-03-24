#if (filesSupport)
using Azure.Storage.Blobs;
using Testcontainers.Azurite;
#endif
#if (auth)
using Testcontainers.Keycloak;
#endif
using Flurl;
using Testcontainers.MsSql;
#if (massTransitIntegration)
using Testcontainers.RabbitMq;
#endif

namespace Monaco.Template.Backend.IntegrationTests;

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

	public MsSqlContainer SqlContainer = new MsSqlBuilder().Build();
#if (massTransitIntegration)
	public RabbitMqContainer RabbitMqContainer = new RabbitMqBuilder().WithEnvironment("RABBITMQ_DEFAULT_VHOST", RabbitMqVHost)
																	  .Build();
#endif
#if (filesSupport)
	public AzuriteContainer AzuriteContainer = new AzuriteBuilder().WithCommand("--skipApiVersionCheck")
																   .Build();
#endif
#if (auth)
	public KeycloakContainer KeycloakContainer = new KeycloakBuilder().WithImage("quay.io/keycloak/keycloak:25.0.6")
																	  .WithResourceMapping(new FileInfo("./Imports/Keycloak/realm-export-template.json"),
																						   new FileInfo("/opt/keycloak/data/import/realm-export-template.json"))
																	  .WithCommand("--import-realm")
																	  .Build();
#endif

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
	}

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
}