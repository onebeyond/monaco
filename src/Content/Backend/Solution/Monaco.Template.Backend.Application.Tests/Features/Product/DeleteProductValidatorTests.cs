using AutoFixture;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Delete")]
public class DeleteProductValidatorTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteProduct.Command Command = new(new Fixture().Create<Guid>());	// Id

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new DeleteProduct.Validator(new Mock<AppDbContext>().Object);

		sut.RuleLevelCascadeMode
		   .Should()
		   .Be(CascadeMode.Stop);
	}

	[Theory(DisplayName = "Existing Product passes validation correctly")]
	[AutoDomainData]
	public async Task ExistingProductPassesValidationCorrectly(Domain.Model.Product product)
	{
		_dbContextMock.CreateAndSetupDbSetMock(product);

		var command = Command with { Id = product.Id };

		var sut = new DeleteProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted
						.Should()
						.Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldNotHaveAnyValidationErrors();
	}

	[Theory(DisplayName = "Non existing Product generates validation error")]
	[AutoDomainData]
	public async Task NonExistingProductGeneratesError(Domain.Model.Product product, Guid id)
	{
		_dbContextMock.CreateAndSetupDbSetMock(product);

		var command = Command with { Id = id };
		
		var sut = new DeleteProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, s => s.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName));

		validationResult.RuleSetsExecuted
						.Should()
						.Contain(ValidatorsExtensions.ExistsRulesetName);
		validationResult.ShouldHaveValidationErrorFor(x => x.Id);
	}
}