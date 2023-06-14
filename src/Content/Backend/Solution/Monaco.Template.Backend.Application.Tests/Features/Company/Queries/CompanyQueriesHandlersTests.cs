using FluentAssertions;
using Microsoft.Extensions.Primitives;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Company.Queries;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Common.Tests.Factories.Entities;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company.Queries;

[ExcludeFromCodeCoverage]
[Trait("Application Queries", "Company Queries")]
public class CompanyQueriesHandlersTests
{
	[Theory(DisplayName = "Get company page without params succeeds")]
	[AnonymousData]
	public async Task GetCompanyPageWithoutParamsSucceeds(List<Domain.Model.Company> companies)
	{
		var dbContextMock = SetupMock(companies);
		var query = new GetCompanyPageQuery(new List<KeyValuePair<string, StringValues>>());

		var sut = new CompanyQueriesHandlers(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().NotBeNull();
		result!.Pager.Count.Should().Be(companies.Count);
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
		var dbContextMock = SetupMock(companies);
		var companiesSet = companies.GetRange(0, 2);
		var query = new GetCompanyPageQuery(new List<KeyValuePair<string, StringValues>>(new KeyValuePair<string, StringValues>[]
																						 {
																							 new (nameof(CompanyDto.Name),
																								  new(companiesSet.Select(x => x.Name).ToArray())),
																							 new ("sort", $"-{nameof(CompanyDto.Name)}")
																						 }));

		var sut = new CompanyQueriesHandlers(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().NotBeNull();
		result!.Pager.Count.Should().Be(companiesSet.Count);
		result.Items
			  .Should()
			  .HaveCount(companiesSet.Count).And
			  .Contain(x => companiesSet.Any(c => c.Name == x.Name)).And
			  .BeInDescendingOrder(x => x.Name);
	}

	[Trait("Application Queries", "Company Queries")]
	[Fact(DisplayName = "Get existing company by Id succeeds")]
	public async Task GetExistingCompanyByIdSucceeds()
	{
		var companies = CompanyFactory.CreateMany().ToList();
		var dbContextMock = SetupMock(companies);
		var company = companies.First();
		var query = new GetCompanyByIdQuery(company.Id);

		var sut = new CompanyQueriesHandlers(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().NotBeNull();
		result!.Name.Should().Be(company.Name);
	}

	[Fact(DisplayName = "Get non-existing company by Id fails")]
	public async Task GetNonExistingCompanyByIdFails()
	{
		var companies = CompanyFactory.CreateMany().ToList();
		var dbContextMock = SetupMock(companies);
		var query = new GetCompanyByIdQuery(Guid.NewGuid());

		var sut = new CompanyQueriesHandlers(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().BeNull();
	}

	private static Mock<AppDbContext> SetupMock(IEnumerable<Domain.Model.Company> companies)
	{
		var dbSetMock = companies.AsQueryable().BuildMockDbSet();
		var dbContextMock = new Mock<AppDbContext>();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>())
					 .Returns(dbSetMock.Object);

		return dbContextMock;
	}
}