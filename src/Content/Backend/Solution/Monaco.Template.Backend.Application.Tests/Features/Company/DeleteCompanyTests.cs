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
public class DeleteCompanyTests
{
	[ExcludeFromCodeCoverage]
	[Trait("Application Commands", "Delete Company Handler")]
	public class HandlerTests
	{
		[Fact(DisplayName = "Delete company succeeds")]
		public async Task DeleteCompanySucceeds()
		{
			var companyMock = new Mock<Domain.Model.Company>();
			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company> { companyMock.Object }.AsQueryable().BuildMockDbSet();
			companyDbSetMock.Setup(x => x.FindAsync(new object[] { It.IsAny<Guid>() }, It.IsAny<CancellationToken>()))
							.ReturnsAsync(companyMock.Object);
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>())
						 .Returns(companyDbSetMock.Object);
			var commandMock = new Mock<DeleteCompany.Command>(It.IsAny<Guid>());

			var sut = new DeleteCompany.Handler(dbContextMock.Object);
			var result = await sut.Handle(commandMock.Object, new CancellationToken());

			dbContextMock.Verify(x => x.Set<Domain.Model.Company>().Remove(It.IsAny<Domain.Model.Company>()), Times.Once);
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
			var sut = new DeleteCompany.Validator(new Mock<AppDbContext>().Object);

			sut.RuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
		}

		[Theory(DisplayName = "Existing company passes validation correctly")]
		[AnonymousData]
		public async Task ExistingCompanyPassesValidationCorrectly(Domain.Model.Company company)
		{
			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company>(new[] { company }).AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);
			var cmdMock = new Mock<DeleteCompany.Command>(company.Id);

			var sut = new DeleteCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(cmdMock.Object, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

			validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
			validationResult.ShouldNotHaveAnyValidationErrors();
		}

		[Theory(DisplayName = "Non existing company passes validation correctly")]
		[AnonymousData]
		public async Task NonExistingCompanyPassesValidationCorrectly(Domain.Model.Company company, Guid id)
		{
			var dbContextMock = new Mock<AppDbContext>();
			var companyDbSetMock = new List<Domain.Model.Company>(new[] { company }).AsQueryable().BuildMockDbSet();
			dbContextMock.Setup(x => x.Set<Domain.Model.Company>()).Returns(companyDbSetMock.Object);
			var cmdMock = new Mock<DeleteCompany.Command>(id);

			var sut = new DeleteCompany.Validator(dbContextMock.Object);
			var validationResult = await sut.TestValidateAsync(cmdMock.Object, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

			validationResult.RuleSetsExecuted.Should().Contain(ValidatorsExtensions.ExistsRulesetName);
			validationResult.ShouldHaveValidationErrorFor(x => x.Id);
		}
	}
}
