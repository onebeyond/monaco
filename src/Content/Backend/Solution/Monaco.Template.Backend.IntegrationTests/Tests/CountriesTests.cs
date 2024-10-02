using FluentAssertions;
using Flurl.Http;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Countries")]
public class CountriesTests : IntegrationTest
{
	public CountriesTests(AppFixture fixture) : base(fixture, true)
	{ }

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		await SetupAccessToken([]);
	}

	[Fact(DisplayName = "Get Countries succeeds")]
	public async Task GetCountriesSucceeds()
	{
		var response = await CreateRequest(ApiRoutes.Countries.Query()).GetAsync();
		
		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var result = await response.GetJsonAsync<CountryDto[]>();
		var countriesCount = await GetDbContext().Set<Country>()
												 .CountAsync();

		result.Should()
			  .NotBeNull();
		result.Should()
			  .HaveCount(countriesCount);
	}

	[Fact(DisplayName = "Get Country succeeds")]
	public async Task GetCountrySucceeds()
	{
		var countryId = Guid.Parse("534A826B-70EF-2128-1A4C-52E23B7D5447");

		var response = await CreateRequest(ApiRoutes.Countries.Get(countryId)).GetAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var result = await response.GetJsonAsync<CountryDto>();
		var country = await GetDbContext().Set<Country>()
										  .SingleAsync(c => c.Id == countryId);

		result.Should()
			  .NotBeNull();
		result.Name
			  .Should()
			  .Be(country.Name);
	}
}