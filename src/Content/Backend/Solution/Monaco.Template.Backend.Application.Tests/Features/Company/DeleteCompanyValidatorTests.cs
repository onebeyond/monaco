using AutoFixture;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Delete")]
public class DeleteCompanyValidatorTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteCompany.Command Command = new(new Fixture().Create<Guid>());

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new DeleteCompany.Validator(_dbContextMock.Object);

		sut.RuleLevelCascadeMode.
			Should()
			.Be(CascadeMode.Stop);
	}

	[Theory(DisplayName = "Existing company passes validation correctly")]
	[AutoDomainData]
	public async Task ExistingCompanyPassesValidationCorrectly(Domain.Model.Entities.Company company)
	{
		var command = Command with { Id = company.Id };

		_dbContextMock.CreateAndSetupDbSetMock(company);

		var sut = new DeleteCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted
						.Should()
						.Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldNotHaveAnyValidationErrors();
	}

	[Theory(DisplayName = "Non existing company generates error")]
	[AutoDomainData]
	public async Task NonExistingCompanyGenertesError(Domain.Model.Entities.Company company, Guid id)
	{
		var command = Command with { Id = id };

		_dbContextMock.CreateAndSetupDbSetMock(company);

		var sut = new DeleteCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted
						.Should()
						.Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldHaveValidationErrorFor(x => x.Id);
	}
#if filesSupport

	[Theory(DisplayName = "Company assigned to Product generates error")]
	[AutoDomainData]
	public async Task CompanyAssignedToProductGeneratesError(Domain.Model.Entities.Company company, Domain.Model.Entities.Product product)
	{
		var command = Command with { Id = company.Id };
		
		product.Update(product.Title,
					   product.Description,
					   product.Price,
					   company);

		_dbContextMock.CreateAndSetupDbSetMock(company);
		_dbContextMock.CreateAndSetupDbSetMock(product);

		var sut = new DeleteCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command);

		validationResult.ShouldHaveValidationErrorFor(x => x);
	}
#endif
}