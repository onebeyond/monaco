using AutoFixture;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
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
[Trait("Application Commands - Company", "Create")]
public class CreateCompanyValidatorTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly CreateCompany.Command Command;

	static CreateCompanyValidatorTests()
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

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CreateCompany.Validator(_dbContextMock.Object);

		sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
	}

	[Fact(DisplayName = "Name being valid does not generate validation error")]
	public async Task NameValidDoesNotGenerateError()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>());

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(Command, s => s.IncludeProperties(x => x.Name));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.Name);
	}

	[Fact(DisplayName = "Name with empty value generates validation error")]
	public async Task NameIsEmptyGeneratesError()
	{
		var command = Command with { Name = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

		validationResult.ShouldHaveValidationErrorFor(x => x.Name)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Name with long value generates validation error")]
	public async Task NameWithLongValueGeneratesError()
	{
		var command = Command with { Name = new string(It.IsAny<char>(), Domain.Model.Entities.Company.NameLength + 1) };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

		validationResult.ShouldHaveValidationErrorFor(x => x.Name)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Domain.Model.Entities.Company.NameLength)
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Name which already exists generates validation error")]
	[AutoDomainData]
	public async Task NameAlreadyExistsGeneratesError(Domain.Model.Entities.Company company)
	{
		var command = Command with { Name = company.Name };

		_dbContextMock.CreateAndSetupDbSetMock([company]);

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

		validationResult.ShouldHaveValidationErrorFor(x => x.Name)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Email being valid does not generate validation error")]
	public async Task EmailIsValidDoesNotGenerateError()
	{
		var command = Command with { Email = "valid@email.com" };

		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>());

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.Email);
	}

	[Fact(DisplayName = "Email with empty value generates validation error")]
	public async Task EmailIsEmptyGeneratesError()
	{
		var command = Command with { Email = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

		validationResult.ShouldHaveValidationErrorFor(x => x.Email)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Email being invalid generates validation error")]
	[AutoDomainData]
	public async Task EmailAddressIsInvalidGeneratesError(string email)
	{
		var command = Command with { Email = email };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

		validationResult.ShouldHaveValidationErrorFor(x => x.Email)
						.WithErrorCode("EmailValidator")
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Email with long value generates validation error")]
	[AutoDomainData]
	public async Task EmailWithLongValueGeneratesError(string emailDomain)
	{
		var command = Command with
					  {
						  Email = string.Join("@",
											  new string(It.IsAny<char>(), Domain.Model.Entities.Company.EmailLength),
											  emailDomain)
					  };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

		validationResult.ShouldHaveValidationErrorFor(x => x.Email)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Domain.Model.Entities.Company.EmailLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Website URL with long value generates validation error")]
	public async Task WebsiteUrlWithLongValueGeneratesError()
	{
		var command = Command with { WebSiteUrl = new string(It.IsAny<char>(), Domain.Model.Entities.Company.WebSiteUrlLength + 1) };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.WebSiteUrl));

		validationResult.ShouldHaveValidationErrorFor(x => x.WebSiteUrl)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Domain.Model.Entities.Company.WebSiteUrlLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Website URL with empty value does not generate validation error")]
	public async Task WebsiteUrlWithEmptyValueDoesNotGenerateError()
	{
		var command = Command with { WebSiteUrl = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.WebSiteUrl));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.WebSiteUrl);
	}

	[Fact(DisplayName = "Street with long value generates validation error")]
	public async Task StreetWithLongValueGeneratesError()
	{
		var command = Command with { Street = new string(It.IsAny<char>(), Address.StreetLength + 1) };

		var validator = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await validator.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street));

		validationResult.ShouldHaveValidationErrorFor(x => x.Street)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Address.StreetLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Street with empty value does not generate validation error")]
	public async Task StreetWithEmptyValueDoesNotGenerateError()
	{
		var command = Command with { Street = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.Street);
	}

	[Fact(DisplayName = "City with long value generates validation error")]
	public async Task CityWithLongValueGeneratesError()
	{
		var command = Command with { City = new string(It.IsAny<char>(), Address.CityLength + 1) };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.City));

		validationResult.ShouldHaveValidationErrorFor(x => x.City)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Address.CityLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "City with empty value does not generate validation error")]
	public async Task CityWithEmptyValueDoesNotGenerateError()
	{
		var command = Command with { City = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.City));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.City);
	}

	[Fact(DisplayName = "County with long value generates validation error")]
	public async Task CountyWithLongValueGeneratesError()
	{
		var command = Command with { County = new string(It.IsAny<char>(), Address.CountyLength + 1) };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.County));

		validationResult.ShouldHaveValidationErrorFor(x => x.County)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Address.CountyLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "County with empty value does not generate validation error")]
	public async Task CountyWithEmptyValueDoesNotGenerateError()
	{
		var command = Command with { County = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.County));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.County);
	}

	[Fact(DisplayName = "Postcode with long value generates validation error")]
	public async Task PostcodeWithLongValueGeneratesError()
	{
		var command = Command with { PostCode = new string(It.IsAny<char>(), Address.PostCodeLength + 1) };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.PostCode));

		validationResult.ShouldHaveValidationErrorFor(x => x.PostCode)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Address.PostCodeLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Postcode with empty value does not generate validation error")]
	public async Task PostcodeWithEmptyValueDoesNotGenerateError()
	{
		var command = Command with { PostCode = string.Empty };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.PostCode));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.PostCode);
	}

	[Theory(DisplayName = "Country being valid does not generate validation error")]
	[AutoDomainData(true)]
	public async Task CountryIsValidDoesNotGenerateError(Domain.Model.Entities.Country country)
	{
		var command = Command with { CountryId = country.Id };

		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>())
					  .CreateAndSetupDbSetMock([country]);

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street,
																							 x => x.City,
																							 x => x.County,
																							 x => x.PostCode,
																							 x => x.CountryId));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.CountryId);
	}

	[Fact(DisplayName = "Country with null value does not generate validation error when Address fields null")]
	public async Task CountryWithNullValueDoesNotGenerateErrorWhenAddressFieldsNull()
	{
		var command = Command with
					  {
						  Street = null,
						  City = null,
						  County = null,
						  PostCode = null,
						  CountryId = null
					  };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street,
																							 x => x.City,
																							 x => x.County,
																							 x => x.PostCode,
																							 x => x.CountryId));

		validationResult.ShouldNotHaveValidationErrorFor(x => x.CountryId);
	}

	[Fact(DisplayName = "Country with null value generates validation error when Address fields present")]
	public async Task CountryWithNullValueGeneratesErrorWhenAddressFieldsPresent()
	{
		var command = Command with
					  {
						  Street = string.Empty,
						  City = string.Empty,
						  County = string.Empty,
						  PostCode = string.Empty,
						  CountryId = null
					  };

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street,
																							 x => x.City,
																							 x => x.County,
																							 x => x.PostCode,
																							 x => x.CountryId));

		validationResult.ShouldHaveValidationErrorFor(x => x.CountryId);
	}

	[Theory(DisplayName = "Country that doesn't exist generates validation error")]
	[AutoDomainData]
	public async Task CountryMustExistValidation(Domain.Model.Entities.Country country)
	{
		var command = Command with { CountryId = Guid.NewGuid() };

		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>())
					  .CreateAndSetupDbSetMock(country);

		var sut = new CreateCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.CountryId));

		validationResult.ShouldHaveValidationErrorFor(x => x.CountryId)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}
}
