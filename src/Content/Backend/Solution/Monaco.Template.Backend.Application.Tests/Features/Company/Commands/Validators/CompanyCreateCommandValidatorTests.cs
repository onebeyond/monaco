using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests.Factories;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company.Commands.Validators;

[ExcludeFromCodeCoverage]
[Trait("Application Validators", "Company Validators")]
public class CompanyCreateCommandValidatorTests
{
	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);

		sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
	}

	[Fact(DisplayName = "Name being valid does not generate validation error")]
	public async Task NameDoesNotGenerateErrorWhenValid()
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var cmdMock = new Mock<CreateCompany.Command>(new string(It.IsAny<char>(), 100), // same Name as the already-existing Company
													  It.IsAny<string>(),                // Email
													  It.IsAny<string>(),                // WebSiteUrl
													  It.IsAny<string>(),                // Street
													  It.IsAny<string>(),                // City
													  It.IsAny<string>(),                // County
													  It.IsAny<string>(),                // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var sut = new CreateCompany.Validator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Name);
	}

	[Fact(DisplayName = "Name with empty value generates validation error")]
	public async Task NameIsEmptyGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(string.Empty,          // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Name)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Name with long value generates validation error")]
	public async Task NameWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(new string(It.IsAny<char>(), 101), // Name
													  It.IsAny<string>(),                // Email
													  It.IsAny<string>(),                // WebSiteUrl
													  It.IsAny<string>(),                // Street
													  It.IsAny<string>(),                // City
													  It.IsAny<string>(),                // County
													  It.IsAny<string>(),                // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Name)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Name which already exists generates validation error")]
	[AnonymousData]
	public async Task NameAlreadyExistsGeneratesError(Domain.Model.Company company)
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company> { company }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var cmdMock = new Mock<CreateCompany.Command>(company.Name,          // same Name as the already-existing Company
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Name));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Name)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Email being valid does not generate validation error")]
	public async Task EmailIsValidDoesNotGenerateError()
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // same Name as the already-existing Company
													  "valid@email.com",     // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Email));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Email);
	}

	[Fact(DisplayName = "Email with empty value generates validation error")]
	public async Task EmailIsEmptyGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  string.Empty,          // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Email));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Email)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Email being invalid generates validation error")]
	[AnonymousData]
	public async Task EmailAddressIsInvalidGeneratesError(string email)
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  email,                 // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Email));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Email)
						.WithErrorCode("EmailValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Website URL with long value generates validation error")]
	public async Task WebsiteUrlWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),                // Name
													  It.IsAny<string>(),                // Email
													  new string(It.IsAny<char>(), 301), // WebSiteUrl
													  It.IsAny<string>(),                // Street
													  It.IsAny<string>(),                // City
													  It.IsAny<string>(),                // County
													  It.IsAny<string>(),                // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.WebSiteUrl));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.WebSiteUrl)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 300)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Website URL with empty value does not generate validation error")]
	public async Task WebsiteUrlWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  string.Empty,          // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.WebSiteUrl));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.WebSiteUrl);
	}

	[Fact(DisplayName = "Street with long value generates validation error")]
	public async Task StreetWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),                // Name
													  It.IsAny<string>(),                // Email
													  It.IsAny<string>(),                // WebSiteUrl
													  new string(It.IsAny<char>(), 101), // Street
													  It.IsAny<string>(),                // City
													  It.IsAny<string>(),                // County
													  It.IsAny<string>(),                // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var validator = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await validator.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Street));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Street)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Street with empty value does not generate validation error")]
	public async Task StreetWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  string.Empty,          // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Street));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Street);
	}

	[Fact(DisplayName = "City with long value generates validation error")]
	public async Task CityWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),                // Name
													  It.IsAny<string>(),                // Email
													  It.IsAny<string>(),                // WebSiteUrl
													  It.IsAny<string>(),                // Street
													  new string(It.IsAny<char>(), 101), // City
													  It.IsAny<string>(),                // County
													  It.IsAny<string>(),                // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.City));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.City)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "City with empty value does not generate validation error")]
	public async Task CityWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  string.Empty,          // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.City));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.City);
	}

	[Fact(DisplayName = "County with long value generates validation error")]
	public async Task CountyWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),                // Name
													  It.IsAny<string>(),                // Email
													  It.IsAny<string>(),                // WebSiteUrl
													  It.IsAny<string>(),                // Street
													  It.IsAny<string>(),                // City
													  new string(It.IsAny<char>(), 101), // County
													  It.IsAny<string>(),                // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.County));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.County)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 100)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "County with empty value does not generate validation error")]
	public async Task CountyWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  string.Empty,          // County
													  It.IsAny<string>(),    // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.County));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.County);
	}

	[Fact(DisplayName = "Postcode with long value generates validation error")]
	public async Task PostcodeWithLongValueGeneratesError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),                // Name
													  It.IsAny<string>(),                // Email
													  It.IsAny<string>(),                // WebSiteUrl
													  It.IsAny<string>(),                // Street
													  It.IsAny<string>(),                // City
													  It.IsAny<string>(),                // County
													  new string(It.IsAny<char>(), 11),  // PostCode
													  It.IsAny<Guid>());                 // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.PostCode));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.PostCode)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", 10)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Postcode with empty value does not generate validation error")]
	public async Task PostcodeWithEmptyValueDoesNotGenerateError()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),	 // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  string.Empty,          // PostCode
													  It.IsAny<Guid>());     // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.PostCode));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.PostCode);
	}

	[Theory(DisplayName = "Country being valid does not generate validation error")]
	[AnonymousData(true)]
	public async Task CountryIsValidDoesNotGenerateError(Domain.Model.Country country)
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var countryDbSetMock = new List<Domain.Model.Country> { country }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Country>()).Returns(countryDbSetMock.Object);

		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  country.Id);           // country.Id

		var sut = new CreateCompany.Validator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Street,
																												  cmd => cmd.City,
																												  cmd => cmd.County,
																												  cmd => cmd.PostCode,
																												  cmd => cmd.CountryId));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.CountryId);
	}

	[Fact(DisplayName = "Country with null value does not generate validation error when Address fields null")]
	public async Task CountryWithNullValueDoesNotGenerateErrorWhenAddressFieldsNull()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  null!,                 // Street
													  null!,                 // City
													  null!,                 // County
													  null!,                 // PostCode
													  null!);                // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Street,
																												  cmd => cmd.City,
																												  cmd => cmd.County,
																												  cmd => cmd.PostCode,
																												  cmd => cmd.CountryId));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.CountryId);
	}

	[Fact(DisplayName = "Country with null value generates validation error when Address fields present")]
	public async Task CountryWithNullValueGeneratesErrorWhenAddressFieldsPresent()
	{
		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(), // Name
													  It.IsAny<string>(), // Email
													  It.IsAny<string>(), // WebSiteUrl
													  string.Empty,       // Street
													  string.Empty,       // City
													  string.Empty,       // County
													  string.Empty,       // PostCode
													  null!);             // country.Id

		var sut = new CreateCompany.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.Street,
																												  cmd => cmd.City,
																												  cmd => cmd.County,
																												  cmd => cmd.PostCode,
																												  cmd => cmd.CountryId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.CountryId);
	}

	[Theory(DisplayName = "Country that doesn't exist generates validation error")]
	[AnonymousData]
	public async Task CountryMustExistValidation(Domain.Model.Country country)
	{
		var dbContextMock = new Mock<AppDbContext>();

		var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

		var countryDbSetMock = new List<Domain.Model.Country> { country }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Country>()).Returns(countryDbSetMock.Object);

		var cmdMock = new Mock<CreateCompany.Command>(It.IsAny<string>(),    // Name
													  It.IsAny<string>(),    // Email
													  It.IsAny<string>(),    // WebSiteUrl
													  It.IsAny<string>(),    // Street
													  It.IsAny<string>(),    // City
													  It.IsAny<string>(),    // County
													  It.IsAny<string>(),    // PostCode
													  Guid.NewGuid());       // country.Id

		var sut = new CreateCompany.Validator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, strategy => strategy.IncludeProperties(cmd => cmd.CountryId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.CountryId)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}
}
