using AutoFixture;
using Monaco.Template.Backend.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests.Factories.Entities;

public static class ProductFactory
{
	public static Product Create() =>
		new Fixture().RegisterImage()
					 .RegisterAddress()
					 .RegisterCompany()
					 .RegisterProduct()
					 .Create<Product>();

	public static IEnumerable<Product> CreateMany() =>
		new Fixture().RegisterImage()
					 .RegisterAddress()
					 .RegisterCompany()
					 .RegisterProductMock()
					 .CreateMany<Product>();
}

public static class ProductFactoryExtension
{
	public static IFixture RegisterProduct(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var product = new Product(fixture.Create<string>(),
													   fixture.Create<string>(),
													   fixture.Create<decimal>());
							 var images = fixture.CreateMany<Image>();
							 foreach (var image in images)
								 product.AddPicture(image);

							 return product;
						 });
		return fixture;
	}

	public static IFixture RegisterProductMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var mock = new Mock<Product>(fixture.Create<string>(),
														  fixture.Create<string>(),
														  fixture.Create<decimal>());
							 mock.SetupGet(x => x.Id)
								 .Returns(Guid.NewGuid());
							 mock.SetupGet(x => x.Company)
								 .Returns(fixture.Create<Company>());

							 var images = fixture.CreateMany<Image>();
							 mock.SetupGet(x => x.Pictures)
								 .Returns(images.ToArray());

							 return mock.Object;
						 });
		return fixture;
	}
}