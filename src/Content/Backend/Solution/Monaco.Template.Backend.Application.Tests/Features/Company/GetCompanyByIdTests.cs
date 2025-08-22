using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories.Entities;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Application.Persistence;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Company", "Get Company by Id")]
public class GetCompanyByIdTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	[Fact(DisplayName = "Get existing company by Id succeeds")]
	public async Task GetExistingCompanyByIdSucceeds()
	{
		var companies = CompanyFactory.CreateMany().ToList();
		_dbContextMock.CreateAndSetupDbSetMock(companies);
		var company = companies.First();

		var query = new GetCompanyById.Query(company.Id);

		var sut = new GetCompanyById.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.Name
			   .Should()
			   .Be(company.Name);
	}

	[Fact(DisplayName = "Get non-existing company by Id fails")]
	public async Task GetNonExistingCompanyByIdFails()
	{
		_dbContextMock.CreateAndSetupDbSetMock(CompanyFactory.CreateMany());

		var query = new GetCompanyById.Query(Guid.NewGuid());

		var sut = new GetCompanyById.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .BeNull();
	}
}
