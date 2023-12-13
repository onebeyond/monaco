using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands", "Edit Company")]
public class EditCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly EditCompany.Command _command = new(It.IsAny<Guid>(),    // Id
															   It.IsAny<string>(),  // Name
															   It.IsAny<string>(),  // Email
															   It.IsAny<string>(),  // WebSiteUrl
															   It.IsAny<string>(),  // Street
															   It.IsAny<string>(),  // City
															   It.IsAny<string>(),  // County
															   It.IsAny<string>(),  // PostCode
															   It.IsAny<Guid>());   // CountryId

	[Theory(DisplayName = "Edit company succeeds")]
	[AnonymousData]
	public async Task EditCompanySucceeds(Domain.Model.Country country)
	{
		_dbContextMock.CreateEntityMockAndSetupDbSetMock<AppDbContext, Domain.Model.Company>(out var companyMock)
					  .CreateAndSetupDbSetMock(country);

		var sut = new EditCompany.Handler(_dbContextMock.Object);
		var result = await sut.Handle(_command, new CancellationToken());

		companyMock.Verify(x => x.Update(It.IsAny<string>(),
										 It.IsAny<string>(),
										 It.IsAny<string>(),
										 It.IsAny<Address>()),
						   Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
		result.ValidationResult.IsValid.Should().BeTrue();
		result.ItemNotFound.Should().BeFalse();
	}
}
