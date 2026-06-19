using AwesomeAssertions;
using Monaco.Template.Backend.Application.Features.Country;
using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories.Entities;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Country;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Country", "Get Country by Id")]
public class GetCountryByIdTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	[Fact(DisplayName = "Get existing country by Id succeeds")]
	public async Task GetExistingCountryByIdSucceeds()
	{
		var countries = CountryFactory.CreateMany().ToList();
		_dbContextMock.CreateAndSetupDbSetMock(countries);
		var country = countries[0];

		var query = new GetCountryById.Query(country.Id);

		var sut = new GetCountryById.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, CancellationToken.None);

		var success = result.Should()
							.BeOfType<Success<CountryDto>>();
		
		success.Subject
			   .Result
			   .Name
			   .Should()
			   .Be(country.Name);
	}

	[Fact(DisplayName = "Get non-existing country by Id fails")]
	public async Task GetNonExistingCountryByIdFails()
	{
		_dbContextMock.CreateAndSetupDbSetMock(CountryFactory.CreateMany().ToList());
		var query = new GetCountryById.Query(Guid.NewGuid());

		var sut = new GetCountryById.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, CancellationToken.None);

		result.Should()
			  .BeOfType<NotFound<CountryDto>>();
	}
}