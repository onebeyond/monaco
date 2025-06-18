using AutoFixture;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model.Entities;
using Moq;

namespace Monaco.Template.Backend.Domain.Tests.Factories.Entities;

public static class ProductFactory
{
	public static Product Create() =>
		FixtureFactory.Create(f => f.RegisterImage()
									.RegisterAddress()
									.RegisterCompany()
									.RegisterProduct())
					  .Create<Product>();

	public static IEnumerable<Product> CreateMany() =>
		FixtureFactory.Create(f => f.RegisterImage()
									.RegisterAddress()
									.RegisterCompanyMock()
									.RegisterProductMock())
					  .CreateMany<Product>();
}

public static class ProductFactoryExtension
{
	public static IFixture RegisterProduct(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var images = fixture.CreateMany<Image>().ToList();
							 var product = new Product(fixture.Create<string>(),
													   fixture.Create<string>(),
													   fixture.Create<decimal>(),
													   fixture.Create<Company>(),
													   images,
													   images.First());

							 return product;
						 });
		return fixture;
	}

	public static IFixture RegisterProductMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var images = fixture.CreateMany<Image>().ToList();
							 var mock = new Mock<Product>(fixture.Create<string>(),
														  fixture.Create<string>(),
														  fixture.Create<decimal>(),
														  fixture.Create<Company>(),
														  images,
														  images.First());
							 mock.SetupGet(x => x.Id)
								 .Returns(Guid.NewGuid());
							 mock.SetupGet(x => x.Company)
								 .Returns(fixture.Create<Company>());
							 mock.SetupGet(x => x.Pictures)
								 .Returns([.. images]);
							 mock.SetupGet(x => x.DefaultPicture)
								 .Returns(images.First());

							 return mock.Object;
						 });
		return fixture;
	}
}