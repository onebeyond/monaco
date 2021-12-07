using AutoFixture;
using DcslGs.Template.Domain.Model;
using Moq;

namespace DcslGs.Template.Common.Tests.Factories.Entities;

public class CountryFactory
{
    public static Country Create()
    {
        return new Fixture().RegisterCountry()
                            .Create<Country>();
    }

    public static IEnumerable<Country> CreateMany()
    {
        return new Fixture().RegisterCountryMock()
                            .CreateMany<Country>();
    }
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