using AutoFixture;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Domain.Tests.Factories.Entities;

public static class CompanyFactory
{
	public static Company Create() =>
		FixtureFactory.Create(f => f.RegisterCompany()
									.RegisterAddress())
					  .Create<Company>();

	public static IEnumerable<Company> CreateMany() =>
		FixtureFactory.Create(f => f.RegisterCompanyMock()
									.RegisterAddress())
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
#if (filesSupport)
							 mock.SetupGet(x => x.Products).Returns(new List<Product>());
#endif
							 return mock.Object;
						 });
		return fixture;
	}
}