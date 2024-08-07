using FluentAssertions;
using Monaco.Template.Backend.Domain.Model;
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
								   decimal price)
	{
		price = Math.Abs(price);	//positive always
		var sut = new Product(title,
							  description,
							  price);

		sut.Title.Should().Be(title);
		sut.Description.Should().Be(description);
		sut.Price.Should().Be(price);
	}

	[Theory(DisplayName = "New product with empty name throws")]
	[AutoDomainData]
	public void NewProductWithEmptyNameThrows(string description,
											  decimal price)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(string.Empty,
									description,
									price);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with name too long throws")]
	[AutoDomainData]
	public void NewProductWithNameToLongThrows(string description,
											  decimal price)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(new string(It.IsAny<char>(), 101),
									description,
									price);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with empty description throws")]
	[AutoDomainData]
	public void NewProductWithEmptyDescriptionThrows(string name,
													 decimal price)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(name,
									string.Empty,
									price);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with description too long throws")]
	[AutoDomainData]
	public void NewProductWithDescriptionToLongThrows(string name,
													  decimal price)
	{
		price = Math.Abs(price); //positive always
		var sut = () => new Product(name,
									new string(It.IsAny<char>(), 501),
									price);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New product with negative price throws")]
	[AutoDomainData]
	public void NewProductWithNegativePriceThrows(string name,
												  string description,
												  decimal price)
	{
		price = -Math.Abs(price); //negative always
		var sut = () => new Product(name,
									description,
									price);

		sut.Should()
		   .ThrowExactly<ArgumentOutOfRangeException>();
	}

	[Theory(DisplayName = "Update product succeeds")]
	[AutoDomainData]
	public void UpdateProductSucceeds(Product sut,
									  string title,
									  string description,
									  decimal price)
	{
		price = Math.Abs(price); //positive always
		sut.Update(title,
				   description,
				   price);

		sut.Title.Should().Be(title);
		sut.Description.Should().Be(description);
		sut.Price.Should().Be(price);
	}

	[Theory(DisplayName = "Update product with empty name fails")]
	[AutoDomainData]
	public void UpdateProductWithEmptyNameFails(Product sut,
												string description,
												decimal price)
	{
		price = Math.Abs(price); //positive always
		var call = () => sut.Update(string.Empty,
									description,
									price);

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

	[Theory(DisplayName = "Set new picture as default succeeds")]
	[AutoDomainData]
	public void AddNewDefaultPictureSucceeds(Product sut, Image picture)
	{
		var originalDefaultPicture = sut.DefaultPicture;

		sut.AddPicture(picture, true);

		sut.DefaultPicture
		   .Should()
		   .Be(picture)
		   .And
		   .NotBe(originalDefaultPicture);
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