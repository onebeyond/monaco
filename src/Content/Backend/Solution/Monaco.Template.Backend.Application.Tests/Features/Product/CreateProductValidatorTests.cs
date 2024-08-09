using AutoFixture;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Create")]
public class CreateProductValidatorTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly CreateProduct.Command Command;

	static CreateProductValidatorTests()
	{
		var fixture = new Fixture();
		Command = new(fixture.Create<string>(),		// Title
					  fixture.Create<string>(),		// Description
					  fixture.Create<decimal>(),	// Price
					  fixture.Create<Guid>(),		// CompanyId
					  fixture.Create<Guid[]>(),		// Pictures
					  fixture.Create<Guid>());		// DefaultPictureId
	}

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CreateProduct.Validator(new Mock<AppDbContext>().Object);

		sut.RuleLevelCascadeMode
		   .Should()
		   .Be(CascadeMode.Stop);
	}

	[Fact(DisplayName = "Title being valid does not generate validation error")]
	public async Task TitleDoesNotGenerateErrorWhenValid()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Product>());

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(Command, strategy => strategy.IncludeProperties(cmd => cmd.Title));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Title);
	}

	[Fact(DisplayName = "Title with empty value generates validation error")]
	public async Task TitleIsEmptyGeneratesError()
	{
		var command = Command with { Title = string.Empty };

		var sut = new CreateProduct.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Title));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Title)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Title with long value generates validation error")]
	public async Task TitleWithLongValueGeneratesError()
	{
		var command = Command with { Title = new string(It.IsAny<char>(), Domain.Model.Product.TitleLength + 1) };

		var sut = new CreateProduct.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Title));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Title)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Domain.Model.Product.TitleLength)
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Title which already exists generates validation error")]
	[AutoDomainData]
	public async Task TitleAlreadyExistsGeneratesError(Domain.Model.Product product)
	{
		_dbContextMock.CreateAndSetupDbSetMock(product);

		var command = Command with { Title = product.Title };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Title));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Title)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Description being valid does not generate validation error")]
	public async Task DescriptionDoesNotGenerateErrorWhenValid()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Product>());

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(Command, strategy => strategy.IncludeProperties(cmd => cmd.Description));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Description);
	}
	
	[Fact(DisplayName = "Description with empty value generates validation error")]
	public async Task DescriptionIsEmptyGeneratesError()
	{
		var command = Command with { Description = string.Empty };

		var sut = new CreateProduct.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Description));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Description)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Description with long value generates validation error")]
	public async Task DescriptionWithLongValueGeneratesError()
	{
		var command = Command with { Description = new string(It.IsAny<char>(), Domain.Model.Product.DescriptionLength + 1) };

		var sut = new CreateProduct.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Description));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Description)
						.WithErrorCode("MaximumLengthValidator")
						.WithMessageArgument("MaxLength", Domain.Model.Product.DescriptionLength)
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Price being valid does not generate validation error")]
	public async Task PriceDoesNotGenerateErrorWhenValid()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Product>());

		var command = Command with { Price = 1m };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Price));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Price);
	}

	[Fact(DisplayName = "Price with negative value generates validation error")]
	public async Task PriceIsNegativeGeneratesError()
	{
		var command = Command with { Price = -1m };

		var sut = new CreateProduct.Validator(new Mock<AppDbContext>().Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Price));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Price)
						.WithErrorCode("GreaterThanOrEqualValidator")
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "CompanyId being valid does not generate validation error")]
	[AutoDomainData(true)]
	public async Task CompanyIdDoesNotGenerateErrorWhenValid(Domain.Model.Company[] companies)
	{
		_dbContextMock.CreateAndSetupDbSetMock(companies);

		var command = Command with { CompanyId = companies.First().Id };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.CompanyId));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.CompanyId);
	}

	[Fact(DisplayName = "CompanyId with empty value generates validation error")]
	public async Task CompanyIdIsEmptyGeneratesError()
	{
		var command = Command with { CompanyId = Guid.Empty };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.CompanyId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.CompanyId)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "CompanyId with non-existing value generates validation error")]
	public async Task CompanyIdNotExistsGeneratesError()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Company>());

		var command = Command with { CompanyId = Guid.NewGuid() };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.CompanyId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.CompanyId)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Pictures being valid does not generate validation error")]
	[AutoDomainData(true)]
	public async Task PicturesDoesNotGenerateErrorWhenValid(Domain.Model.Product[] products, Image[] newPictures)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);
		_dbContextMock.CreateAndSetupDbSetMock(newPictures);

		var picturesIds = newPictures.Select(x => x.Id)
									 .ToArray();

		var command = Command with { Pictures = picturesIds };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Pictures));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Pictures);
	}

	[Fact(DisplayName = "Pictures empty array generates validation error")]
	public async Task PictureArrayEmptyGeneratesError()
	{
		var command = Command with { Pictures = [] };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Pictures));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Pictures)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Pictures with empty element generates validation error")]
	public async Task PictureEmptyElementGeneratesError()
	{
		var command = Command with { Pictures = [Guid.Empty] };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Pictures));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Pictures)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Pictures with non-existing element generates validation error")]
	public async Task PicturesWithNonExistingElementGeneratesError()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Image>());

		var command = Command with { Pictures = [Guid.NewGuid()] };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Pictures));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Pictures)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Pictures with another product's picture generates validation error")]
	public async Task PicturesWithAnotherProductPictureGeneratesError()
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Image>());

		var command = Command with { Pictures = [Guid.NewGuid()] };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Pictures));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Pictures)
						.WithErrorCode("AsyncPredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Theory(DisplayName = "Default Picture being valid does not generate validation error")]
	[AutoDomainData(true)]
	public async Task DefaultPictureDoesNotGenerateErrorWhenValid(Guid[] picturesIds)
	{
		var command = Command with
					  {
						  Pictures = picturesIds,
						  DefaultPictureId = picturesIds.First()
					  };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.DefaultPictureId));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.DefaultPictureId);
	}

	[Fact(DisplayName = "Default Picture with empty value generates validation error")]
	public async Task DefaultPictureIsEmptyGeneratesError()
	{
		var command = Command with { DefaultPictureId = Guid.Empty };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.DefaultPictureId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.DefaultPictureId)
						.WithErrorCode("NotEmptyValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Default Picture with non-existing value generates validation error")]
	public async Task DefaultPictureNotExistsGeneratesError()
	{
		var command = Command with
					  {
						  Pictures = [Guid.NewGuid()],
						  DefaultPictureId = Guid.NewGuid()
					  };

		var sut = new CreateProduct.Validator(_dbContextMock.Object);
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.DefaultPictureId));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.DefaultPictureId)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}
}