using AutoFixture;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Application.Persistence;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Create")]
public class CreateCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly CreateCompany.Command Command;

	static CreateCompanyHandlerTests()
	{
		var fixture = new Fixture();
		Command = new(fixture.Create<string>(), // Name
					  fixture.Create<string>(), // Email
					  fixture.Create<string>(), // WebsiteUrl
					  fixture.Create<string>(), // Street
					  fixture.Create<string>(), // City
					  fixture.Create<string>(), // County
					  fixture.Create<string>(), // PostCode
					  fixture.Create<Guid>()); // CountryId
	}

	[Theory(DisplayName = "Create new company succeeds")]
	[AutoDomainData]
	public async Task CreateNewCompanySucceeds(Domain.Model.Entities.Country country)
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>(), out var companyDbSetMock)
					  .CreateAndSetupDbSetMock([country]);

		var sut = new CreateCompany.Handler(_dbContextMock.Object);
		var result = await sut.Handle(Command, new CancellationToken());

		companyDbSetMock.Verify(x => x.Attach(It.IsAny<Domain.Model.Entities.Company>()), Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
		result.ValidationResult
			  .IsValid
			  .Should()
			  .BeTrue();
		result.ItemNotFound
			  .Should()
			  .BeFalse();
	}
}