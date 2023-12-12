using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands", "Create Company Handler")]
public class CreateCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly CreateCompany.Command _command = new(It.IsAny<string>(),	// Name
																 It.IsAny<string>(),	// Email
																 It.IsAny<string>(),	// WebsiteUrl
																 It.IsAny<string>(),	// Street
																 It.IsAny<string>(),	// City
																 It.IsAny<string>(),	// County
																 It.IsAny<string>(),	// PostCode
																 It.IsAny<Guid>());		// CountryId


	[Theory(DisplayName = "Create new company succeeds")]
	[AnonymousData]
	public async Task CreateNewCompanySucceeds(Domain.Model.Country country)
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Company>(), out var companyDbSetMock)
					  .CreateAndSetupDbSetMock([country]);

		var sut = new CreateCompany.Handler(_dbContextMock.Object);
		var result = await sut.Handle(_command, new CancellationToken());

		companyDbSetMock.Verify(x => x.Attach(It.IsAny<Domain.Model.Company>()), Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
		result.ValidationResult.IsValid.Should().BeTrue();
		result.ItemNotFound.Should().BeFalse();
	}
}
