using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Delete")]
public class DeleteCompanyValidatorTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteCompany.Command _command = new(It.IsAny<Guid>());

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new DeleteCompany.Validator(_dbContextMock.Object);

		sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
	}

	[Theory(DisplayName = "Existing company passes validation correctly")]
	[AnonymousData]
	public async Task ExistingCompanyPassesValidationCorrectly(Domain.Model.Company company)
	{
		var command = _command with { Id = company.Id };

		_dbContextMock.CreateAndSetupDbSetMock(company);

		var sut = new DeleteCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldNotHaveAnyValidationErrors();
	}

	[Theory(DisplayName = "Non existing company passes validation correctly")]
	[AnonymousData]
	public async Task NonExistingCompanyPassesValidationCorrectly(Domain.Model.Company company, Guid id)
	{
		var command = _command with { Id = id };

		_dbContextMock.CreateAndSetupDbSetMock(company);

		var sut = new DeleteCompany.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldHaveValidationErrorFor(x => x.Id);
	}
}