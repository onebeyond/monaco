using AwesomeAssertions;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Company", "Get Company Page")]
public class GetCompanyPageTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	[Theory(DisplayName = "Get company page without params succeeds")]
	[AutoDomainData]
	public async Task GetCompanyPageWithoutParamsSucceeds(List<Domain.Model.Entities.Company> companies)
	{
		_dbContextMock.CreateAndSetupDbSetMock(companies);

		var query = new GetCompanyPage.Query([]);

		var sut = new GetCompanyPage.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, CancellationToken.None);

		var success = result.Should()
							.BeOfType<Success<Page<CompanyDto>>>();

		success.Subject
			   .Result
			   .Pager
			   .Count
			   .Should()
			   .Be(companies.Count);

		success.Subject
			   .Result
			   .Items
			   .Should()
			   .HaveCount(companies.Count).And
			   .Contain(x => companies.Any(c => c.Name == x.Name)).And
			   .BeInAscendingOrder(x => x.Name);
	}

	[Theory(DisplayName = "Get company page with params succeeds")]
	[AutoDomainData]
	public async Task GetCompanyPageWithParamsSucceeds(List<Domain.Model.Entities.Company> companies)
	{
		_dbContextMock.CreateAndSetupDbSetMock(companies);
		var companiesSet = companies.GetRange(0, 2);
		var queryString = new List<KeyValuePair<string, StringValues>>
						  {
							  new(nameof(CompanyDto.Name),
								  new([..companiesSet.Select(x => x.Name)])),
							  new("expand", nameof(CompanyDto.Country)),
							  new("sort", $"-{nameof(CompanyDto.Name)}")
						  };

		var query = new GetCompanyPage.Query(queryString);

		var sut = new GetCompanyPage.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, CancellationToken.None);

		var success = result.Should()
							.BeOfType<Success<Page<CompanyDto>>>();

		success.Subject
			   .Result
			   .Pager
			   .Count
			   .Should()
			   .Be(companiesSet.Count);

		success.Subject
			   .Result
			   .Items
			   .Should()
			   .HaveCount(companiesSet.Count).And
			   .Contain(x => companiesSet.Any(c => c.Name == x.Name)).And
			   .BeInDescendingOrder(x => x.Name);
	}
}