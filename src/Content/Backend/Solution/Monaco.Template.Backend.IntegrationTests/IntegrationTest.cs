using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
#if (apiService)
using Refit;
#endif
#if (apiService && auth)
using System.Net.Http.Headers;
using Monaco.Template.Backend.IntegrationTests.Auth;
#endif

namespace Monaco.Template.Backend.IntegrationTests;

[ExcludeFromCodeCoverage]
public abstract class IntegrationTest : IAsyncLifetime
{
	protected readonly AppFixture Fixture;
#if (apiService)
	private HttpClient? _httpClient;
#endif
#if (apiService && auth)
	protected KeycloakService? KeycloakService;
	protected AccessTokenDto? AccessToken;

	protected abstract bool RequiresAuthentication { get; }
#endif

	protected IntegrationTest(AppFixture fixture)
	{
		Fixture = fixture;

#if (apiService && auth)
		if (RequiresAuthentication)
			KeycloakService = new KeycloakService(Fixture.KeycloakContainer.GetBaseAddress(),
												  AppFixture.KeycloakRealm,
												  AppFixture.KeycloakRealmUsername,
												  AppFixture.KeycloakRealmPassword);
#endif
	}

#if (apiService)
	protected T GetApi<T>(WebApplicationFactory<Api.Program> factory)
	{
		_httpClient ??= CreateHttpClient(factory);
		return RestService.For<T>(_httpClient);
	}

	private HttpClient CreateHttpClient(WebApplicationFactory<Api.Program> factory) =>
#if (auth)
		factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri,
									new BearerTokenHandler(() => AccessToken));
#else
		factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri);
#endif
#if (auth)

	private sealed class BearerTokenHandler(Func<AccessTokenDto?> tokenAccessor) : DelegatingHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
															   CancellationToken cancellationToken)
		{
			var token = tokenAccessor();
			if (token is not null)
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
			return base.SendAsync(request, cancellationToken);
		}
	}
#endif
#endif

	public virtual Task InitializeAsync() =>
		Task.CompletedTask;

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

	protected virtual async Task RunScriptAsync(string filePath) =>
		await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
					 .Database
					 .ExecuteSqlRawAsync(await File.ReadAllTextAsync(filePath));

	public virtual async Task DisposeAsync()
	{
		await Fixture.ResetDatabaseDataAsync();
#if (apiService)
		_httpClient?.Dispose();
		_httpClient = null;
#endif
	}
}