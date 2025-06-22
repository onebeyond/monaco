using AutoFixture;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Edit")]
public class EditCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	private static readonly EditCompany.Command Command;

	static EditCompanyHandlerTests()
	{
		var fixture = new Fixture();
		Command = new(fixture.Create<Guid>(),     // Id
					  fixture.Create<string>(),   // Name
					  fixture.Create<string>(),   // Email
					  fixture.Create<string>(),   // WebSiteUrl
					  fixture.Create<string>(),   // Street
					  fixture.Create<string>(),   // City
					  fixture.Create<string>(),   // County
					  fixture.Create<string>(),   // PostCode
					  fixture.Create<Guid>());    // CountryId
	}

	[Theory(DisplayName = "Edit company succeeds")]
	[AutoDomainData]
	public async Task EditCompanySucceeds(Domain.Model.Entities.Country country)
	{
		_dbContextMock.CreateEntityMockAndSetupDbSetMock<AppDbContext, Domain.Model.Entities.Company>(out var companyMock)
					  .CreateAndSetupDbSetMock(country);

		var command = Command with { Id = companyMock.Object.Id };

		var sut = new EditCompany.Handler(_dbContextMock.Object);
		var result = await sut.Handle(command, new CancellationToken());

		companyMock.Verify(x => x.Update(It.IsAny<string>(),
										 It.IsAny<string>(),
										 It.IsAny<string>(),
										 It.IsAny<Address>()),
						   Times.Once);
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
