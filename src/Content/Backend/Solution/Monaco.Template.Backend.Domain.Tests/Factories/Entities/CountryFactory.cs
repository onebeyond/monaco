using AutoFixture;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model.Entities;
using Moq;

namespace Monaco.Template.Backend.Domain.Tests.Factories.Entities;

public static class CountryFactory
{
	public static Country Create() =>
		FixtureFactory.Create(f => f.RegisterCountry())
					  .Create<Country>();

	public static IEnumerable<Country> CreateMany() =>
		FixtureFactory.Create(f => f.RegisterCountryMock())
					  .CreateMany<Country>();
}

public static class CountryFactoryExtensions
{
	public static IFixture RegisterCountry(this IFixture fixture)
	{
		fixture.Register(() => new Country(fixture.Create<string>()));

		return fixture;
	}

	public static IFixture RegisterCountryMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var mock = new Mock<Country>(fixture.Create<string>());
							 mock.SetupGet(x => x.Id).Returns(Guid.NewGuid());
							 return mock.Object;
						 });
		return fixture;
	}
}