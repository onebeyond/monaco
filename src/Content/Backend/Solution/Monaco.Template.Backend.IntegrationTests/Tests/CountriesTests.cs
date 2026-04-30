using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.IntegrationTests.Apis;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Countries")]
public class CountriesTests : IntegrationTest
{
	public CountriesTests(AppFixture fixture) : base(fixture)
	{ }

#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
#if (auth)

		await SetupAccessToken([]);
#endif
	}

	[Fact(DisplayName = "Get Countries succeeds")]
	public async Task GetCountriesSucceeds()
	{
		var api = GetApi<ICountriesApi>(Fixture.WebAppFactory);
		var response = await api.Query();

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.OK);

		response.Content
				.Should()
				.NotBeNull();

		var countriesCount = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
										  .Set<Country>()
										  .CountAsync();

		response.Content
				.Should()
				.HaveCount(countriesCount);
	}

	[Fact(DisplayName = "Get Country succeeds")]
	public async Task GetCountrySucceeds()
	{
		var countryId = Guid.Parse("534A826B-70EF-2128-1A4C-52E23B7D5447");

		var api = GetApi<ICountriesApi>(Fixture.WebAppFactory);
		var response = await api.Get(countryId);

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.OK);

		var country = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								   .Set<Country>()
								   .SingleAsync(c => c.Id == countryId);

		response.Content
				.Should()
				.NotBeNull();
		response.Content!.Name
				.Should()
				.Be(country.Name);
	}
}