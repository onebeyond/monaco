using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Features.Country;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests.Factories.Entities;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Country;

[ExcludeFromCodeCoverage]
[Trait("Application Queries", "Country Queries")]
public class GetCountryByIdTests
{
	[Fact(DisplayName = "Get existing country by Id succeeds")]
	public async Task GetExistingCountryByIdSucceeds()
	{
		var countries = CountryFactory.CreateMany().ToList();
		var dbContextMock = SetupMock(countries);
		var country = countries.First();
		var query = new GetCountryById.Query(country.Id);

		var sut = new GetCountryById.Handler(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().NotBeNull();
		result!.Name.Should().Be(country.Name);
	}

	[Fact(DisplayName = "Get non-existing country by Id fails")]
	public async Task GetNonExistingCountryByIdFails()
	{
		var countries = CountryFactory.CreateMany().ToList();
		var dbContextMock = SetupMock(countries);
		var query = new GetCountryById.Query(Guid.NewGuid());

		var sut = new GetCountryById.Handler(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().BeNull();
	}

	private static Mock<AppDbContext> SetupMock(IEnumerable<Domain.Model.Country> countries)
	{
		var dbSetMock = countries.AsQueryable().BuildMockDbSet();
		var dbContextMock = new Mock<AppDbContext>();
		dbContextMock.Setup(x => x.Set<Domain.Model.Country>())
					 .Returns(dbSetMock.Object);

		return dbContextMock;
	}
}
