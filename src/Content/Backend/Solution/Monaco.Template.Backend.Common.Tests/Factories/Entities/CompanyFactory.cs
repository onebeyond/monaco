using AutoFixture;
using Monaco.Template.Backend.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests.Factories.Entities;

public static class CompanyFactory
{
	public static Company Create() =>
		new Fixture().RegisterCompany()
					 .RegisterAddress()
					 .Create<Company>();

	public static IEnumerable<Company> CreateMany() =>
		new Fixture().RegisterCompanyMock()
					 .RegisterAddress()
					 .CreateMany<Company>();
}

public static class CompanyFactoryExtension
{
	public static IFixture RegisterCompany(this IFixture fixture)
	{
		fixture.Register(() => new Company(fixture.Create<string>(),
										   fixture.Create<string>(),
										   fixture.Create<string>(),
										   fixture.Create<Address>()));
		return fixture;
	}

	public static IFixture RegisterCompanyMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var mock = new Mock<Company>(fixture.Create<string>(),
														  fixture.Create<string>(),
														  fixture.Create<string>(),
														  fixture.Create<Address>());
							 mock.SetupGet(x => x.Id).Returns(Guid.NewGuid());
							 #if (!excludeFilesSupport)
							 mock.SetupGet(x => x.Products).Returns(new List<Product>());
							 #endif
							 return mock.Object;
						 });
		return fixture;
	}
}