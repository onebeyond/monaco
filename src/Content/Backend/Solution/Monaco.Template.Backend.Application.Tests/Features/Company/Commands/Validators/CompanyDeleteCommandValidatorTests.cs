using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Features.Company.Commands;
using Monaco.Template.Backend.Application.Features.Company.Commands.Validators;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
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
public class CompanyDeleteCommandValidatorTests
{
	[Trait("Application Validators", "Company Validators")]
	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CompanyDeleteCommandValidator(new Mock<AppDbContext>().Object);

		sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
	}

	[Trait("Application Validators", "Company Validators")]
	[Theory(DisplayName = "Existing company passes validation correctly")]
	[AnonymousData]
	public async Task ExistingCompanyPassesValidationCorrectly(Domain.Model.Company company)
	{
		var dbContextMock = new Mock<AppDbContext>();
		var companyDbSetMock = new List<Domain.Model.Company>(new[] { company }).AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);
		var cmdMock = new Mock<CompanyDeleteCommand>(company.Id);

		var sut = new CompanyDeleteCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldNotHaveAnyValidationErrors();
	}

	[Trait("Application Validators", "Company Validators")]
	[Theory(DisplayName = "Non existing company passes validation correctly")]
	[AnonymousData]
	public async Task NonExistingCompanyPassesValidationCorrectly(Domain.Model.Company company, Guid id)
	{
		var dbContextMock = new Mock<AppDbContext>();
		var companyDbSetMock = new List<Domain.Model.Company>(new[] { company }).AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);
		var cmdMock = new Mock<CompanyDeleteCommand>(id);

		var sut = new CompanyDeleteCommandValidator(dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(cmdMock.Object, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldHaveValidationErrorFor(x => x.Id);
	}
}