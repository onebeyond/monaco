using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands", "Company Commands")]
public class EditCompanyTests
{
	private static readonly EditCompany.Command _command = new(It.IsAny<Guid>(),	// Id
															   It.IsAny<string>(),	// Name
															   It.IsAny<string>(),	// Email
															   It.IsAny<string>(),  // WebSiteUrl
															   It.IsAny<string>(),  // Street
															   It.IsAny<string>(),	// City
															   It.IsAny<string>(),	// County
															   It.IsAny<string>(),  // PostCode
															   It.IsAny<Guid>());	// CountryId

	[ExcludeFromCodeCoverage]
	[Trait("Application Commands", "Edit Company Handler")]
	public class HandlerTests
	{
		[Theory(DisplayName = "Edit company succeeds")]
		[AnonymousData]
		public async Task EditCompanySucceeds(Domain.Model.Country country)
		{
			var companyMock = new Mock<Domain.Model.Company>();
			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company> { companyMock.Object }.AsQueryable().BuildMockDbSet();
			companyDbSetMock.Setup(x => x.FindAsync(new object[] { It.IsAny<Guid>() }, It.IsAny<CancellationToken>()))
							.ReturnsAsync(companyMock.Object);
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>())
						 .Returns(companyDbSetMock.Object);
			var countryDbSetMock = new[] { country }.AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Country>())
						 .Returns(countryDbSetMock.Object);

			var sut = new EditCompany.Handler(dbContextMock.Object);
			var result = await sut.Handle(_command, new CancellationToken());

			companyMock.Verify(x => x.Update(It.IsAny<string>(),
											 It.IsAny<string>(),
											 It.IsAny<string>(),
											 It.IsAny<Address>()),
							   Times.Once);
			dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
			result.ValidationResult.IsValid.Should().BeTrue();
			result.ItemNotFound.Should().BeFalse();
		}
	}

	[ExcludeFromCodeCoverage]
	[Trait("Application Validators", "Company Validators")]
	public class ValidatorTests
	{
		[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
		public void ValidatorRuleLevelCascadeModeIsStop()
		{
			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);

			sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
		}

		[Theory(DisplayName = "Existing company passes validation correctly")]
		[AnonymousData]
		public async Task ExistingCompanyPassesValidationCorrectly(Domain.Model.Company company)
		{
			var command = _command with { Id = company.Id, CountryId = Guid.NewGuid() };

			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company>(new[] { company }).AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

			validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
			validationResult.ShouldNotHaveAnyValidationErrors();
		}

		[Theory(DisplayName = "Non existing company passes validation correctly")]
		[AnonymousData]
		public async Task NonExistingCompanyPassesValidationCorrectly(Domain.Model.Company company, Guid id)
		{
			var command = _command with { Id = id, CountryId = Guid.NewGuid() };

			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company>(new[] { company }).AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

			validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
			validationResult.ShouldHaveValidationErrorFor(x => x.Id);
		}

		[Fact(DisplayName = "Name being valid does not generate validation error")]
		public async Task NameDoesNotGenerateErrorWhenValid()
		{
			var command = _command with { Name = new string(It.IsAny<char>(), 100) };

			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.Name);
		}

		[Fact(DisplayName = "Name with empty value generates validation error")]
		public async Task NameIsEmptyGeneratesError()
		{
			var command = _command with { Name = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

			validationResult.ShouldHaveValidationErrorFor(x => x.Name)
							.WithErrorCode("NotEmptyValidator")
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "Name with long value generates validation error")]
		public async Task NameWithLongValueGeneratesError()
		{
			var command = _command with { Name = new string(It.IsAny<char>(), 101) };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

			validationResult.ShouldHaveValidationErrorFor(x => x.Name)
							.WithErrorCode("MaximumLengthValidator")
							.WithMessageArgument("MaxLength", 100)
							.Should()
							.HaveCount(1);
		}

		[Theory(DisplayName = "Name which already exists generates validation error")]
		[AnonymousData]
		public async Task NameAlreadyExistsGeneratesError(Domain.Model.Company company, Guid id)
		{
			var command = _command with { Id = id, Name = company.Name };

			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company> { company }.AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Name));

			validationResult.ShouldHaveValidationErrorFor(x => x.Name)
							.WithErrorCode("AsyncPredicateValidator")
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "Email being valid does not generate validation error")]
		public async Task EmailIsValidDoesNotGenerateError()
		{
			var command = _command with { Email = "valid@email.com" };

			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.Email);
		}

		[Fact(DisplayName = "Email with empty value generates validation error")]
		public async Task EmailIsEmptyGeneratesError()
		{
			var command = _command with { Email = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

			validationResult.ShouldHaveValidationErrorFor(x => x.Email)
							.WithErrorCode("NotEmptyValidator")
							.Should()
							.HaveCount(1);
		}

		[Theory(DisplayName = "Email being invalid generates validation error")]
		[AnonymousData]
		public async Task EmailAddressIsInvalidGeneratesError(string email)
		{
			var command = _command with { Email = email };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Email));

			validationResult.ShouldHaveValidationErrorFor(x => x.Email)
							.WithErrorCode("EmailValidator")
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "Website URL with long value generates validation error")]
		public async Task WebsiteUrlWithLongValueGeneratesError()
		{
			var command = _command with { WebSiteUrl = new string(It.IsAny<char>(), 301) };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.WebSiteUrl));

			validationResult.ShouldHaveValidationErrorFor(x => x.WebSiteUrl)
							.WithErrorCode("MaximumLengthValidator")
							.WithMessageArgument("MaxLength", 300)
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "Website URL with empty value does not generate validation error")]
		public async Task WebsiteUrlWithEmptyValueDoesNotGenerateError()
		{
			var command = _command with { WebSiteUrl = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.WebSiteUrl));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.WebSiteUrl);
		}

		[Fact(DisplayName = "Street with long value generates validation error")]
		public async Task AddressWithLongValueGeneratesError()
		{
			var command = _command with { Street = new string(It.IsAny<char>(), 101) };

			var validator = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await validator.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street));

			validationResult.ShouldHaveValidationErrorFor(x => x.Street)
							.WithErrorCode("MaximumLengthValidator")
							.WithMessageArgument("MaxLength", 100)
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "Street with empty value does not generate validation error")]
		public async Task AddressWithEmptyValueDoesNotGenerateError()
		{
			var command = _command with { Street = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.Street));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.Street);
		}

		[Fact(DisplayName = "City with long value generates validation error")]
		public async Task CityWithLongValueGeneratesError()
		{
			var command = _command with { City = new string(It.IsAny<char>(), 101) };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.City));

			validationResult.ShouldHaveValidationErrorFor(x => x.City)
							.WithErrorCode("MaximumLengthValidator")
							.WithMessageArgument("MaxLength", 100)
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "City with empty value does not generate validation error")]
		public async Task CityWithEmptyValueDoesNotGenerateError()
		{
			var command = _command with { City = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.City));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.City);
		}

		[Fact(DisplayName = "County with long value generates validation error")]
		public async Task CountyWithLongValueGeneratesError()
		{
			var command = _command with { County = new string(It.IsAny<char>(), 101) };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.County));

			validationResult.ShouldHaveValidationErrorFor(x => x.County)
							.WithErrorCode("MaximumLengthValidator")
							.WithMessageArgument("MaxLength", 100)
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "County with empty value does not generate validation error")]
		public async Task CountyWithEmptyValueDoesNotGenerateError()
		{
			var command = _command with { County = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.County));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.County);
		}

		[Fact(DisplayName = "Postcode with long value generates validation error")]
		public async Task PostcodeWithLongValueGeneratesError()
		{
			var command = _command with { PostCode = new string(It.IsAny<char>(), 11) };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.PostCode));

			validationResult.ShouldHaveValidationErrorFor(x => x.PostCode)
							.WithErrorCode("MaximumLengthValidator")
							.WithMessageArgument("MaxLength", 10)
							.Should()
							.HaveCount(1);
		}

		[Fact(DisplayName = "Postcode with empty value does not generate validation error")]
		public async Task PostcodeWithEmptyValueDoesNotGenerateError()
		{
			var command = _command with { PostCode = string.Empty };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.PostCode));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.PostCode);
		}

		[Theory(DisplayName = "Country being valid does not generate validation error")]
		[AnonymousData(true)]
		public async Task CountryIsValidDoesNotGenerateError(Domain.Model.Country country)
		{
			var command = _command with { CountryId = country.Id };

			var dbContextMock = new Mock<AppDbContext>();

			var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var countryDbSetMock = new List<Domain.Model.Country>() { country }.AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Country>()).Returns(countryDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.CountryId));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.CountryId);
		}

		[Fact(DisplayName = "Country with null value does not generate validation error")]
		public async Task CountryWithNullValueDoesNotGenerateError()
		{
			var command = _command with { CountryId = null };

			var sut = new EditCompany.Validator(new Mock<AppDbContext>().Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.CountryId));

			validationResult.ShouldNotHaveValidationErrorFor(x => x.CountryId);
		}

		[Theory(DisplayName = "Country that doesn't exist generates validation error")]
		[AnonymousData]
		public async Task CountryMustExistValidation(Domain.Model.Country country)
		{
			var command = _command with { CountryId = Guid.NewGuid() };

			var dbContextMock = new Mock<AppDbContext>();

			var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);

			var countryDbSetMock = new List<Domain.Model.Country>() { country }.AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Country>()).Returns(countryDbSetMock.Object);

			var sut = new EditCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(command, s => s.IncludeProperties(x => x.CountryId));

			validationResult.ShouldHaveValidationErrorFor(x => x.CountryId)
							.WithErrorCode("AsyncPredicateValidator")
							.Should()
							.HaveCount(1);
		}
	}
}
