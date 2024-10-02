using Flurl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Monaco.Template.Backend.IntegrationTests;

public class ApiWebApplicationFactory : WebApplicationFactory<Api.Program>
{
	public readonly string SqlConnectionString;
	public readonly Url RabbitMqHost;
	public readonly string RabbitMqUsername;
	public readonly string RabbitMqPassword;
	public readonly Url StorageConnectionString;
	public readonly Url KeycloakRealmUrl;

	public ApiWebApplicationFactory(AppFixture fixture)
	{
		SqlConnectionString = fixture.SqlContainer.GetConnectionString();
		Url rabbitMqConnString = fixture.RabbitMqContainer.GetConnectionString();
		RabbitMqUsername = rabbitMqConnString.UserInfo.Split(':').First();
		RabbitMqPassword = rabbitMqConnString.UserInfo.Split(':').Last();
		RabbitMqHost = rabbitMqConnString.Root.Replace($"{rabbitMqConnString.UserInfo}@", string.Empty);
		StorageConnectionString = fixture.AzuriteContainer.GetConnectionString();
		KeycloakRealmUrl = fixture.KeycloakContainer
								  .GetBaseAddress()
								  .AppendPathSegments("realms", AppFixture.KeycloakRealm);
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseConfiguration(new ConfigurationManager
								 {
									 ["ConnectionStrings:AppDbContext"] = SqlConnectionString,
									 ["SSO:Authority"] = KeycloakRealmUrl,
									 ["BlobStorage:ConnectionString"] = StorageConnectionString,
									 ["MessageBus:RabbitMQ:Host"] = RabbitMqHost,
									 ["MessageBus:RabbitMQ:User"] = RabbitMqUsername,
									 ["MessageBus:RabbitMQ:Password"] = RabbitMqPassword
								 })
			   .UseSetting("https_port", "8080");
	}
}
