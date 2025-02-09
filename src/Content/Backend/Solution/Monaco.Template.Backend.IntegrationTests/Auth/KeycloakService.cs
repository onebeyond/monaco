using Flurl;
using Flurl.Http;
using Keycloak.Net;
using Keycloak.Net.Models.Clients;

namespace Monaco.Template.Backend.IntegrationTests.Auth;

public class KeycloakService
{
	private readonly string _realm;
	private readonly KeycloakClient _kcClient;
	private readonly Url _tokenEndpoint;

	public KeycloakService(string baseUrl,
						   string realm,
						   string username,
						   string password)
	{
		_realm = realm;
		_tokenEndpoint = baseUrl.AppendPathSegments("realms",
													_realm,
													"protocol",
													"openid-connect",
													"token");
		_kcClient = new KeycloakClient(baseUrl, username, password);
	}

	public async Task<Client> CreateTestClient(string audienceClientId,
											   string[] roles,
											   string[] scopes)
	{
		var client = new Client
		{
			Name = "Test Client",
			ClientId = Guid.NewGuid().ToString(),
			Secret = Guid.NewGuid().ToString(),
			ServiceAccountsEnabled = true,
			StandardFlowEnabled = false,
			AuthorizationServicesEnabled = false,
			DefaultClientScopes = [audienceClientId, .. scopes]
		};
		await _kcClient.CreateClientAsync(_realm, client);
		client = (await GetClientByClientId(client.ClientId))!;

		var clientUser = await _kcClient.GetUserForServiceAccountAsync(_realm, client.Id);

		var audienceClient = (await GetClientByClientId(audienceClientId))!;
		var clientRoles = await _kcClient.GetRolesAsync(_realm, audienceClient.Id);

		await _kcClient.AddClientRoleMappingsToUserAsync(_realm,
														 clientUser.Id,
														 audienceClient.Id,
														 [.. clientRoles.Where(r => roles.Contains(r.Name))]);
		return client;
	}

	public async Task<Client?> GetClientByClientId(string clientId)
	{
		var results = await _kcClient.GetClientsAsync(_realm, clientId);
		return results.SingleOrDefault();
	}

	public async Task<AccessTokenDto> GetAccessToken(Client client) =>
		await _tokenEndpoint.PostUrlEncodedAsync(new
												 {
													 client_id = client.ClientId,
													 client_secret = client.Secret,
													 grant_type = "client_credentials"
												 })
							.ReceiveJson<AccessTokenDto>();
}