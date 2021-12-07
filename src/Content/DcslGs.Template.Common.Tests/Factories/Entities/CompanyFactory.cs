using AutoFixture;
using DcslGs.Template.Domain.Model;
using Moq;

namespace DcslGs.Template.Common.Tests.Factories.Entities;

public static class CompanyFactory
{
    public static Company Create()
    {
        return new Fixture().RegisterCompany()
                            .Create<Company>();
    }

    public static IEnumerable<Company> CreateMany()
    {
        return new Fixture().RegisterCompanyMock()
                            .CreateMany<Company>();
    }
}

public static class CompanyFactoryExtension
{
    public static IFixture RegisterCompany(this IFixture fixture)
    {
        fixture.Register(() => new Company(fixture.Create<string>(),
                                           fixture.Create<string>(),
                                           fixture.Create<string>(),
                                           fixture.Create<string>(),
                                           fixture.Create<string>(),
                                           fixture.Create<string>(),
                                           fixture.Create<string>(),
                                           fixture.Create<Country>()));
        return fixture;
    }

    public static IFixture RegisterCompanyMock(this IFixture fixture)
    {
        fixture.Register(() =>
                         {
                             var mock = new Mock<Company>(fixture.Create<string>(),
                                                          fixture.Create<string>(),
                                                          fixture.Create<string>(),
                                                          fixture.Create<string>(),
                                                          fixture.Create<string>(),
                                                          fixture.Create<string>(),
                                                          fixture.Create<string>(),
                                                          fixture.Create<Country>());
                             mock.SetupGet(x => x.Id).Returns(Guid.NewGuid());
                             return mock.Object;
                         });
        return fixture;
    }
}