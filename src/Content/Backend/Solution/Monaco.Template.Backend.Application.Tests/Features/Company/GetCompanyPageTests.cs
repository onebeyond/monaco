using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
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
	[AnonymousData]
	public async Task GetCompanyPageWithoutParamsSucceeds(List<Domain.Model.Company> companies)
	{
		_dbContextMock.CreateAndSetupDbSetMock(companies);

		var query = new GetCompanyPage.Query(new List<KeyValuePair<string, StringValues>>());

		var sut = new GetCompanyPage.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.Pager
			   .Count
			   .Should()
			   .Be(companies.Count);
		result.Items
			  .Should()
			  .HaveCount(companies.Count).And
			  .Contain(x => companies.Any(c => c.Name == x.Name)).And
			  .BeInAscendingOrder(x => x.Name);
	}

	[Theory(DisplayName = "Get company page with params succeeds")]
	[AnonymousData]
	public async Task GetCompanyPageWithParamsSucceeds(List<Domain.Model.Company> companies)
	{
		_dbContextMock.CreateAndSetupDbSetMock(companies);
		var companiesSet = companies.GetRange(0, 2);
		var queryString = new List<KeyValuePair<string, StringValues>>
						  {
							  new(nameof(CompanyDto.Name),
								  new(companiesSet.Select(x => x.Name)
												  .ToArray())),
							  new("expand", nameof(CompanyDto.Country)),
							  new("sort", $"-{nameof(CompanyDto.Name)}")
						  };

		var query = new GetCompanyPage.Query(queryString);

		var sut = new GetCompanyPage.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.Pager
			   .Count
			   .Should()
			   .Be(companiesSet.Count);
		result.Items
			  .Should()
			  .HaveCount(companiesSet.Count).And
			  .Contain(x => companiesSet.Any(c => c.Name == x.Name)).And
			  .BeInDescendingOrder(x => x.Name);
	}
}
