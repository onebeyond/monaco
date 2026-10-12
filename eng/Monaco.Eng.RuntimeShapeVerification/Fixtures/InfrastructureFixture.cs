using System.Diagnostics.CodeAnalysis;
using Azure.Storage.Blobs;
using Flurl;
using Testcontainers.Azurite;
using Testcontainers.Keycloak;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification.Fixtures;

[ExcludeFromCodeCoverage]
public sealed class InfrastructureFixture : IAsyncLifetime
{
	public const string RabbitMqVHost = "monaco";
	public const string StorageContainer = "files-store";
	public const string KeycloakRealm = "monaco-template";
	public const string KeycloakRealmUsername = "admin@admin.com";
	public const string KeycloakRealmPassword = "admin";

	private bool _dockerAvailable = true;
	private string? _dockerFailure;

	public MsSqlContainer SqlContainer { get; } = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();

	public RabbitMqContainer RabbitMqContainer { get; } =
		new RabbitMqBuilder("rabbitmq:3.11").WithEnvironment("RABBITMQ_DEFAULT_VHOST", RabbitMqVHost)
											.Build();

	public AzuriteContainer AzuriteContainer { get; } =
		new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.28.0").WithCommand("--skipApiVersionCheck")
																			.Build();

	public KeycloakContainer? KeycloakContainer { get; private set; }

	public string SqlConnectionString =>
		SqlContainer.GetConnectionString();

	public string CreateIsolatedConnectionString(string token)
	{
		var catalog = $"rsv_{token}_{Guid.NewGuid():N}";
		var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(SqlConnectionString) { InitialCatalog = catalog };
		return builder.ConnectionString;
	}

	public Url RabbitMqConnectionString =>
		RabbitMqContainer.GetConnectionString();

	public string RabbitMqHost =>
		RabbitMqConnectionString.Host;

	public int RabbitMqPort =>
		RabbitMqConnectionString.Port!.Value;

	public string RabbitMqUsername =>
		RabbitMqConnectionString.UserInfo.Split(':').First();

	public string RabbitMqPassword =>
		RabbitMqConnectionString.UserInfo.Split(':').Last();

	public string StorageConnectionString =>
		AzuriteContainer.GetConnectionString();

	public Url KeycloakRealmUrl =>
		RunningKeycloak().GetBaseAddress().AppendPathSegments("realms", KeycloakRealm);

	public string KeycloakBaseAddress =>
		RunningKeycloak().GetBaseAddress();

	public async Task InitializeAsync()
	{
		try
		{
			await SqlContainer.StartAsync();
			await RabbitMqContainer.StartAsync();
			await AzuriteContainer.StartAsync();
			await new BlobContainerClient(StorageConnectionString, StorageContainer).CreateAsync();

			var realmExport = FindRealmExport();
			KeycloakContainer = new KeycloakBuilder("quay.io/keycloak/keycloak:25.0.6")
								.WithResourceMapping(new(realmExport),
													 new FileInfo("/opt/keycloak/data/import/realm-export-template.json"))
								.WithCommand("--import-realm")
								.Build();
			await KeycloakContainer.StartAsync();
		}
		catch (Exception ex)
		{
			_dockerAvailable = false;
			_dockerFailure = ex.Message;
		}
	}

	public void EnsureDocker()
	{
		if (_dockerAvailable)
			return;

		Assert.Fail($"Docker infrastructure is unavailable; a skip is not a pass. {_dockerFailure}");
	}

	public async Task DisposeAsync()
	{
		if (!_dockerAvailable)
			return;

		await SqlContainer.DisposeAsync();
		await RabbitMqContainer.DisposeAsync();
		await AzuriteContainer.DisposeAsync();
		if (KeycloakContainer is not null)
			await KeycloakContainer.DisposeAsync();
	}

	private KeycloakContainer RunningKeycloak() =>
		KeycloakContainer ?? throw new InvalidOperationException("Keycloak has not been started.");

	private static string FindRealmExport()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName,
										 "src",
										 "Content",
										 "Backend",
										 "Solution",
										 "Monaco.Template.Backend.IntegrationTests",
										 "Imports",
										 "Keycloak",
										 "realm-export-template.json");
			if (File.Exists(candidate))
				return candidate;

			var nested = Path.Combine(directory.FullName,
									  "Imports",
									  "Keycloak",
									  "realm-export-template.json");
			if (File.Exists(nested))
				return nested;
		}

		throw new FileNotFoundException("Could not locate realm-export-template.json for Keycloak.");
	}
}