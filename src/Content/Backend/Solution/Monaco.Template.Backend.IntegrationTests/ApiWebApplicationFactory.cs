using Flurl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Monaco.Template.Backend.IntegrationTests;

public class ApiWebApplicationFactory : WebApplicationFactory<Api.Program>
{
	public readonly string SqlConnectionString;
#if (massTransitIntegration)
	public readonly Url RabbitMqHost;
	public readonly int RabbitMqPort;
	public readonly string RabbitMqUsername;
	public readonly string RabbitMqPassword;
#endif
#if (filesSupport)
	public readonly Url StorageConnectionString;
#endif
#if (auth)
	public readonly Url KeycloakRealmUrl;
#endif

	public ApiWebApplicationFactory(AppFixture fixture)
	{
		SqlConnectionString = fixture.SqlContainer.GetConnectionString();
#if (massTransitIntegration)
		Url rabbitMqConnString = fixture.RabbitMqContainer.GetConnectionString();
		RabbitMqUsername = rabbitMqConnString.UserInfo.Split(':').First();
		RabbitMqPassword = rabbitMqConnString.UserInfo.Split(':').Last();
		RabbitMqHost = rabbitMqConnString.Host;
		RabbitMqPort = rabbitMqConnString.Port!.Value;
#endif
#if (filesSupport)
		StorageConnectionString = fixture.AzuriteContainer.GetConnectionString();
#endif
#if (auth)
		KeycloakRealmUrl = fixture.KeycloakContainer
								  .GetBaseAddress()
								  .AppendPathSegments("realms", AppFixture.KeycloakRealm);
#endif
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseConfiguration(new ConfigurationManager
								 {
									 ["ConnectionStrings:AppDbContext"] = SqlConnectionString,
									 #if (auth)
									 ["SSO:Authority"] = KeycloakRealmUrl,
									 #endif
									 #if (filesSupport)
									 ["BlobStorage:ConnectionString"] = StorageConnectionString,
									 #endif
									 #if (massTransitIntegration)
									 ["MessageBus:RabbitMQ:Host"] = RabbitMqHost, 
									 ["MessageBus:RabbitMQ:Port"] = RabbitMqPort.ToString(),
									 ["MessageBus:RabbitMQ:Username"] = RabbitMqUsername,
									 ["MessageBus:RabbitMQ:Password"] = RabbitMqPassword
									 #endif
								 })
			   .UseSetting("https_port", "8080");
	}
}
