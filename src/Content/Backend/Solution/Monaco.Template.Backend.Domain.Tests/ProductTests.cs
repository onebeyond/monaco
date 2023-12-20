using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Product Entity")]
public class ProductTests
{

	[Theory(DisplayName = "New product succeeds")]
	[AnonymousData]
	public void NewProductSucceeds(string title,
								   string description,
								   decimal price)
	{
		price *= price;	//positive always
		var sut = new Product(title,
							  description,
							  price);

		sut.Title.Should().Be(title);
		sut.Description.Should().Be(description);
		sut.Price.Should().Be(price);
	}

	[Theory(DisplayName = "Update product succeeds")]
	[AnonymousData]
	public void UpdateProductSucceeds(Product sut,
									  string title,
									  string description,
									  decimal price)
	{
		price *= price; //positive always
		sut.Update(title,
				   description,
				   price);

		sut.Title.Should().Be(title);
		sut.Description.Should().Be(description);
		sut.Price.Should().Be(price);
	}

	[Theory(DisplayName = "Add new picture succeeds")]
	[AnonymousData]
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
	[AnonymousData]
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
	[AnonymousData]
	public void RemovePictureSucceeds(Product sut)
	{
		var picturesCount = sut.Pictures.Count;

		sut.RemovePicture(sut.Pictures.First());

		sut.Pictures
		   .Should()
		   .HaveCount(picturesCount - 1);
	}

	[Theory(DisplayName = "Remove default picture succeeds")]
	[AnonymousData]
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