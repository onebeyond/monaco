﻿using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Country;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Application.Persistence;
using Xunit;
using Monaco.Template.Backend.Application.Features.Country.DTOs;

namespace Monaco.Template.Backend.Application.Tests.Features.Country;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Country", "Get Country List")]
public class GetCountryListTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	[Theory(DisplayName = "Get country list without params succeeds")]
	[AutoDomainData]
	public async Task GetCountryListWithoutParamsSucceeds(List<Domain.Model.Entities.Country> countries)
	{
		_dbContextMock.CreateAndSetupDbSetMock(countries);

		var query = new GetCountryList.Query(new List<KeyValuePair<string, StringValues>>());

		var sut = new GetCountryList.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .HaveCount(countries.Count).And
			  .Contain(x => countries.Any(c => c.Name == x.Name)).And
			  .BeInAscendingOrder(x => x.Name);
	}

	[Theory(DisplayName = "Get country list with params succeeds")]
	[AutoDomainData]
	public async Task GetCountryListWithParamsSucceeds(List<Domain.Model.Entities.Country> countries)
	{
		_dbContextMock.CreateAndSetupDbSetMock(countries);
		var countriesSet = countries.GetRange(0, 2);

		var queryString = new List<KeyValuePair<string, StringValues>>
		{
			new(nameof(CountryDto.Name),
				new(countriesSet.Select(x => x.Name).ToArray())),
			new("sort", $"-{nameof(CountryDto.Name)}")
		};

		var query = new GetCountryList.Query(queryString);

		var sut = new GetCountryList.Handler(_dbContextMock.Object);

		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .HaveCount(countriesSet.Count).And
			  .Contain(x => countriesSet.Any(c => c.Name == x.Name)).And
			  .BeInDescendingOrder(x => x.Name);
	}
}
