using AwesomeAssertions;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Product Entity")]
public class ProductTests
{
	[Theory(DisplayName = "New product succeeds")]
	[AutoDomainData]
	public void NewProductSucceeds(string title,
								   string description,
								   decimal price,
								   Company company,
								   List<Image> pictures)
	{
		price = Math.Abs(price);	//positive always
		var sut = new Product(title,
							  description,
							  price,
							  company,
							  pictures,
							  pictures.First());

		sut.Title
		   .Should()
		   .Be(title);
		sut.Description
		   .Should()
		   .Be(description);
		sut.Price
		   .Should()
		   .Be(price);
		sut.Company
		   .Should()
		   .Be(company);
		sut.Pictures
		   .Should()
		   .Contain(pictures);
		sut.DefaultPicture
		   .Should()
		   .Be(pictures.First());
	}

	[Theory(DisplayName = "New product with empty title throws")]
	[AutoDomainData]
	public void NewProductWithEmptyTitleThrows(string description,
											   decimal price,
											   Company company,
											   List<Image> pictures)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(string.Empty,
									description,
									price,
									company,
									pictures,
									pictures.First());

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with title too long throws")]
	[AutoDomainData]
	public void NewProductWithTitleToLongThrows(string description,
												decimal price,
												Company company,
												List<Image> pictures)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(new string(It.IsAny<char>(), Product.TitleLength + 1),
									description,
									price,
									company,
									pictures,
									pictures.First());

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with empty description throws")]
	[AutoDomainData]
	public void NewProductWithEmptyDescriptionThrows(string name,
													 decimal price,
													 Company company,
													 List<Image> pictures)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(name,
									string.Empty,
									price,
									company,
									pictures,
									pictures.First());

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with description too long throws")]
	[AutoDomainData]
	public void NewProductWithDescriptionToLongThrows(string name,
													  decimal price,
													  Company company,
													  List<Image> pictures)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(name,
									new string(It.IsAny<char>(), Product.DescriptionLength + 1),
									price,
									company,
									pictures,
									pictures.First());

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with negative price throws")]
	[AutoDomainData]
	public void NewProductWithNegativePriceThrows(string name,
												  string description,
												  decimal price,
												  Company company,
												  List<Image> pictures)
	{
		price = -Math.Abs(price); //negative always
		var sut = () => new Product(name,
									description,
									price,
									company,
									pictures,
									pictures.First());

		sut.Should()
		   .ThrowExactly<ArgumentOutOfRangeException>();
	}

	[Theory(DisplayName = "Update product succeeds")]
	[AutoDomainData]
	public void UpdateProductSucceeds(Product sut,
									  string title,
									  string description,
									  decimal price,
									  Company company)
	{
		price = Math.Abs(price); //positive always
		sut.Update(title,
				   description,
				   price,
				   company);

		sut.Title
		   .Should()
		   .Be(title);
		sut.Description
		   .Should()
		   .Be(description);
		sut.Price
		   .Should()
		   .Be(price);
	}

	[Theory(DisplayName = "Update product with empty name fails")]
	[AutoDomainData]
	public void UpdateProductWithEmptyNameFails(Product sut,
												string description,
												decimal price,
												Company company)
	{
		price = Math.Abs(price); //positive always
		var call = () => sut.Update(string.Empty,
									description,
									price,
									company);

		call.Should()
			.ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "Add new picture succeeds")]
	[AutoDomainData]
	public void AddNewPictureSucceeds(Product sut, Image picture)
	{
		var picturesCount = sut.Pictures.Count;

		sut.AddPicture(picture);

		sut.Pictures
		   .Should()
		   .HaveCount(picturesCount + 1)
		   .And
		   .Contain(picture);
	}

	[Theory(DisplayName = "Add existing picture does nothing")]
	[AutoDomainData]
	public void AddExistingPictureDoesNothing(Product sut)
	{
		var action = () => sut.AddPicture(sut.Pictures.First());
		action.Should()
			  .NotThrow();
		sut.Pictures
		   .Should()
		   .HaveCount(3);
	}

	[Theory(DisplayName = "Set new picture as default succeeds")]
	[AutoDomainData]
	public void SetNewDefaultPictureSucceeds(Product sut)
	{
		var originalDefaultPicture = sut.DefaultPicture;
		var newDefaultPicture = sut.Pictures.Last();

		sut.SetDefaultPicture(newDefaultPicture);

		sut.DefaultPicture
		   .Should()
		   .Be(newDefaultPicture)
		   .And
		   .NotBe(originalDefaultPicture);
	}

	[Theory(DisplayName = "Set non-existing default picture throws")]
	[AutoDomainData]
	public void SetNonExistingDefaultPictureThrows(Product sut, Image picture)
	{
		var action = () => sut.SetDefaultPicture(picture);

		action.Should()
			  .ThrowExactly<ArgumentOutOfRangeException>();
	}

	[Theory(DisplayName = "Remove picture succeeds")]
	[AutoDomainData]
	public void RemovePictureSucceeds(Product sut)
	{
		var picturesCount = sut.Pictures.Count;

		sut.RemovePicture(sut.Pictures.First());

		sut.Pictures
		   .Should()
		   .HaveCount(picturesCount - 1);
	}

	[Theory(DisplayName = "Remove last picture throws")]
	[AutoDomainData]
	public void RemoveLastPictureThrows(Product sut)
	{
		sut.RemovePicture(sut.Pictures.First());
		sut.RemovePicture(sut.Pictures.First());

		var action = () => sut.RemovePicture(sut.Pictures.First());
		action.Should()
			  .ThrowExactly<InvalidOperationException>();
	}

	[Theory(DisplayName = "Remove non-existing picture does nothing")]
	[AutoDomainData]
	public void RemoveNonExistingPictureDoesNothing(Product sut, Image picture)
	{
		var action = () => sut.RemovePicture(picture);
		action.Should()
			  .NotThrow("because it should ignore removing a non-existing picture.");
		sut.Pictures
		   .Should()
		   .HaveCount(3);
	}

	[Theory(DisplayName = "Remove default picture succeeds")]
	[AutoDomainData]
	public void RemoveDefaultPictureSucceeds(Product sut)
	{
		var picturesCount = sut.Pictures.Count;

		sut.RemovePicture(sut.DefaultPicture);

		sut.Pictures
		   .Should()
		   .HaveCount(picturesCount - 1);
		sut.DefaultPicture
		   .Should()
		   .Be(sut.Pictures.First());
	}
}