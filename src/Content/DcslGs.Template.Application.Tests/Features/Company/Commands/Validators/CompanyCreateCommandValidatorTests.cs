using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using DcslGs.Template.Application.Features.Company.Commands;
using DcslGs.Template.Application.Features.Company.Commands.Validators;
using DcslGs.Template.Application.Infrastructure.Context;
using DcslGs.Template.Common.Tests.Factories;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace DcslGs.Template.Application.Tests.Features.Company.Commands.Validators;

[ExcludeFromCodeCoverage]
public class CompanyCreateCommandValidatorTests
{
	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);

		sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Name being valid does not generate validation error")]
	public async Task NameDoesNotGenerateErrorWhenValid()
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var cmdMock = new Mock<CompanyCreateCommand>(new string(It.IsAny<char>(), 100),	// same Name as the already-existing Company
													 It.IsAny<string>(),				// Email
													 It.IsAny<string>(),				// WebSiteUrl
													 It.IsAny<string>(),				// Address
													 It.IsAny<string>(),				// City
													 It.IsAny<string>(),				// County
													 It.IsAny<string>(),				// PostCode
													 It.IsAny<Guid>());					// country.Id

		var sut = new CompanyCreateCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Name);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Name with empty value generates validation error")]
	public async Task NameIsEmptyGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(string.Empty,			// Name
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Name)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Name with long value generates validation error")]
	public async Task NameWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(new string(It.IsAny<char>(), 101),	// Name
													 It.IsAny<string>(),				// Email
													 It.IsAny<string>(),				// WebSiteUrl
													 It.IsAny<string>(),				// Address
													 It.IsAny<string>(),				// City
													 It.IsAny<string>(),				// County
													 It.IsAny<string>(),				// PostCode
													 It.IsAny<Guid>());					// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Name)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Theory(DisplayName = "Name which already exists generates validation error")]
	[AnonymousData]
	public async Task NameAlreadyExistsGeneratesError(Domain.Model.Company company)
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company> { company }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var cmdMock = new Mock<CompanyCreateCommand>(company.Name,          // same Name as the already-existing Company
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),    // County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Name)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Email being valid does not generate validation error")]
	public async Task EmailIsValidDoesNotGenerateError()
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),	// same Name as the already-existing Company
													 "valid@email.com",		// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Email));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Email);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Email with empty value generates validation error")]
	public async Task EmailIsEmptyGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),	// Name
													 string.Empty,			// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Email));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Email)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Theory(DisplayName = "Email being invalid generates validation error")]
	[AnonymousData]
	public async Task EmailAddressIsInvalidGeneratesError(string email)
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),	// Name
													 email,					// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Email));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Email)
						.WithErrorCode("EmailValidator")
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Website URL with long value generates validation error")]
	public async Task WebsiteUrlWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),				// Name
													 It.IsAny<string>(),				// Email
													 new string(It.IsAny<char>(), 301),	// WebSiteUrl
													 It.IsAny<string>(),				// Address
													 It.IsAny<string>(),				// City
													 It.IsAny<string>(),				// County
													 It.IsAny<string>(),				// PostCode
													 It.IsAny<Guid>());					// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.WebSiteUrl));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.WebSiteUrl)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 300)
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Website URL with empty value does not generate validation error")]
	public async Task WebsiteUrlWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),    // Name
													 It.IsAny<string>(),	// Email
													 string.Empty,			// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.WebSiteUrl));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.WebSiteUrl);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Address with long value generates validation error")]
	public async Task AddressWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),                // Name
													 It.IsAny<string>(),				// Email
													 It.IsAny<string>(),				// WebSiteUrl
													 new string(It.IsAny<char>(), 101),	// Address
													 It.IsAny<string>(),				// City
													 It.IsAny<string>(),				// County
													 It.IsAny<string>(),				// PostCode
													 It.IsAny<Guid>());					// country.Id

		var validator = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await validator.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Address));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Address)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Address with empty value does not generate validation error")]
	public async Task AddressWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),    // Name
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 string.Empty,			// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Address));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Address);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "City with long value generates validation error")]
	public async Task CityWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),                // Name
													 It.IsAny<string>(),				// Email
													 It.IsAny<string>(),				// WebSiteUrl
													 It.IsAny<string>(),				// Address
													 new string(It.IsAny<char>(), 101),	// City
													 It.IsAny<string>(),				// County
													 It.IsAny<string>(),				// PostCode
													 It.IsAny<Guid>());					// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.City));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.City)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "City with empty value does not generate validation error")]
	public async Task CityWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),    // Name
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 string.Empty,			// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.City));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.City);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "County with long value generates validation error")]
	public async Task CountyWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),                // Name
													 It.IsAny<string>(),				// Email
													 It.IsAny<string>(),				// WebSiteUrl
													 It.IsAny<string>(),				// Address
													 It.IsAny<string>(),				// City
													 new string(It.IsAny<char>(), 101),	// County
													 It.IsAny<string>(),				// PostCode
													 It.IsAny<Guid>());					// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.County));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.County)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "County with empty value does not generate validation error")]
	public async Task CountyWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),        // Name
														 It.IsAny<string>(),	// Email
														 It.IsAny<string>(),	// WebSiteUrl
														 It.IsAny<string>(),	// Address
														 It.IsAny<string>(),	// City
														 string.Empty,			// County
														 It.IsAny<string>(),	// PostCode
														 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.County));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.County);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Postcode with long value generates validation error")]
	public async Task PostcodeWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),                // Name
													 It.IsAny<string>(),				// Email
													 It.IsAny<string>(),				// WebSiteUrl
													 It.IsAny<string>(),				// Address
													 It.IsAny<string>(),				// City
													 It.IsAny<string>(),				// County
													 new string(It.IsAny<char>(), 11),	// PostCode
													 It.IsAny<Guid>());					// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.PostCode));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.PostCode)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 10)
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Postcode with empty value does not generate validation error")]
	public async Task PostcodeWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),        // Name
														 It.IsAny<string>(),	// Email
														 It.IsAny<string>(),	// WebSiteUrl
														 It.IsAny<string>(),	// Address
														 It.IsAny<string>(),	// City
														 It.IsAny<string>(),	// County
														 string.Empty,			// PostCode
														 It.IsAny<Guid>());		// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.PostCode));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.PostCode);
	}

	[Trait("Application Validators", "Company Validators")]
	[Theory(DisplayName = "Country being valid does not generate validation error")]
	[AnonymousData(true)]
	public async Task CountryIsValidDoesNotGenerateError(Domain.Model.Country country)
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var countryDbSetMock = new List<Domain.Model.Country>() { country }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Country>()).Returns(countryDbSetMock.Object);

		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),	// Name
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),	// PostCode
													 country.Id);			// country.Id

		var sut = new CompanyCreateCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.CountryId));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.CountryId);
	}

	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Country with empty value generates validation error")]
	public async Task CountryWithEmptyValueGeneratesError()
	{
		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),        // Name
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),    // County
													 It.IsAny<string>(),	// PostCode
													 Guid.Empty);			// country.Id

		var sut = new CompanyCreateCommandValidator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.CountryId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.CountryId)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Trait("Application Validators", "Company Validators")]
	[Theory(DisplayName = "Country that doesn't exist generates validation error")]
	[AnonymousData]
	public async Task CountryMustExistValidation(Domain.Model.Country country)
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var countryDbSetMock = new List<Domain.Model.Country>() { country }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Country>()).Returns(countryDbSetMock.Object);

		var cmdMock = new Mock<CompanyCreateCommand>(It.IsAny<string>(),    // Name
													 It.IsAny<string>(),	// Email
													 It.IsAny<string>(),	// WebSiteUrl
													 It.IsAny<string>(),	// Address
													 It.IsAny<string>(),	// City
													 It.IsAny<string>(),	// County
													 It.IsAny<string>(),    // PostCode
													 Guid.NewGuid());		// country.Id

		var sut = new CompanyCreateCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.CountryId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.CountryId)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}
}
