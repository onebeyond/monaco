﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests.Factories.Entities;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Queries", "Company Queries")]
public class GetCompanyByIdTests
{
	[Fact(DisplayName = "Get existing company by Id succeeds")]
	public async Task GetExistingCompanyByIdSucceeds()
	{
		var companies = CompanyFactory.CreateMany().ToList();
		var dbContextMock = SetupMock(companies);
		var company = companies.First();
		var query = new GetCompanyById.Query(company.Id);

		var sut = new GetCompanyById.Handler(dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should().NotBeNull();
		result!.Name.Should().Be(company.Name);
	}

	[Fact(DisplayName = "Get non-existing company by Id fails")]
	public async Task GetNonExistingCompanyByIdFails()
	{
		var companies = CompanyFactory.CreateMany().ToList();
		var dbContextMock = SetupMock(companies);
		var query = new GetCompanyById.Query(Guid.NewGuid());

		var sut = new GetCompanyById.Handler(dbContextMock.Object);
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
