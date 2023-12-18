using AutoFixture;
using Monaco.Template.Backend.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests.Factories.Entities;

public static class AddressFactory
{
	public static Address Create() =>
		new Fixture().RegisterAddress()
					 .RegisterCountry()
					 .Create<Address>();

	public static IEnumerable<Address> CreateMany() =>
		new Fixture().RegisterAddress()
					 .RegisterCountryMock()
					 .CreateMany<Address>();
}

public static class AddressFactoryExtensions
{
	public static IFixture RegisterAddress(this IFixture fixture)
	{
		fixture.Register(() => new Address(fixture.Create<string?>(),
										   fixture.Create<string?>(),
										   fixture.Create<string?>(),
										   fixture.Create<string?>()?[..10],
										   fixture.Create<Country>()));
		return fixture;
	}

	public static IFixture RegisterAddressMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var country = fixture.Create<Country>();
							 var mock = new Mock<Address>(fixture.Create<string?>(),
														  fixture.Create<string?>(),
														  fixture.Create<string?>(),
														  fixture.Create<string?>()?[..10],
														  country);
							 return mock.Object;
						 });
		return fixture;
	}
}